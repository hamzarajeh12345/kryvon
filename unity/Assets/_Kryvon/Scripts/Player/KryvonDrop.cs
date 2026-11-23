// The start of every match:
//   1. WAITING AREA – the forward base outside the map; a countdown until the plane takes off
//   2. PLANE        – the drop jet crosses the map on a straight line that passes over the region picked in the lobby;
//                     F / JUMP (or the end of the line) jumps out
//   3. FREEFALL     – W dives faster (~55 m/s down, up to 35 m/s forward), steer with the camera
//   4. PARACHUTE    – F opens it, or it opens by itself 130 m above the ground; ~6 m/s down, glide 14 m/s
//   5. LANDED       – normal play; the zone starts counting from the moment you jump
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    [DefaultExecutionOrder(-140)]
    public class KryvonDrop : MonoBehaviour
    {
        public enum Stage { Waiting, Plane, Freefall, Chute, Landed }
        public static KryvonDrop Instance { get; private set; }
        public static bool Active => Instance && Instance.S != Stage.Landed;
        public Stage S { get; private set; } = Stage.Waiting;
        public float Timer { get; private set; }
        public float Height { get; private set; }
        public float PathT => pathT;
        public Vector3 PlanePos => Vector3.Lerp(pathA, pathB, pathT);
        public bool OverMap(Vector3 p) => Mathf.Abs(p.x) < HX - 20 && Mathf.Abs(p.z) < HZ - 20;
        public float HX => mapHalfX > 0 ? mapHalfX : mapHalf; public float HZ => mapHalfZ > 0 ? mapHalfZ : mapHalf;

        [SerializeField] float waitSeconds = 45f, altitude = 300f, planeSpeed = 48f, mapHalf = 470f, mapHalfX = 0f, mapHalfZ = 0f;
        [SerializeField] float autoOpenHeight = 130f;
        const float AutoOpenAt = 100f;   // wins over the scene value
        [SerializeField] GameObject jetTemplate, chuteTemplate;
        [SerializeField] AudioClip engineLoop, windLoop, chuteOpen;
        [SerializeField] Font font;

        PlayerController body; CharacterController cc; ThirdPersonCamera rig; PlayerInputReader input; WeaponController weapons;
        GameObject jet, chute; LineRenderer[] cords; AudioSource engine, wind;
        Vector3 pathA, pathB, vel; float pathT, sinceJump; bool flightAuto;
        public Vector3 PathA => pathA; public Vector3 PathB => pathB;
        readonly List<Renderer> hidden = new();

        void Awake() { Instance = this; if (!GetComponent<KryvonClimb>()) gameObject.AddComponent<KryvonClimb>(); }

        void Start()
        {
            body = GetComponent<PlayerController>(); cc = GetComponent<CharacterController>(); input = GetComponent<PlayerInputReader>();
            weapons = GetComponent<WeaponController>(); rig = Camera.main ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            flightAuto = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") >= 0 || KryvonTrailer.On;
            var area = GameObject.Find("WaitingSpawn");
            if (!area) { Land(); return; }
            var p = area.transform.position + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-4f, 4f));
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.1f;
            Teleport(p, area.transform.rotation);
            Timer = flightAuto ? 4f : waitSeconds;
            PlanFlight();
        }

        void Teleport(Vector3 p, Quaternion r) { if (cc) cc.enabled = false; transform.SetPositionAndRotation(p, r); if (cc && S == Stage.Waiting) cc.enabled = true; }

        // a straight line across the map that passes close to the region picked in the lobby
        void PlanFlight()
        {
            var sp = GameObject.Find("Spawn_" + PlayerPrefs.GetInt("kryvon.region", 0));
            var via = sp ? sp.transform.position : Vector3.zero; via.y = 0;
            via += new Vector3(Random.Range(-60f, 60f), 0, Random.Range(-60f, 60f));
            float a = Random.Range(0f, Mathf.PI * 2f); var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            // extend both ways until the line leaves the map square (+ a margin for the approach)
            float Exit(Vector3 d) { float t = 0; while (t < 3000 && Mathf.Abs(via.x + d.x * t) < HX + 150 && Mathf.Abs(via.z + d.z * t) < HZ + 150) t += 10; return t; }
            pathA = via - dir * Exit(-dir); pathB = via + dir * Exit(dir);
            pathA.y = pathB.y = altitude;
            // in the self-test the plane starts right before the region
            pathT = flightAuto ? Mathf.Clamp01(1f - (Vector3.Distance(via, pathB - Vector3.up * altitude) + 200f) / Vector3.Distance(pathA, pathB)) : 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (S)
            {
                case Stage.Waiting:
                    Timer -= dt; if (Timer <= 0) Board(); break;
                case Stage.Plane:
                {
                    float len = Vector3.Distance(pathA, pathB); pathT += planeSpeed * dt / len;
                    var p = Vector3.Lerp(pathA, pathB, pathT);
                    jet.transform.SetPositionAndRotation(p, Quaternion.LookRotation(pathB - pathA) * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 0.6f) * 2f));
                    transform.position = p - Vector3.up * 3f;
                    Timer = (1f - pathT) * len / planeSpeed;
                    bool overMap = Mathf.Abs(p.x) < HX && Mathf.Abs(p.z) < HZ;
                    if ((input.InteractPressed || input.JumpPressed) && overMap) Jump();
                    else if (pathT >= 0.86f && overMap || pathT >= 0.98f) Jump();
                    else if (flightAuto && !KryvonTrailer.On && pathT > 0.02f && overMap && Timer < len / planeSpeed * 0.75f) Jump();
                    break;
                }
                case Stage.Freefall: case Stage.Chute:
                    if (S == Stage.Chute && (input.InteractPressed || input.JumpPressed)) { if (CanCloseChute) CloseChute(); else KryvonKillFeed.Notice("TOO LOW TO CLOSE THE PARACHUTE"); }
                    Fly(dt); break;
            }
            bool air = S == Stage.Plane || S == Stage.Freefall || S == Stage.Chute;
            KryvonGraphics.SkyView(air, air ? (S == Stage.Plane ? altitude : Height) : 0f);
            if (S != Stage.Waiting && S != Stage.Plane && pathT < 1.05f)
            {
                pathT += planeSpeed * dt / Mathf.Max(1f, Vector3.Distance(pathA, pathB));
                if (jet) { jet.transform.position = PlanePos; if (pathT >= 1.05f) Destroy(jet); }
            }
        }

        void Board()
        {
            S = Stage.Plane;
            if (cc) cc.enabled = false; if (body) body.enabled = false; if (weapons) weapons.enabled = false; Toggle(false);
            hidden.Clear(); foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            jet = jetTemplate ? Instantiate(jetTemplate) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            jet.SetActive(true); jet.name = "DropJet";
            { var js = KryvonSkins.Equipped(CosKind.Jet); if (js != null && js.tex != null) KryvonSkins.Apply(jet, js); }   // Shaheen livery from the Nashama Pass
            foreach (var c in jet.GetComponentsInChildren<Collider>()) Destroy(c);
            engine = jet.AddComponent<AudioSource>(); engine.clip = engineLoop; engine.loop = true; engine.spatialBlend = 0.6f; engine.pitch = 0.55f; engine.volume = 0.7f; engine.minDistance = 40; if (engineLoop) engine.Play();
            if (rig) { rig.Mode = CameraMode.Parachute; rig.DistanceOverride = 34f; }
        }

        void Toggle(bool on) { foreach (var b in new Behaviour[] { GetComponent<KryvonLootPicker>(), GetComponent<KryvonThrowables>(), GetComponent<KryvonFootsteps>() }) if (b) b.enabled = on; }

        void Jump()
        {
            S = Stage.Freefall; sinceJump = 0;
            foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear();
            vel = (pathB - pathA).normalized * planeSpeed * 0.5f;
            if (engine) engine.spatialBlend = 1f;
            wind = gameObject.AddComponent<AudioSource>(); wind.clip = windLoop; wind.loop = true; wind.volume = 0.9f; if (windLoop) wind.Play();
            if (rig) rig.DistanceOverride = 8f;
            var zone = FindAnyObjectByType<SafeZone>(); if (zone) zone.Begin();
        }

        float GroundBelow(out Vector3 point)
        {
            point = transform.position - Vector3.up * 999f;
            var hits = Physics.RaycastAll(transform.position + Vector3.up, Vector3.down, 2000f, ~0, QueryTriggerInteraction.Ignore);
            float best = 9999f;
            foreach (var h in hits) { if (h.collider.transform.IsChildOf(transform) || (chute && h.collider.transform.IsChildOf(chute.transform))) continue; if (h.distance < best) { best = h.distance; point = h.point; } }
            return best - 1f;
        }

        void Fly(float dt)
        {
            sinceJump += dt;
            Height = GroundBelow(out var ground);
            float yaw = rig ? rig.MoveReferenceYaw : transform.eulerAngles.y;
            var m = input.Move; var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward; var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            Vector3 want; float down;
            if (S == Stage.Freefall)
            {
                float dive = Mathf.Max(0, m.y);
                want = fwd * (m.y >= 0 ? 5f + 15f * dive : 5f * (1f + m.y)) + right * m.x * 6f;
                down = Mathf.Lerp(8f, 20f, dive * dive);          // ~50% slower than before: a 430 m jump lasts 75–95 s          // belly-down drifts slowly; pushing forward = head-first dive
                if ((input.InteractPressed || input.JumpPressed) && sinceJump > 1.2f || Height < AutoOpenAt) OpenChute();
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yaw, 0), 1f - Mathf.Exp(-6f * dt));
                TiltBody(Mathf.Lerp(78f, 165f, dive), dt);            // 78° ≈ flat, face to the ground; 165° = head down, feet up
            }
            else
            {
                want = fwd * Mathf.Lerp(4.5f, 9f, (m.y + 1f) * 0.5f) + right * m.x * 4f;
                down = 2.2f;                                   // canopy: 2.2 m/s (slow, readable landing), W only glides forward
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yaw, -m.x * 12f), 1f - Mathf.Exp(-4f * dt));
                TiltBody(0f, dt);
                UpdateCords();
            }
            var h = new Vector3(vel.x, 0, vel.z); h = Vector3.Lerp(h, want, 1f - Mathf.Exp(-1.6f * dt));
            vel = new Vector3(h.x, Mathf.Lerp(vel.y, -down, 1f - Mathf.Exp(-2.5f * dt)), h.z);
            // over the sea: the canopy keeps you up and glides you to the nearest shore (nobody lands in the water)
            if (S == Stage.Chute && OverWater(ground))
            {
                var shore = NearestLand(transform.position); var to = shore - transform.position; to.y = 0;
                if (to.sqrMagnitude > 1f) vel = new Vector3(Mathf.Lerp(vel.x, to.normalized.x * 15f, 1f - Mathf.Exp(-2f * dt)), Mathf.Max(vel.y, -1.2f), Mathf.Lerp(vel.z, to.normalized.z * 15f, 1f - Mathf.Exp(-2f * dt)));
                if (Time.time > nextShoreTip) { nextShoreTip = Time.time + 6f; KryvonKillFeed.Notice("WATER BELOW  ·  GLIDING TO THE SHORE"); }
            }
            var next = transform.position + vel * dt;
            // stay on the map
            next.x = Mathf.Clamp(next.x, -HX + 10, HX - 10); next.z = Mathf.Clamp(next.z, -HZ + 10, HZ - 10);
            if (S == Stage.Chute && OverWater(ground) && next.y < SeaY + 4f) next.y = SeaY + 4f;      // skim above the waves until the beach
            else if (next.y <= ground.y + 0.05f || Height < 0.6f) { transform.position = new Vector3(next.x, ground.y + 0.05f, next.z); Land(); return; }
            transform.position = next;
            if (wind) { wind.volume = S == Stage.Freefall ? 0.95f : 0.3f; wind.pitch = S == Stage.Freefall ? 1.1f : 0.8f; }
        }

        // tilt the visible hero around the middle of the body (so the camera stays on it)
        float tilt; Transform heroModel; Vector3 basePos; Quaternion baseRot;
        void TiltBody(float target, float dt)
        {
            if (!heroModel) { foreach (Transform c in transform) if (c.gameObject.activeSelf && c.GetComponentInChildren<Animator>()) { heroModel = c; break; } if (!heroModel) return; basePos = heroModel.localPosition; baseRot = heroModel.localRotation; }
            tilt = Mathf.LerpAngle(tilt, target, 1f - Mathf.Exp(-5f * dt));
            var mid = new Vector3(0, 0.95f, 0); var r = Quaternion.Euler(tilt, 0, 0);
            heroModel.localRotation = r * baseRot; heroModel.localPosition = r * (basePos - mid) + mid;
        }

        void OpenChute()
        {
            S = Stage.Chute;
            if (chuteOpen) AudioSource.PlayClipAtPoint(chuteOpen, transform.position, 1f);
            chute = chuteTemplate ? Instantiate(chuteTemplate, transform) : null;
            if (chute)
            {
                chute.SetActive(true);
                var skin = KryvonSkins.Equipped(CosKind.Parachute); if (skin != null && skin.tex != null) KryvonSkins.Apply(chute, skin);   // CLASSIC = the canopy's own Jordan design
                chute.transform.localPosition = new Vector3(0, 6.2f, 0.3f); chute.transform.localRotation = Quaternion.identity;
                foreach (var c in chute.GetComponentsInChildren<Collider>()) Destroy(c);
                var mat = Resources.Load<Material>("KryvonUnlit");
                cords = new LineRenderer[6];
                for (int i = 0; i < cords.Length; i++)
                {
                    var g = new GameObject("Cord"); g.transform.SetParent(chute.transform, false);
                    var l = g.AddComponent<LineRenderer>(); l.positionCount = 2; l.widthMultiplier = 0.02f; l.useWorldSpace = true;
                    if (mat) { l.material = new Material(mat); l.material.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f)); }
                    cords[i] = l;
                }
                StartCoroutine(Inflate());
            }
            if (rig) rig.DistanceOverride = 11f;
        }

        /// the same button closes the canopy again: back to free fall (only high enough to open it once more)
        public bool CanCloseChute => S == Stage.Chute && Height > AutoOpenAt + 40f && !closing;
        bool closing;
        public void CloseChute()
        {
            if (!CanCloseChute) return;
            S = Stage.Freefall; sinceJump = 0.4f;                 // can be opened again ~0.8 s later
            if (chuteOpen) AudioSource.PlayClipAtPoint(chuteOpen, transform.position, 0.6f);
            if (chute) { chute.transform.SetParent(null, true); StartCoroutine(Deflate(chute)); chute = null; }
            cords = null;
            if (rig) rig.DistanceOverride = 8f;
            KryvonKillFeed.Notice("PARACHUTE CLOSED  ·  TAP AGAIN TO OPEN");
        }
        System.Collections.IEnumerator Deflate(GameObject c)
        {   // the canopy collapses and drifts up and away
            closing = true; var s0 = c.transform.localScale; var p0 = c.transform.position;
            for (float t = 0; t < 0.6f && c; t += Time.deltaTime)
            {
                float k = t / 0.6f; c.transform.localScale = new Vector3(s0.x * (1f - 0.8f * k), s0.y * (1f - 0.5f * k), s0.z * (1f - 0.8f * k));
                c.transform.position = p0 + Vector3.up * (6f * k);
                yield return null;
            }
            if (c) Destroy(c); closing = false;
        }

        System.Collections.IEnumerator StandAfterLanding()
        {
            for (int i = 0; i < 6; i++) { yield return null; if (body && body.Stance != Stance.Stand) body.TrySetStance(Stance.Stand); }
        }
        System.Collections.IEnumerator Inflate()
        {
            for (float t = 0; t < 0.7f && chute; t += Time.deltaTime) { float k = Mathf.SmoothStep(0.15f, 1f, t / 0.7f); chute.transform.localScale = new Vector3(k, Mathf.Lerp(0.4f, 1f, k), k); yield return null; }
            if (chute) chute.transform.localScale = Vector3.one;
        }

        void UpdateCords()
        {
            if (!chute || cords == null) return;
            var shoulder = transform.position + Vector3.up * 1.55f;
            for (int i = 0; i < cords.Length; i++)
            {
                float x = Mathf.Lerp(-3.4f, 3.4f, i % 3 / 2f) * chute.transform.localScale.x; float z = i < 3 ? 0.9f : -0.9f;
                cords[i].SetPosition(0, chute.transform.TransformPoint(new Vector3(x / Mathf.Max(0.01f, chute.transform.localScale.x), -0.3f, z)));
                cords[i].SetPosition(1, shoulder + transform.right * (i % 3 - 1) * 0.18f);
            }
        }

        // the sea surface (none = no water on this map)
        float seaY = float.NaN, nextShoreTip;
        float SeaY { get { if (float.IsNaN(seaY)) { var g = GameObject.Find("Sea"); seaY = g ? g.transform.position.y : -1e9f; } return seaY; } }
        bool OverWater(Vector3 ground) => ground.y < SeaY - 0.3f;
        /// the closest point of dry land (terrain above the sea), searched in rings around p
        Vector3 NearestLand(Vector3 p)
        {
            var ter = Terrain.activeTerrain; if (!ter) return p;
            for (float r = 20f; r <= 1500f; r += 20f)
            {
                Vector3 best = p; float bd = float.MaxValue; int n = Mathf.Max(12, Mathf.RoundToInt(r / 8f));
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n; var q = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                    q.x = Mathf.Clamp(q.x, -HX + 10, HX - 10); q.z = Mathf.Clamp(q.z, -HZ + 10, HZ - 10);
                    float y = ter.SampleHeight(q) + ter.transform.position.y;
                    if (y > SeaY + 1.5f) { float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = new Vector3(q.x, y, q.z); } }
                }
                if (bd < float.MaxValue) return best + (best - new Vector3(p.x, best.y, p.z)).normalized * 8f;   // a few metres onto the beach
            }
            return p;
        }

        void Land()
        {
            bool fromSky = S == Stage.Freefall || S == Stage.Chute;
            // safety net: a touchdown in the water is moved onto the closest beach
            if (fromSky && transform.position.y < SeaY - 0.3f)
            {
                var land = NearestLand(transform.position); var ter = Terrain.activeTerrain;
                if (ter) land.y = ter.SampleHeight(land) + ter.transform.position.y;
                if (Physics.Raycast(land + Vector3.up * 50f, Vector3.down, out var lh, 100f, ~0, QueryTriggerInteraction.Ignore)) land = lh.point;
                transform.position = land + Vector3.up * 0.05f;
            }
            S = Stage.Landed;
            if (fromSky) KryvonFX.LandingDust(transform.position);    // boots hit the ground: a ring of dust
            foreach (var r in hidden) if (r) r.enabled = true; hidden.Clear();
            if (chute) { chute.transform.SetParent(null, true); var c = chute; Destroy(c, 0.05f); }
            if (wind) Destroy(wind);
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            if (heroModel) { heroModel.localRotation = baseRot; heroModel.localPosition = basePos; } tilt = 0;
            if (cc) cc.enabled = true; if (body) body.enabled = true; if (weapons) weapons.enabled = true; Toggle(true);
            if (rig) { rig.Mode = CameraMode.OnFoot; rig.DistanceOverride = -1f; }
            if (fromSky) KryvonWallet.Add("landings");
            // land on your feet: whatever stance you had in the waiting area (prone, crouched) does not come back with you
            if (body) { body.TrySetStance(Stance.Stand); StartCoroutine(StandAfterLanding()); }
            if (!fromSky) { var zone = FindAnyObjectByType<SafeZone>(); if (zone) zone.Begin(); }
        }

        // ---------------- HUD ----------------
        GUIStyle big, small; Texture2D px;
        void OnGUI()
        {
            if (S == Stage.Landed || KryvonTrailer.HideHud) return;
            if (big == null)
            {
                px = Texture2D.whiteTexture;
                big = new GUIStyle(GUI.skin.label) { font = font, fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; big.normal.textColor = Color.white;
                small = new GUIStyle(big) { fontSize = 20, fontStyle = FontStyle.Normal };
            }
            float W = Screen.width, H = Screen.height, k = H / 1080f;
            var m = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1)); W /= k; H = 1080;
            string t = Mathf.FloorToInt(Timer / 60) + ":" + Mathf.FloorToInt(Timer % 60).ToString("00");
            string head = S switch
            {
                Stage.Waiting => $"WAITING AREA  ·  PLANE TAKES OFF IN {t}",
                Stage.Plane => "PRESS  F  TO JUMP",
                Stage.Freefall => $"{Height:0} m   ·   F  OPEN PARACHUTE",
                Stage.Chute when Height > AutoOpenAt + 40f => $"{Height:0} m   ·   F  CLOSE PARACHUTE",
                _ => $"{Height:0} m",
            };
            string sub = S switch
            {
                Stage.Waiting => "Walk around the base — the jet will pick you up",
                Stage.Plane => $"The jet crosses the map — leaves the map in {t}",
                Stage.Freefall => "W dive faster  ·  A / D steer  ·  mouse to look  ·  opens by itself at 100 m",
                _ => "W glide forward  ·  S slow down  ·  A / D turn",
            };
            var r = new Rect(W / 2 - 420, 120, 840, 54);
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.72f); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 92), px); GUI.color = new Color(1f, 0.78f, 0.25f); GUI.DrawTexture(new Rect(r.x, r.y, 6, 92), px); GUI.color = Color.white;
            GUI.Label(r, head, big); GUI.color = new Color(1, 1, 1, 0.7f); GUI.Label(new Rect(r.x, r.y + 52, r.width, 34), sub, small); GUI.color = Color.white;
            // the map with the flight path and place names is KryvonMinimap (tap the minimap or press M)
            GUI.matrix = m;
        }

        // top-down map with the flight line, the jet and the regions
        void DrawMap(float W, float H)
        {
            float size = 360, x0 = W - size - 40, y0 = 200;
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.8f); GUI.DrawTexture(new Rect(x0 - 8, y0 - 8, size + 16, size + 16), px);
            GUI.color = new Color(0.42f, 0.4f, 0.3f, 0.9f); GUI.DrawTexture(new Rect(x0, y0, size, size), px);
            Vector2 P(Vector3 w) => new(x0 + (w.x / (Mathf.Max(HX, HZ) * 2) + 0.5f) * size, y0 + (0.5f - w.z / (Mathf.Max(HX, HZ) * 2)) * size);
            string[] names = { "DAMASCUS QUARTER", "PETRA", "JERASH", "AMMAN TOWNS", "OUTSKIRTS" };
            for (int i = 0; i < 5; i++)
            {
                var sp = GameObject.Find("Spawn_" + i); if (!sp) continue; var q = P(sp.transform.position);
                GUI.color = i == PlayerPrefs.GetInt("kryvon.region", 0) ? new Color(1f, 0.78f, 0.25f) : new Color(1, 1, 1, 0.8f);
                GUI.DrawTexture(new Rect(q.x - 4, q.y - 4, 8, 8), px); GUI.Label(new Rect(q.x - 90, q.y + 2, 180, 22), names[i], new GUIStyle(small) { fontSize = 13 });
            }
            // flight line (dotted) and the jet
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            for (float s = 0; s <= 1; s += 0.012f) { var q = P(Vector3.Lerp(pathA, pathB, s)); if (q.x > x0 && q.x < x0 + size && q.y > y0 && q.y < y0 + size) GUI.DrawTexture(new Rect(q.x - 1.5f, q.y - 1.5f, 3, 3), px); }
            if (S == Stage.Plane) { var j = P(Vector3.Lerp(pathA, pathB, pathT)); GUI.color = new Color(1f, 0.3f, 0.2f); GUI.DrawTexture(new Rect(Mathf.Clamp(j.x, x0, x0 + size) - 6, Mathf.Clamp(j.y, y0, y0 + size) - 6, 12, 12), px); }
            GUI.color = Color.white;
            GUI.Label(new Rect(x0, y0 + size + 6, size, 24), "FLIGHT PATH", new GUIStyle(small) { fontSize = 15 });
        }
    }
}

// In-match minimap (top-left, north up, ~320 m across) and a full map on tap / M: the real top-down picture of the
// map baked at build time (Resources/UI/minimap.png), the player arrow turning with the camera, the safe zone (white)
// and the next zone (dashed gold), teammates in green. Hidden while waiting / in the plane (the drop has its own map).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kryvon
{
    public class KryvonMinimap : MonoBehaviour
    {
        const float Span = 320f;          // metres shown across the minimap
        Texture2D map; KryvonDrop drop; SafeZone zone; Transform player; Camera cam;
        RectTransform root, mini, full; RawImage miniImg, fullImg; RectTransform miniArrow, fullArrow;
        RectTransform miniZone, miniNext, fullZone, fullNext; readonly List<RectTransform> miniMates = new(), fullMates = new();
        float hx, hz; Sprite ring, dot, arrow;
        readonly List<RectTransform> pathDots = new(), miniPath = new(); RectTransform jet, miniJet;
        // sound radar: gunfire (red, heard up to 150 m) and enemy footsteps (orange, up to 40 m) flash on the minimap for 2 s
        static KryvonMinimap inst; KryvonRoutePlan plan;
        readonly List<(Vector3 at, Color c, float t0, RectTransform r)> pings = new();
        readonly Dictionary<KryvonBot, Vector3> lastPos = new(); float nextSteps; Text grid;
        // run-to-zone guide: dotted line on the minimap from you to the nearest edge of the next safe circle, and the distance
        readonly List<RectTransform> zoneDots = new(); Text zoneText;
        float nextScan;
        /// recon class: every 20 s the enemies within 70 m flash on the minimap (purple)
        void ReconScan()
        {
            if (!KryvonClass.Is(OperatorClass.Recon) || Time.time < nextScan || (drop && drop.S != KryvonDrop.Stage.Landed)) return;
            nextScan = Time.time + 20f; int n = 0;
            foreach (var b in KryvonBot.All)
                if (b && !b.Mate && b.TryGetComponent<Health>(out var bh) && bh.IsAlive && (b.transform.position - player.position).sqrMagnitude < 70f * 70f)
                { var r = Img(mini, dot, new Color(0.75f, 0.45f, 1f)); r.sizeDelta = new Vector2(18, 18); pings.Add((b.transform.position, new Color(0.75f, 0.45f, 1f), Time.time, r)); n++; }
            if (n > 0) KryvonKillFeed.Notice($"RECON SCAN  ·  {n} ENEM{(n == 1 ? "Y" : "IES")} NEARBY");
        }
        public static void Gunshot(Vector3 at) { if (inst) inst.Ping(at, new Color(1f, 0.22f, 0.18f), 150f); }
        void Ping(Vector3 at, Color c, float range)
        {
            if (!player || (at - player.position).sqrMagnitude > range * range || (at - player.position).sqrMagnitude < 4f) return;
            foreach (var p in pings) if ((p.at - at).sqrMagnitude < 25f && Time.time - p.t0 < 0.6f) return;     // one mark per burst
            var r = Img(mini, dot, c); r.sizeDelta = new Vector2(16, 16); pings.Add((at, c, Time.time, r));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Hook(); SceneManager.sceneLoaded += (_, _) => Hook(); }
        static void Hook()
        {
            if (FindAnyObjectByType<KryvonMinimap>()) return;
            var pc = FindAnyObjectByType<PlayerController>(); if (!pc) return;
            pc.gameObject.AddComponent<KryvonMinimap>();
        }

        void Start()
        {
            map = Resources.Load<Texture2D>("UI/minimap"); if (!map) { enabled = false; return; }
            inst = this;
            player = transform; drop = GetComponent<KryvonDrop>(); zone = FindAnyObjectByType<SafeZone>();
            hx = drop ? drop.HX : 1280; hz = drop ? drop.HZ : 698;
            ring = Ring(); dot = Dot(); arrow = Arrow();
            var cg = new GameObject("MinimapUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // above the touch controls (50): their movement pad covers the top-left corner and swallowed the tap
            var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 60;
            if (!FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
            root = (RectTransform)cg.transform;

            // minimap
            // round minimap: a dark disc with a thin silver ring; inside, a circular mask whose own colour is the open sea,
            // so wherever the map ends you see water, never stretched edge pixels or a square corner
            var disc = Disc();
            var frame = Box(root, "Minimap", new Color(0.03f, 0.04f, 0.05f, 0.9f)); frame.GetComponent<Image>().sprite = disc;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0, 1); frame.anchoredPosition = new Vector2(24, -24); frame.sizeDelta = new Vector2(270, 270);
            mini = Box(frame, "View", Sea); mini.anchorMin = Vector2.zero; mini.anchorMax = Vector2.one; mini.offsetMin = Vector2.one * 5; mini.offsetMax = -Vector2.one * 5;
            mini.GetComponent<Image>().sprite = disc; mini.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var rim = Img(frame, ring, new Color(0.78f, 0.82f, 0.88f, 0.95f)); rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one; rim.offsetMin = rim.offsetMax = Vector2.zero; rim.SetAsLastSibling();
            miniImg = Raw(mini); miniZone = Img(mini, ring, Color.white); miniNext = Img(mini, ring, new Color(1f, 0.78f, 0.3f, 0.9f));
            miniArrow = Img(mini, arrow, new Color(1f, 0.85f, 0.35f)); miniArrow.sizeDelta = new Vector2(26, 26);
            var n = Txt(frame, "N", 20); n.rectTransform.anchorMin = n.rectTransform.anchorMax = new Vector2(0.5f, 1); n.rectTransform.anchoredPosition = new Vector2(0, -16);
            grid = Txt(frame, "", 17); grid.rectTransform.anchorMin = grid.rectTransform.anchorMax = new Vector2(0.5f, 0); grid.rectTransform.pivot = new Vector2(0.5f, 1); grid.rectTransform.anchoredPosition = new Vector2(0, -4); grid.rectTransform.sizeDelta = new Vector2(270, 24);
            grid.color = new Color(0.86f, 0.86f, 0.86f);
            zoneText = Txt(frame, "", 18); zoneText.rectTransform.anchorMin = zoneText.rectTransform.anchorMax = new Vector2(0.5f, 0); zoneText.rectTransform.pivot = new Vector2(0.5f, 1); zoneText.rectTransform.anchoredPosition = new Vector2(0, -28); zoneText.rectTransform.sizeDelta = new Vector2(300, 24);
            var tap = frame.gameObject.AddComponent<Button>(); tap.onClick.AddListener(() => full.gameObject.SetActive(true)); frame.GetComponent<Image>().raycastTarget = true;

            // full map
            full = Box(root, "FullMap", new Color(0.03f, 0.11f, 0.17f, 0.94f));   // open sea all around the island, covering the whole screen
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            full.GetComponent<Image>().raycastTarget = true; full.gameObject.AddComponent<Button>().onClick.AddListener(() => full.gameObject.SetActive(false));
            var area = Box(full, "Area", Color.white); area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f); area.sizeDelta = new Vector2(1000 * hx / hz, 1000); Destroy(area.GetComponent<Image>());
            fullImg = Raw(area); fullImg.texture = map; fullImg.rectTransform.anchorMin = Vector2.zero; fullImg.rectTransform.anchorMax = Vector2.one; fullImg.rectTransform.offsetMin = fullImg.rectTransform.offsetMax = Vector2.zero;
            fullZone = Img(area, ring, Color.white); fullNext = Img(area, ring, new Color(1f, 0.78f, 0.3f, 0.9f));
            fullArrow = Img(area, arrow, new Color(1f, 0.85f, 0.35f)); fullArrow.sizeDelta = new Vector2(34, 34);
            var hint = Txt(full, "X  ·  TAP THE SEA  ·  M  TO CLOSE", 22); hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0); hint.rectTransform.anchoredPosition = new Vector2(0, 30);
            // place names (baked from the map at build time) and the flight path (dots + the jet) on the full map
            var lbl = Resources.Load<TextAsset>("UI/map_labels"); float kL = 1000f / (2 * hz);
            if (lbl) foreach (var line in lbl.text.Split((char)10))
            {
                var f = line.Trim().Split(';'); if (f.Length < 3 || !float.TryParse(f[1], out var lx) || !float.TryParse(f[2], out var lz)) continue;
                bool city = f.Length > 3 && (f[3].Contains("city") || f[3].Contains("town"));
                var t = Txt(area, f[0].ToUpperInvariant(), city ? 20 : 16); t.color = city ? new Color(1f, 0.9f, 0.6f) : new Color(1, 1, 1, 0.9f);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); t.rectTransform.sizeDelta = new Vector2(260, 26); t.rectTransform.anchoredPosition = new Vector2(lx, lz) * kL + new Vector2(0, 12);
                var dt = Img(area, dot, Color.white); dt.sizeDelta = new Vector2(7, 7); dt.anchoredPosition = new Vector2(lx, lz) * kL;
            }
            for (int i = 0; i < 70; i++) { var d = Img(area, dot, new Color(1f, 1f, 1f, 0.85f)); d.sizeDelta = new Vector2(6, 6); pathDots.Add(d); }
            jet = Img(area, arrow, new Color(1f, 0.3f, 0.2f)); jet.sizeDelta = new Vector2(30, 30);
            for (int i = 0; i < 30; i++) { var d = Img(mini, dot, new Color(1f, 1f, 1f, 0.8f)); d.sizeDelta = new Vector2(4, 4); miniPath.Add(d); }
            miniJet = Img(mini, arrow, new Color(1f, 0.3f, 0.2f)); miniJet.sizeDelta = new Vector2(22, 22);
            // a big X in the top-right corner (the whole sea around the map closes it too)
            var x = Box(full, "Close", new Color(0.75f, 0.16f, 0.14f, 0.95f)); x.anchorMin = x.anchorMax = x.pivot = new Vector2(1, 1);
            x.anchoredPosition = new Vector2(-30, -30); x.sizeDelta = new Vector2(110, 110); x.GetComponent<Image>().sprite = Disc(); x.GetComponent<Image>().raycastTarget = true;
            x.gameObject.AddComponent<Button>().onClick.AddListener(() => full.gameObject.SetActive(false));
            var xt = Txt(x, "X", 56); xt.rectTransform.anchorMin = Vector2.zero; xt.rectTransform.anchorMax = Vector2.one; xt.rectTransform.offsetMin = xt.rectTransform.offsetMax = Vector2.zero; xt.raycastTarget = false;
            x.SetAsLastSibling();
            plan = new KryvonRoutePlan(full, fullImg.rectTransform, mini, hz, dot, disc);   // squad route planning on the full map
            full.gameObject.SetActive(false);
            miniImg.texture = map;
        }

        void Update()
        {
            if (!root) return;
            root.gameObject.SetActive(!KryvonTrailer.HideHud);
            bool flying = drop && (drop.S == KryvonDrop.Stage.Waiting || drop.S == KryvonDrop.Stage.Plane);
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null && kb.mKey.wasPressedThisFrame) full.gameObject.SetActive(!full.gameObject.activeSelf);
            cam ??= Camera.main;
            float yaw = cam ? cam.transform.eulerAngles.y : player.eulerAngles.y;
            var me = flying && drop.S == KryvonDrop.Stage.Plane ? drop.PlanePos : player.position;
            Vector2 uv = UV(me);
            // minimap window around the player
            float uw = Span / (2 * hx), vh = Span / (2 * hz);
            // only the part of the window that lies on the map gets the map picture; the rest stays sea (the mask colour)
            var win = new Rect(uv.x - uw / 2, uv.y - vh / 2, uw, vh);
            float x0 = Mathf.Max(0, win.xMin), x1 = Mathf.Min(1, win.xMax), y0 = Mathf.Max(0, win.yMin), y1 = Mathf.Min(1, win.yMax);
            if (x1 > x0 && y1 > y0)
            {
                miniImg.enabled = true; miniImg.uvRect = new Rect(x0, y0, x1 - x0, y1 - y0);
                var rt = miniImg.rectTransform; rt.anchorMin = new Vector2((x0 - win.xMin) / uw, (y0 - win.yMin) / vh); rt.anchorMax = new Vector2((x1 - win.xMin) / uw, (y1 - win.yMin) / vh); rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else miniImg.enabled = false;
            var ms = mini.rect.size; float k = ms.x / Span;            // UI units per metre
            miniArrow.anchoredPosition = Vector2.zero; miniArrow.localEulerAngles = new Vector3(0, 0, -yaw);
            Circle(miniZone, zone && zone.CircleShown, zone ? zone.Center : default, zone ? zone.Radius : 0, p => (p - Flat(me)) * k);
            Circle(miniNext, zone && zone.CircleShown && zone.NextRadius < zone.Radius - 1, zone ? zone.NextCenter : default, zone ? zone.NextRadius : 0, p => (p - Flat(me)) * k);
            if (full.gameObject.activeSelf)
            {
                var fs = fullImg.rectTransform.rect.size; float kf = fs.y / (2 * hz);
                fullArrow.anchoredPosition = Flat(player.position) * kf; fullArrow.localEulerAngles = new Vector3(0, 0, -yaw);
                Circle(fullZone, zone && zone.CircleShown, zone ? zone.Center : default, zone ? zone.Radius : 0, p => p * kf);
                Circle(fullNext, zone && zone.CircleShown && zone.NextRadius < zone.Radius - 1, zone ? zone.NextCenter : default, zone ? zone.NextRadius : 0, p => p * kf);
            }
            // flight path: shown until you jump
            bool path = flying && (drop.PathA - drop.PathB).sqrMagnitude > 1;
            float kf2 = fullImg.rectTransform.rect.size.y / (2 * hz);
            for (int i = 0; i < pathDots.Count; i++) { pathDots[i].gameObject.SetActive(path); if (path) pathDots[i].anchoredPosition = Flat(Vector3.Lerp(drop.PathA, drop.PathB, i / (pathDots.Count - 1f))) * kf2; }
            jet.gameObject.SetActive(path && drop.S == KryvonDrop.Stage.Plane);
            var dirP = Flat(drop ? drop.PathB - drop.PathA : Vector3.forward); float jyaw = Mathf.Atan2(dirP.x, dirP.y) * Mathf.Rad2Deg;
            if (jet.gameObject.activeSelf) { jet.anchoredPosition = Flat(drop.PlanePos) * kf2; jet.localEulerAngles = new Vector3(0, 0, -jyaw); }
            for (int i = 0; i < miniPath.Count; i++) { miniPath[i].gameObject.SetActive(path); if (path) miniPath[i].anchoredPosition = (Flat(Vector3.Lerp(drop.PathA, drop.PathB, i / (miniPath.Count - 1f))) - Flat(me)) * k; }
            miniJet.gameObject.SetActive(path && drop.S == KryvonDrop.Stage.Plane); miniJet.localEulerAngles = new Vector3(0, 0, -jyaw);
            miniArrow.gameObject.SetActive(!(path && drop.S == KryvonDrop.Stage.Plane)); fullArrow.gameObject.SetActive(!(path && drop.S == KryvonDrop.Stage.Plane));
            Mates(miniMates, mini, p => (p - Flat(me)) * k, 10);
            ZoneGuide(me, k, ms.x * 0.5f - 6f);
            plan?.Update(me, k, ms.x * 0.5f - 8f, full.gameObject.activeSelf);
            ReconScan();
            if (grid) grid.text = $"GRID  X {Mathf.RoundToInt(player.position.x + hx):0000}  ·  Z {Mathf.RoundToInt(player.position.z + hz):0000}";
            // enemy footsteps close by
            if (Time.time >= nextSteps && !flying)
            {
                nextSteps = Time.time + 0.5f;
                foreach (var b in KryvonBot.All)
                {
                    if (!b || b.Mate || !b.TryGetComponent<Health>(out var bh) || !bh.IsAlive) continue;
                    var bp = b.transform.position;
                    if (lastPos.TryGetValue(b, out var lp) && (bp - lp).sqrMagnitude > 0.5f && Mathf.Abs(bp.y - lp.y) < 3f) Ping(bp, new Color(1f, 0.6f, 0.15f), 40f);
                    lastPos[b] = bp;
                }
            }
            float edge = ms.x * 0.5f - 9f;
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                var (at, c, t0, r) = pings[i]; float age = Time.time - t0;
                if (age > 2f || !r) { if (r) Destroy(r.gameObject); pings.RemoveAt(i); continue; }
                var pos = (Flat(at) - Flat(me)) * k; if (pos.magnitude > edge) pos = pos.normalized * edge;    // off the minimap: stuck to its edge, pointing the way
                r.anchoredPosition = pos; float pulse = 1f + 0.35f * Mathf.Sin(age * 14f);
                r.sizeDelta = Vector2.one * 16f * pulse; r.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 1f - age / 2f);
            }
            if (full.gameObject.activeSelf) Mates(fullMates, fullImg.rectTransform, p => p * (fullImg.rectTransform.rect.size.y / (2 * hz)), 14);
        }

        void ZoneGuide(Vector3 me, float k, float edge)
        {
            bool on = zone && zone.CircleShown && !(drop && drop.S != KryvonDrop.Stage.Landed && drop.S != KryvonDrop.Stage.Chute);
            Vector2 c = on ? Flat(zone.NextCenter) : default; float rad = on ? zone.NextRadius : 0;
            float dist = on ? (Flat(me) - c).magnitude - rad : 0;
            bool outside = on && dist > 0.5f;
            if (zoneText)
            {
                bool hurting = on && !zone.IsInside(me);
                zoneText.text = outside ? $"SAFE ZONE  ▸  {Mathf.CeilToInt(dist)} m" : on ? "INSIDE THE SAFE ZONE" : "";
                zoneText.color = hurting ? new Color(0.95f, 0.35f, 0.3f) : outside ? new Color(1f, 0.82f, 0.4f) : new Color(0.6f, 0.9f, 0.65f);
            }
            const int N = 12;
            while (zoneDots.Count < N) { var r = Img(mini, dot, Color.white); r.sizeDelta = new Vector2(6, 6); zoneDots.Add(r); }
            if (!outside) { foreach (var z in zoneDots) z.gameObject.SetActive(false); return; }
            var target = c + (Flat(me) - c).normalized * rad;                   // nearest point on the safe circle
            var to = (target - Flat(me)) * k; float len = Mathf.Min(to.magnitude, edge);
            var dir = to.normalized;
            for (int i = 0; i < N; i++)
            {
                float at = (i + 1) * len / N; var z = zoneDots[i];
                z.gameObject.SetActive(true); z.anchoredPosition = dir * at;
                z.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f + 0.65f * Mathf.PingPong(Time.time * 1.5f - i * 0.12f, 1f));
            }
        }

        Vector2 UV(Vector3 w) => new(w.x / (2 * hx) + 0.5f, w.z / (2 * hz) + 0.5f);
        static Vector2 Flat(Vector3 w) => new(w.x, w.z);
        void Circle(RectTransform r, bool on, Vector3 c, float radius, System.Func<Vector2, Vector2> place)
        {
            r.gameObject.SetActive(on && radius > 1); if (!on) return;
            var p0 = place(Flat(c)); var p1 = place(Flat(c) + new Vector2(radius, 0));
            r.anchoredPosition = p0; float d = (p1 - p0).magnitude * 2; r.sizeDelta = new Vector2(d, d);
        }
        void Mates(List<RectTransform> pool, RectTransform parent, System.Func<Vector2, Vector2> place, float size)
        {
            int i = 0;
            foreach (var b in KryvonBot.All)
            {
                if (!b || !b.Mate || !b.TryGetComponent<Health>(out var bh) || !bh.IsAlive) continue;
                if (i >= pool.Count) { var r = Img(parent, dot, new Color(0.35f, 0.95f, 0.45f)); r.sizeDelta = new Vector2(size, size); pool.Add(r); }
                pool[i].gameObject.SetActive(true); pool[i].anchoredPosition = place(Flat(b.transform.position)); i++;
            }
            for (; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }

        // ---------- tiny UI builders ----------
        static RectTransform Box(Transform parent, string n, Color c)
        {
            var g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
            var i = g.GetComponent<Image>(); i.color = c; i.raycastTarget = false; return (RectTransform)g.transform;
        }
        static RawImage Raw(RectTransform parent)
        {
            var g = new GameObject("Map", typeof(RectTransform), typeof(RawImage)); g.transform.SetParent(parent, false);
            var r = g.GetComponent<RawImage>(); r.raycastTarget = false; var t = r.rectTransform; t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one; t.offsetMin = t.offsetMax = Vector2.zero; return r;
        }
        static RectTransform Img(RectTransform parent, Sprite s, Color c)
        {
            var g = new GameObject("Mark", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
            var i = g.GetComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false;
            var r = (RectTransform)g.transform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(12, 12); return r;
        }
        Text Txt(RectTransform parent, string s, int size)
        {
            var g = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow)); g.transform.SetParent(parent, false);
            var t = g.GetComponent<Text>(); t.text = s; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontStyle = FontStyle.Bold; t.rectTransform.sizeDelta = new Vector2(600, 30); return t;
        }
        static Sprite Ring()
        {
            int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)); float a = Mathf.Clamp01(1.5f - Mathf.Abs(d - (S / 2f - 3))); t.SetPixel(x, y, new Color(1, 1, 1, a)); }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        static readonly Color Sea = new(0.07f, 0.23f, 0.34f, 1f);
        static Sprite Disc()
        {   // a smooth filled circle, big enough for a clean mask edge
            int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)); t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(S / 2f - 1 - d))); }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        static Sprite Dot()
        {
            int S = 32; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)); t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(S / 2f - 1 - d))); }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        static Sprite Arrow()
        {
            int S = 64; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                // a chevron pointing up (+y = north = the camera's forward after rotation)
                float u = (x - S / 2f) / (S / 2f), v = (y - S / 2f) / (S / 2f);
                bool inside = v < 0.9f - Mathf.Abs(u) * 1.6f && v > -0.8f + Mathf.Abs(u) * 0.6f;
                t.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
    }
}

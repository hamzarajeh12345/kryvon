// Arcade vehicle: F to get in/out, WASD to drive, Space handbrake, Shift boost. Follows the ground, stops on walls.
// The model faces +Z. Uses no Rigidbody so it never tips over or jitters.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class KryvonVehicle : MonoBehaviour, IDamageable
    {
        public static readonly List<KryvonVehicle> All = new();
        public string displayName = "Car";
        [SerializeField] float maxSpeed = 30f, accel = 9f, brake = 22f, reverse = 8f, turnRate = 70f, length = 4.5f, width = 1.9f;
        [SerializeField] Vector3 seat = new(-0.4f, 0.55f, 0.2f);
        [SerializeField] bool lean;                 // motorcycles lean into turns
        [SerializeField] AudioClip engine, crash;
        [SerializeField] float maxHp = 1000f;
        [SerializeField, Range(0, 0.95f)] float bulletResist = 0f;   // Jawad: 0.8
        public float Hp { get; private set; } = -1; public bool Wrecked { get; private set; }
        public bool TyresBlown { get; private set; }
        public float Speed { get; private set; }
        readonly System.Collections.Generic.Dictionary<Health, float> lastHit = new();
        public GameObject Driver { get; private set; }
        AudioSource eng; float yaw, roll; Collider[] own; readonly RaycastHit[] hits = new RaycastHit[8];

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Awake()
        {
            // realistic pacing: vehicles 35% slower than the first tuning (a pickup tops out ~56 km/h off-road)
            maxSpeed *= 0.5f; accel *= 0.6f; reverse *= 0.6f;   // car ≈ 54 km/h top: a map crossing takes ~3 min
            yaw = transform.eulerAngles.y; own = GetComponentsInChildren<Collider>(); Hp = maxHp;
            if (engine) { eng = gameObject.AddComponent<AudioSource>(); eng.clip = engine; eng.loop = true; eng.spatialBlend = 1f; eng.volume = 0f; eng.minDistance = 4; eng.maxDistance = 80; eng.Play(); }
        }

        bool IsOwn(Collider c) { foreach (var o in own) if (o == c) return true; return Driver && c.transform.IsChildOf(Driver.transform); }

        public void TakeDamage(DamageInfo info)
        {
            if (Wrecked) return;
            Hp -= info.Amount * (1f - bulletResist) * (Driver && Driver.CompareTag("Player") ? KryvonClass.VehicleDamageMul : 1f);   // engineer class
            if (Hp <= 0) Explode(info);
        }
        void Explode(DamageInfo info)
        {
            Wrecked = true; Speed = 0;
            var p = transform.position + Vector3.up;
            if (engineFire) Destroy(engineFire);
            KryvonFX.Explosion(p, 1.7f); CameraShake.Kick(0.8f);
            if (crash) AudioSource.PlayClipAtPoint(crash, p, 1f);
            KryvonFX.GroundFire(p + Vector3.up * 0.2f, 1.3f, 30f, transform);      // the wreck keeps burning
            var d = Driver; Exit();
            if (d) { var h = d.GetComponent<Health>(); if (h) h.TakeDamage(new DamageInfo { Amount = 60, Source = info.Source, Cause = displayName + " explosion", IgnoreArmor = true }); }
            foreach (var h in FindObjectsByType<Health>()) if (h.IsAlive && (h.transform.position - p).sqrMagnitude < 36f) h.TakeDamage(new DamageInfo { Amount = 70, Source = info.Source, Cause = displayName + " explosion" });
            foreach (var r in GetComponentsInChildren<Renderer>()) { if (r is ParticleSystemRenderer) continue; r.SetPropertyBlock(null); foreach (var m in r.materials) { foreach (var cp in new[] { "_BaseColor", "baseColorFactor" }) if (m.HasProperty(cp)) m.SetColor(cp, new Color(0.12f, 0.1f, 0.09f)); foreach (var tp in new[] { "_BaseMap", "baseColorTexture" }) if (m.HasProperty(tp)) m.SetTexture(tp, null); } }   // burnt black
            if (eng) eng.Stop();
        }

        public void Enter(GameObject player)
        {
            if (Driver || Wrecked) return;
            // the player's vehicle skin from the store goes on the car they drive (before they are parented into it)
            if (!player.GetComponent<KryvonBot>()) { var cos = KryvonSkins.Equipped(CosKind.Vehicle); KryvonSkins.Apply(gameObject, cos != null && cos.tex != null ? cos : null); }
            Driver = player;
            foreach (var b in new Behaviour[] { player.GetComponent<PlayerController>(), player.GetComponent<WeaponController>() }) if (b) b.enabled = false;
            var cc0 = player.GetComponent<CharacterController>(); if (cc0) cc0.enabled = false;
            player.transform.SetParent(transform, true);
            player.transform.localPosition = seat; player.transform.localRotation = Quaternion.identity;
            SetDriving(player, true);
        }
        public void Exit()
        {
            if (!Driver) return;
            var p = Driver; Driver = null;
            p.transform.SetParent(null, true);
            var side = transform.position - transform.right * (width / 2 + 1.0f);
            if (Physics.Raycast(side + Vector3.up * 3, Vector3.down, out var g, 8f, ~0, QueryTriggerInteraction.Ignore)) side.y = g.point.y + 0.05f;
            p.transform.SetPositionAndRotation(side, Quaternion.Euler(0, transform.eulerAngles.y, 0));
            var cc1 = p.GetComponent<CharacterController>(); if (cc1) cc1.enabled = true;
            foreach (var b in new Behaviour[] { p.GetComponent<PlayerController>(), p.GetComponent<WeaponController>() }) if (b) b.enabled = true;
            SetDriving(p, false);
            // step out standing with the gun up – never prone or crouched (stance taps made while driving are ignored)
            var pc = p.GetComponent<PlayerController>(); if (pc) { pc.TrySetStance(Stance.Stand); StartCoroutine(StandNextFrames(pc)); }
            // jumping out of a moving car hurts: nothing up to 18 km/h, then more and more, dead from ~50 km/h
            float s = Mathf.Abs(Speed);
            if (s > 5f)
            {
                var hp = p.GetComponent<Health>();
                if (hp) hp.TakeDamage(new DamageInfo { Amount = s >= 14f ? 999f : (s - 5f) * 11f, Cause = "Jumped out of a moving " + displayName, IgnoreArmor = true });
                if (crash) AudioSource.PlayClipAtPoint(crash, side, Mathf.Clamp01(s / 20f));
                KryvonFX.Dust(side);
            }
        }
        System.Collections.IEnumerator StandNextFrames(PlayerController pc)
        {
            for (int i = 0; i < 6; i++) { yield return null; if (pc && pc.Stance != Stance.Stand) pc.TrySetStance(Stance.Stand); }
        }
        static void SetDriving(GameObject p, bool on)
        {
            var an = p.GetComponentInChildren<Animator>(); if (!an) return;
            foreach (var par in an.parameters) if (par.name == "Driving" && par.type == AnimatorControllerParameterType.Bool) an.SetBool("Driving", on);
        }

        float nextSmoke; GameObject engineFire;
        void Damaged()
        {
            // a hurt car smokes from the engine; badly hurt it catches fire
            if (Hp < 0 || Time.time < nextSmoke) return;
            var engineAt = transform.position + transform.forward * (length * 0.32f) + Vector3.up * 1.1f;
            if (Hp < maxHp * 0.4f) { nextSmoke = Time.time + (Hp < maxHp * 0.2f ? 0.18f : 0.35f); KryvonFX.Smoke(engineAt); }
            if (Hp < maxHp * 0.15f && !engineFire) engineFire = KryvonFX.GroundFire(engineAt - Vector3.up * 0.2f, 0.35f, 0f, transform);
        }

        void Update()
        {
            if (Wrecked) return;
            Damaged();
            if (Driver && Driver.CompareTag("Player") && KryvonClass.Is(OperatorClass.Engineer) && Hp < maxHp) Hp = Mathf.Min(maxHp, Hp + 6f * Time.deltaTime);   // engineer: field repairs
            float throttle = 0, steer = 0; bool hand = false, boost = false;
            if (Driver)
            {
                var input = Driver.GetComponent<PlayerInputReader>();
                var mv = input ? input.Move : Vector2.zero;
                throttle = mv.y; steer = mv.x;
                var kb = Keyboard.current; hand = kb != null && kb.spaceKey.isPressed; boost = input && input.SprintHeld;
            }
            if (!TyresBlown) foreach (var sp in KryvonSpikeStrip.All) if (sp.Area.Contains(new Vector3(transform.position.x, sp.Area.center.y, transform.position.z)) && Mathf.Abs(Speed) > 2f) { TyresBlown = true; if (crash) AudioSource.PlayClipAtPoint(crash, transform.position, 0.8f); KryvonFX.Dust(transform.position); }
            if (TyresBlown) steer += Mathf.Sin(Time.time * 9f) * 0.35f * Mathf.Clamp01(Mathf.Abs(Speed) / 10f);   // wobble on flat tyres
            float top = maxSpeed * (boost ? 1.1f : 1f) * (TyresBlown ? 0.35f : 1f), dt = Time.deltaTime;
            if (throttle > 0) Speed = Speed < 0 ? Speed + brake * dt : Mathf.Min(top, Speed + accel * throttle * dt * (boost ? 1.15f : 1f) * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(Speed / Mathf.Max(1f, top))));
            else if (throttle < 0) Speed = Speed > 0 ? Speed - brake * dt : Mathf.Max(-reverse, Speed + accel * throttle * dt);
            else Speed = Mathf.MoveTowards(Speed, 0, (Driver ? 3f : 12f) * dt);
            if (hand) Speed = Mathf.MoveTowards(Speed, 0, brake * 1.4f * dt);
            float grip = Mathf.Clamp01(Mathf.Abs(Speed) / 6f);
            yaw += steer * turnRate * grip * Mathf.Sign(Speed) * dt * (hand ? 1.5f : 1f);

            // walls and other vehicles stop us
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward; float step = Speed * dt;
            if (Mathf.Abs(step) > 0.0001f)
            {
                var dir = fwd * Mathf.Sign(step);
                int n = Physics.BoxCastNonAlloc(transform.position + Vector3.up * 0.9f, new Vector3(width / 2, 0.45f, 0.2f), dir, hits, Quaternion.Euler(0, yaw, 0), length / 2 + Mathf.Abs(step), ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (IsOwn(hits[i].collider) || hits[i].collider is TerrainCollider) continue;
                    var h = hits[i].collider.GetComponentInParent<Health>();
                    // a person in the way is run over, not a wall: the car keeps going (a little slower) and the person is
                    // hurt by the speed – a knock at walking pace, dead from ~40 km/h. Each person is hit once per pass.
                    if (h && !h.GetComponentInParent<KryvonVehicle>())
                    {
                        float s = Mathf.Abs(Speed);
                        if (h.IsAlive && s > 2.5f &&(!lastHit.TryGetValue(h, out var when) || Time.time - when > 1.2f))
                        {
                            lastHit[h] = Time.time;
                            h.TakeDamage(new DamageInfo { Amount = s >= 11f ? 999f : (s - 2.5f) * 12f, Source = Driver, Cause = "Run over by " + displayName, IgnoreArmor = s >= 11f });
                            if (crash) AudioSource.PlayClipAtPoint(crash, hits[i].point, Mathf.Clamp01(s / 18f));
                            var vcc = h.GetComponent<CharacterController>();     // thrown aside
                            if (vcc && vcc.enabled) vcc.Move((dir + Vector3.Cross(Vector3.up, dir) * (Vector3.Dot(h.transform.position - transform.position, Vector3.Cross(Vector3.up, dir)) >= 0 ? 1 : -1)).normalized * Mathf.Min(2.5f, s * 0.15f));
                            Speed *= 0.85f;
                        }
                        continue;
                    }
                    if (h && Mathf.Abs(Speed) > 8f) h.TakeDamage(new DamageInfo { Amount = Mathf.Abs(Speed) * 4f, Source = Driver, Cause = displayName });
                    if (crash && Mathf.Abs(Speed) > 6f) AudioSource.PlayClipAtPoint(crash, hits[i].point, Mathf.Clamp01(Mathf.Abs(Speed) / 25f));
                    // hitting something solid hurts the car (and, hard enough, the driver): ~70 km/h into a wall wrecks most cars
                    float cs = Mathf.Abs(Speed);
                    if (cs > 8f)
                    {
                        KryvonFX.Sparks(hits[i].point, hits[i].normal, (int)(cs * 1.5f)); KryvonFX.Dust(hits[i].point);
                        if (Driver) CameraShake.Kick(Mathf.Clamp01(cs / 25f));
                        var drv = Driver ? Driver.GetComponent<Health>() : null;
                        if (drv && cs > 15f) drv.TakeDamage(new DamageInfo { Amount = (cs - 15f) * 9f, Cause = "Crashed the " + displayName, IgnoreArmor = true });
                        TakeDamage(new DamageInfo { Amount = (cs - 8f) * (cs - 8f) * 6f / Mathf.Max(0.2f, 1f - bulletResist), Source = Driver, Cause = "Crash" });
                        if (Wrecked) return;
                    }
                    Speed = -Speed * 0.25f; step = 0; break;
                }
            }
            var pos = transform.position + fwd * step;
            // follow the ground: sample front and back for pitch, left and right for roll
            // the terrain height is the floor we can never sink below (a steep climb used to leave the car inside the hill)
            var ter = Terrain.activeTerrain;
            float TerrainY(Vector3 p) => ter ? ter.SampleHeight(p) + ter.transform.position.y : float.MinValue;
            float GroundY(Vector3 p)
            {
                int n = Physics.RaycastNonAlloc(p + Vector3.up * 6f, Vector3.down, hits, 16f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MinValue;
                for (int i = 0; i < n; i++) if (!IsOwn(hits[i].collider) && !(hits[i].collider is TerrainCollider) && hits[i].point.y < p.y + 2.5f && hits[i].point.y > best) best = hits[i].point.y;
                best = Mathf.Max(best, TerrainY(p));
                return best == float.MinValue ? p.y : best;
            }
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            float yf = GroundY(pos + fwd * length * 0.4f), yb = GroundY(pos - fwd * length * 0.4f), yl = GroundY(pos - right * width * 0.4f), yr = GroundY(pos + right * width * 0.4f);
            pos.y = Mathf.Lerp(transform.position.y, (yf + yb + yl + yr) / 4f, 1f - Mathf.Exp(-12f * dt));
            pos.y = Mathf.Max(pos.y, TerrainY(pos) - 0.05f);
            float pitch = -Mathf.Atan2(yf - yb, length * 0.8f) * Mathf.Rad2Deg;
            // gravity along the slope: hills slow you down going up and speed you up going down
            float slope = (yf - yb) / (length * 0.8f) * Mathf.Sign(Speed == 0 ? 1 : Speed);
            if (Driver && Mathf.Abs(Speed) > 0.5f) Speed -= Mathf.Sign(Speed) * Mathf.Clamp(slope, -0.4f, 0.6f) * 9.81f * 0.55f * dt;
            float rollT = lean ? -steer * 22f * grip : Mathf.Atan2(yl - yr, width * 0.8f) * Mathf.Rad2Deg;
            roll = Mathf.Lerp(roll, rollT, 1f - Mathf.Exp(-6f * dt));
            transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, roll));

            if (eng) { eng.volume = Driver ? 0.35f + 0.4f * Mathf.Abs(Speed) / maxSpeed : 0f; eng.pitch = 0.7f + 1.1f * Mathf.Abs(Speed) / maxSpeed; }
        }
    }
}

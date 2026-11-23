// Throwables and deployables.
//   T  cycle: Frag · Molotov · Smoke · Flashbang · Flare gun
//   G  hold to pull the pin (frag cooks, up to 5 s – it blows in your hand at 5), release to throw along the shown arc
//   Y  lay a spike strip · U  plant a deployable shield
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public enum Throwable { Frag, Molotov, Smoke, Flashbang, Flare }

    public class KryvonThrowables : MonoBehaviour
    {
        public int[] counts = new int[5];
        public int spikes, shields;
        public Throwable Selected { get; private set; }
        public float Cook { get; private set; } = -1;
        float lastThrowTap = -9;      // seconds since the pin was pulled, -1 = not holding
        public static readonly string[] Names = { "Frag", "Molotov", "Smoke", "Flashbang", "Flare Gun" };

        [SerializeField] GameObject[] templates = new GameObject[5];      // disabled model per throwable
        [SerializeField] GameObject spikeTemplate, shieldTemplate;
        [SerializeField] AudioClip pinSound, throwSound, boomSound, glassSound, hissSound, bangSound, ringSound, fireLoop;
        KryvonGear gear; KryvonAirdrop drop; LineRenderer arc; Health self;

        void Awake()
        {
            gear = GetComponent<KryvonGear>(); drop = GetComponent<KryvonAirdrop>(); self = GetComponent<Health>();
            // battle royale: everyone lands with nothing – throwables and deployables only come from loot
            for (int i = 0; i < counts.Length; i++) counts[i] = 0; spikes = 0; shields = 0;
            var g = new GameObject("ThrowArc"); g.transform.SetParent(transform, false);
            arc = g.AddComponent<LineRenderer>(); arc.positionCount = 30; arc.widthMultiplier = 0.04f; arc.enabled = false;
            arc.material = new Material(Resources.Load<Material>("KryvonUnlit")); arc.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f));
        }
        // touch hooks
        bool tCycle, tDown, tUp, tSpike, tShield;
        public void TouchCycle() => tCycle = true; public void TouchThrowDown() => tDown = true; public void TouchThrowUp() => tUp = true;
        public void TouchSpike() => tSpike = true; public void TouchShield() => tShield = true;
        static bool Consume(ref bool f) { bool v = f; f = false; return v; }
        public void Select(Throwable t) { if (Count(t) > 0 && Cook < 0) Selected = t; }
        public int Count(Throwable t) => t == Throwable.Flare ? (gear ? gear.flares : 0) : counts[(int)t];
        void Spend(Throwable t) { if (t == Throwable.Flare) gear.flares--; else counts[(int)t]--; }

        Vector3 Origin => transform.position + Vector3.up * 1.6f + transform.right * 0.25f;
        Vector3 Velocity()
        {
            var cam = Camera.main ? Camera.main.transform : transform;
            var dir = (cam.forward + Vector3.up * 0.18f).normalized;
            return dir * (Selected == Throwable.Flare ? 0f : 17f);
        }

        void Update()
        {
            var kb = Keyboard.current; if (self && !self.IsAlive) return;
            bool cycle = (kb != null && kb.tKey.wasPressedThisFrame) || Consume(ref tCycle), gDown = (kb != null && kb.gKey.wasPressedThisFrame) || Consume(ref tDown), gUp = (kb != null && kb.gKey.wasReleasedThisFrame) || Consume(ref tUp);
            bool ySp = (kb != null && kb.yKey.wasPressedThisFrame) || Consume(ref tSpike), uSh = (kb != null && kb.uKey.wasPressedThisFrame) || Consume(ref tShield);
            var body = GetComponent<PlayerController>(); if (body && !body.enabled) { arc.enabled = false; Cook = -1; return; }   // driving
            if (Cook < 0 && Count(Selected) <= 0) for (int i = 1; i < 5; i++) { var n = (Throwable)(((int)Selected + i) % 5); if (Count(n) > 0) { Selected = n; break; } }   // ran out: hold the next one you have
            if (cycle) for (int i = 1; i <= 5; i++) { var n = (Throwable)(((int)Selected + i) % 5); if (Count(n) > 0 || i == 5) { Selected = n; break; } }
            if (ySp && spikes > 0) { spikes--; Deploy(spikeTemplate, 3.2f, true); }
            if (uSh && shields > 0) { shields--; Deploy(shieldTemplate, 1.8f, false); }

            // emergency smoke: two quick presses of throw while badly hurt = a smoke at your own feet, no cooking
            if (gDown && self && self.Current < self.Max * 0.3f && Time.time - lastThrowTap < 0.35f && Count(Throwable.Smoke) > 0)
            {
                Cook = -1; arc.enabled = false; counts[(int)Throwable.Smoke]--; StartCoroutine(Smoke(transform.position + transform.forward * 0.6f));
                KryvonKillFeed.Notice("EMERGENCY SMOKE"); lastThrowTap = -9; return;
            }
            if (gDown) lastThrowTap = Time.time;
            if (gDown && Count(Selected) > 0)
            {
                if (Selected == Throwable.Flare) { Spend(Throwable.Flare); drop?.Call(transform.position + transform.forward * 25f); return; }
                Cook = 0; Play(pinSound);
            }
            if (Cook >= 0)
            {
                Cook += Time.deltaTime;
                DrawArc();
                if (Selected == Throwable.Frag && Cook >= 5f) { Spend(Selected); Cook = -1; arc.enabled = false; StartCoroutine(Frag(Origin, 0f)); return; }   // held too long
                if (gUp) { Spend(Selected); Throw(Selected, Cook); Cook = -1; arc.enabled = false; }
            }
        }

        void DrawArc()
        {
            arc.enabled = true; var p = Origin; var v = Velocity();
            for (int i = 0; i < arc.positionCount; i++) { arc.SetPosition(i, p); v += Physics.gravity * 0.05f; p += v * 0.05f; }
        }
        void Play(AudioClip c, float vol = 0.8f) { if (c) AudioSource.PlayClipAtPoint(c, transform.position, vol); }

        void Throw(Throwable t, float cooked)
        {
            Play(throwSound);
            var tpl = templates[(int)t];
            var g = tpl ? Instantiate(tpl) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.SetActive(true); g.transform.position = Origin; g.transform.localScale = tpl ? tpl.transform.lossyScale : Vector3.one * 0.12f;
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            var col = g.AddComponent<SphereCollider>(); col.radius = 0.06f / Mathf.Max(0.01f, g.transform.lossyScale.x);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 0.4f; rb.linearVelocity = Velocity(); rb.angularVelocity = Random.insideUnitSphere * 8f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);
            var pm = new PhysicsMaterial { bounciness = t == Throwable.Molotov ? 0f : 0.35f, dynamicFriction = 0.6f }; col.sharedMaterial = pm;
            var p = g.AddComponent<KryvonProjectile>(); p.owner = this; p.kind = t; p.fuse = t == Throwable.Frag ? 5f - cooked : t == Throwable.Flashbang ? 2.2f : t == Throwable.Smoke ? 1.6f : 99f;
        }

        // ---------- effects, called by the projectile ----------
        public void Detonate(Throwable t, Vector3 at, GameObject obj)
        {
            switch (t)
            {
                case Throwable.Frag: StartCoroutine(Frag(at, 0)); break;
                case Throwable.Molotov: StartCoroutine(Molotov(at)); break;
                case Throwable.Smoke: StartCoroutine(Smoke(at)); break;
                case Throwable.Flashbang: Flash(at); break;
            }
            if (obj) Destroy(obj);
        }

        IEnumerator Frag(Vector3 at, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            KryvonFX.Explosion(at, 1.1f); if (boomSound) AudioSource.PlayClipAtPoint(boomSound, at, 1f);
            CameraShake.Kick(Mathf.Clamp01(1.2f - Vector3.Distance(at, transform.position) / 25f));
            foreach (var h in FindObjectsByType<Health>())
            {
                if (!h.IsAlive) continue;
                var c = h.transform.position + Vector3.up; float d = Vector3.Distance(at, c); if (d > 7f) continue;
                if (Physics.Linecast(at + Vector3.up * 0.2f, c, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(h.transform)) continue;   // walls protect
                float dmg = d < 2f ? 100f : Mathf.Lerp(100f, 0f, (d - 2f) / 5f);
                h.TakeDamage(new DamageInfo { Amount = dmg, Source = gameObject, Cause = "Frag Grenade" });
            }
            foreach (var v in KryvonVehicle.All) { float d = Vector3.Distance(at, v.transform.position); if (d < 6f) v.TakeDamage(new DamageInfo { Amount = Mathf.Lerp(900, 200, d / 6f), Source = gameObject, Cause = "Frag Grenade" }); }
            foreach (var w in FindObjectsByType<KryvonWindow>()) if (Vector3.Distance(at, w.transform.position) < 5f) w.TakeDamage(new DamageInfo { Amount = 10 });
            foreach (var dr in KryvonDoor.All.ToArray()) if (dr && Vector3.Distance(at, dr.transform.position) < 5f) dr.TakeDamage(new DamageInfo { Amount = 400 });
        }

        IEnumerator Molotov(Vector3 at)
        {
            if (glassSound) AudioSource.PlayClipAtPoint(glassSound, at, 1f);
            AudioSource loop = null;
            if (fireLoop) { var g = new GameObject("Fire"); g.transform.position = at; loop = g.AddComponent<AudioSource>(); loop.clip = fireLoop; loop.loop = true; loop.spatialBlend = 1; loop.Play(); }
            // real burning ground: flame tongues, embers, black smoke and a flickering light, dying down at the end
            var fire = KryvonFX.GroundFire(at + Vector3.up * 0.05f, 2.4f, 8f);
            for (float t = 0; t < 8f; t += 0.25f)
            {
                foreach (var h in FindObjectsByType<Health>())
                    if (h.IsAlive && (h.transform.position - at).sqrMagnitude < 9f) h.TakeDamage(new DamageInfo { Amount = 2.5f, Source = gameObject, Cause = "Molotov", IgnoreArmor = true });
                yield return new WaitForSeconds(0.25f);
            }
            if (loop) Destroy(loop.gameObject, 4f);
        }

        public static readonly List<(Vector3 p, float until)> Smokes = new();
        IEnumerator Smoke(Vector3 at)
        {
            if (hissSound) AudioSource.PlayClipAtPoint(hissSound, at, 0.9f);
            Smokes.Add((at, Time.time + 25f));
            KryvonFX.SmokeScreen(at, 25f); yield break;
        }

        void Flash(Vector3 at)
        {
            if (bangSound) AudioSource.PlayClipAtPoint(bangSound, at, 1f);
            KryvonFX.Flash(at);
            // the player is blinded if close and the bang is in view (or very close behind)
            var cam = Camera.main; if (!cam) return;
            var to = at - cam.transform.position; float d = to.magnitude; if (d > 16f) return;
            if (Physics.Linecast(cam.transform.position, at + Vector3.up * 0.2f, out var hit, ~0, QueryTriggerInteraction.Ignore) && (hit.point - at).sqrMagnitude > 0.5f) return;
            float facing = Vector3.Dot(cam.transform.forward, to.normalized);
            float strength = Mathf.Clamp01((facing + 0.3f) / 1.3f) * Mathf.Clamp01(1.2f - d / 16f);
            if (d < 3f) strength = Mathf.Max(strength, 0.6f);
            KryvonFlashOverlay.Blind(3f + 2f * strength, strength, ringSound);
        }

        void Deploy(GameObject tpl, float ahead, bool flat)
        {
            if (!tpl) return;
            var p = transform.position + transform.forward * ahead + Vector3.up * 2f;
            if (Physics.Raycast(p, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) p = g.point;
            var o = Instantiate(tpl, p, Quaternion.Euler(0, transform.eulerAngles.y + (flat ? 90 : 0), 0)); o.SetActive(true);
            Play(pinSound);
        }
    }
}

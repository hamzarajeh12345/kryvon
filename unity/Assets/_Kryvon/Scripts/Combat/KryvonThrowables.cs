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
        public float Cook { get; private set; } = -1;      // seconds since the pin was pulled, -1 = not holding
        public static readonly string[] Names = { "Frag", "Molotov", "Smoke", "Flashbang", "Flare Gun" };

        [SerializeField] GameObject[] templates = new GameObject[5];      // disabled model per throwable
        [SerializeField] GameObject spikeTemplate, shieldTemplate;
        [SerializeField] AudioClip pinSound, throwSound, boomSound, glassSound, hissSound, bangSound, ringSound, fireLoop;
        KryvonGear gear; KryvonAirdrop drop; LineRenderer arc; Health self;

        void Awake()
        {
            gear = GetComponent<KryvonGear>(); drop = GetComponent<KryvonAirdrop>(); self = GetComponent<Health>();
            var g = new GameObject("ThrowArc"); g.transform.SetParent(transform, false);
            arc = g.AddComponent<LineRenderer>(); arc.positionCount = 30; arc.widthMultiplier = 0.04f; arc.enabled = false;
            arc.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); arc.material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f));
        }
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
            var kb = Keyboard.current; if (kb == null || (self && !self.IsAlive)) return;
            var body = GetComponent<PlayerController>(); if (body && !body.enabled) { arc.enabled = false; Cook = -1; return; }   // driving
            if (kb.tKey.wasPressedThisFrame) for (int i = 1; i <= 5; i++) { var n = (Throwable)(((int)Selected + i) % 5); if (Count(n) > 0 || i == 5) { Selected = n; break; } }
            if (kb.yKey.wasPressedThisFrame && spikes > 0) { spikes--; Deploy(spikeTemplate, 3.2f, true); }
            if (kb.uKey.wasPressedThisFrame && shields > 0) { shields--; Deploy(shieldTemplate, 1.8f, false); }

            if (kb.gKey.wasPressedThisFrame && Count(Selected) > 0)
            {
                if (Selected == Throwable.Flare) { Spend(Throwable.Flare); drop?.Call(transform.position + transform.forward * 25f); return; }
                Cook = 0; Play(pinSound);
            }
            if (Cook >= 0)
            {
                Cook += Time.deltaTime;
                DrawArc();
                if (Selected == Throwable.Frag && Cook >= 5f) { Spend(Selected); Cook = -1; arc.enabled = false; StartCoroutine(Frag(Origin, 0f)); return; }   // held too long
                if (kb.gKey.wasReleasedThisFrame) { Spend(Selected); Throw(Selected, Cook); Cook = -1; arc.enabled = false; }
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
            KryvonFX.Fire(at); if (boomSound) AudioSource.PlayClipAtPoint(boomSound, at, 1f);
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
        }

        IEnumerator Molotov(Vector3 at)
        {
            if (glassSound) AudioSource.PlayClipAtPoint(glassSound, at, 1f);
            AudioSource loop = null;
            if (fireLoop) { var g = new GameObject("Fire"); g.transform.position = at; loop = g.AddComponent<AudioSource>(); loop.clip = fireLoop; loop.loop = true; loop.spatialBlend = 1; loop.Play(); }
            var light = new GameObject("FireLight").AddComponent<Light>(); light.transform.position = at + Vector3.up; light.type = LightType.Point; light.color = new Color(1f, 0.5f, 0.15f); light.range = 9;
            for (float t = 0; t < 8f; t += 0.25f)
            {
                for (int i = 0; i < 4; i++) KryvonFX.Flame(at + new Vector3(Random.Range(-2.6f, 2.6f), 0.1f, Random.Range(-2.6f, 2.6f)));
                light.intensity = 3f + Random.value * 2f;
                foreach (var h in FindObjectsByType<Health>())
                    if (h.IsAlive && (h.transform.position - at).sqrMagnitude < 9f) h.TakeDamage(new DamageInfo { Amount = 2.5f, Source = gameObject, Cause = "Molotov", IgnoreArmor = true });
                yield return new WaitForSeconds(0.25f);
            }
            Destroy(light.gameObject); if (loop) Destroy(loop.gameObject);
        }

        public static readonly List<(Vector3 p, float until)> Smokes = new();
        IEnumerator Smoke(Vector3 at)
        {
            if (hissSound) AudioSource.PlayClipAtPoint(hissSound, at, 0.9f);
            Smokes.Add((at, Time.time + 25f));
            for (float t = 0; t < 25f; t += 0.2f) { KryvonFX.SmokeCloud(at, t < 3f ? t / 3f : 1f); yield return new WaitForSeconds(0.2f); }
        }

        void Flash(Vector3 at)
        {
            if (bangSound) AudioSource.PlayClipAtPoint(bangSound, at, 1f);
            var l = new GameObject("Flash").AddComponent<Light>(); l.transform.position = at + Vector3.up * 0.3f; l.type = LightType.Point; l.range = 25; l.intensity = 40; Destroy(l.gameObject, 0.12f);
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

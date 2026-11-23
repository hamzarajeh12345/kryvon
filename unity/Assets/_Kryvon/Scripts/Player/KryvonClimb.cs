// Climb, vault and fall damage.
//  • Jump facing a wall, car, barrier or crate whose top is 0.6–2.1 m high → climb onto it.
//  • Jump facing a window (or any low opening above a sill) → vault through it and land on the other side.
//  • Falling hurts: nothing up to ~3 m, about 40 from 6 m, deadly from ~9 m. Armour does not help.
using System.Collections;
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(PlayerController), typeof(CharacterController))]
    public class KryvonClimb : MonoBehaviour
    {
        public const float MinHeight = 0.6f, MaxHeight = 2.1f;
        public bool Climbing { get; private set; }
        public static int Climbs, Vaults;
        public static string LastReason = "";
        PlayerController body; CharacterController cc; Health hp; Animator anim; bool hasClimb;

        public struct Move { public Vector3 over, land; public bool vault; }

        void Awake()
        {
            body = GetComponent<PlayerController>(); cc = GetComponent<CharacterController>(); hp = GetComponent<Health>();
            body.OnJumped += TryClimb;
            body.OnLanded += FallDamage;
        }

        // ---- fall damage (impact speed in m/s; gravity is 22 m/s², so 3 m ≈ 11.5 m/s, 6 m ≈ 16, 9 m ≈ 20) ----
        void FallDamage(float speed)
        {
            if (hp == null || !hp.IsAlive || Climbing || speed <= 11.5f) return;
            float dmg = (speed - 11.5f) * (speed - 11.5f) * 1.2f;
            // still untouched (full health, as at the start of the match): a fall from anywhere leaves you badly hurt, never dead;
            // once you have taken any damage, falls count in full and a high one can kill
            if (hp.Current >= hp.Max - 0.01f) dmg = Mathf.Min(dmg, hp.Current - 12f);
            hp.TakeDamage(new DamageInfo { Amount = dmg, Source = null, Cause = "Fall", IgnoreArmor = true });
            CameraShake.Kick(Mathf.Clamp01(dmg / 60f));
        }

        Animator Anim()
        {
            if (anim && anim.isActiveAndEnabled) return anim;
            foreach (var a in GetComponentsInChildren<Animator>()) if (a.isActiveAndEnabled) { anim = a; hasClimb = false; foreach (var p in a.parameters) if (p.name == "Climb") hasClimb = true; }
            return anim;
        }

        static bool Mine(Collider c, Transform t) => c.transform.IsChildOf(t);

        /// what can be climbed or vaulted in front, or null (the reason is kept in LastReason)
        public static Move? FindLedge(Transform t, CharacterController cc)
        {
            var fwd = t.forward; fwd.y = 0; fwd.Normalize();
            var feet = t.position;
            float wall = float.MaxValue;
            foreach (float hgt in new[] { 0.35f, 0.7f, 1.05f, 1.4f, 1.75f })
                if (Physics.Raycast(feet + Vector3.up * hgt, fwd, out var w, 1.6f, ~0, QueryTriggerInteraction.Ignore) && !Mine(w.collider, t)) wall = Mathf.Min(wall, w.distance);
            if (wall > 1.6f) { LastReason = "nothing in front"; return null; }
            LastReason = $"wall {wall:0.00}m;";
            for (float d = wall + 0.08f; d <= wall + 0.9f; d += 0.12f)
            {
                var probe = feet + fwd * d + Vector3.up * (MaxHeight + 0.3f);
                if (!Physics.Raycast(probe, Vector3.down, out var top, MaxHeight + 0.3f, ~0, QueryTriggerInteraction.Ignore) || Mine(top.collider, t)) { LastReason += $" {d:0.0}:-"; continue; }
                // the way over the top must be clear –
                // otherwise a floor behind a wall looks like a ledge and the player is pulled through the wall
                var over0 = new Vector3(feet.x, top.point.y + 0.35f, feet.z);
                if (Physics.Raycast(over0, fwd, out var blockHit, d + 0.25f, ~0, QueryTriggerInteraction.Ignore) && !Mine(blockHit.collider, t)) { LastReason += $" {d:0.0}:blocked"; continue; }
                float h = top.point.y - feet.y;
                if (h < MinHeight || h > MaxHeight || Vector3.Angle(top.normal, Vector3.up) > 35f) { LastReason += $" {d:0.0}:h{h:0.0}"; continue; }
                // how much room above the top?
                float room = Physics.Raycast(top.point + Vector3.up * 0.05f, Vector3.up, out var roof, 3f, ~0, QueryTriggerInteraction.Ignore) ? roof.distance : 3f;
                var p0 = top.point + Vector3.up * (cc.radius + 0.06f); var p1 = top.point + Vector3.up * (cc.height - cc.radius);
                if (room >= cc.height && !Physics.CheckCapsule(p0, p1, cc.radius * 0.85f, ~0, QueryTriggerInteraction.Ignore))
                    return new Move { over = top.point + fwd * 0.2f, land = top.point + fwd * 0.2f, vault = false };      // climb onto it
                if (room >= 0.7f)
                {
                    // an unbroken window is glass: shoot or punch it first
                    if (Physics.Raycast(feet + Vector3.up * (h + 0.4f), fwd, out var glass, wall + 0.6f, ~0, QueryTriggerInteraction.Ignore) && glass.collider.GetComponentInParent<KryvonWindow>() is KryvonWindow kw && !kw.Broken)
                    { LastReason += " window glass is intact"; return null; }   // a window or a low gap: go through and land on the far side
                    for (float e = 0.45f; e <= 1.3f; e += 0.15f)
                    {
                        var far = top.point + fwd * e;
                        if (!Physics.Raycast(far + Vector3.up * 0.3f, Vector3.down, out var floor, 4.5f, ~0, QueryTriggerInteraction.Ignore) || Mine(floor.collider, t)) continue;
                        if (floor.point.y > top.point.y - 0.05f) continue;      // still on the sill / wall top
                        var f0 = floor.point + Vector3.up * (cc.radius + 0.06f); var f1 = floor.point + Vector3.up * (cc.height - cc.radius);
                        if (Physics.CheckCapsule(f0, f1, cc.radius * 0.85f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        return new Move { over = top.point + fwd * 0.1f, land = floor.point, vault = true };
                    }
                    LastReason += $" {d:0.0}:h{h:0.0} gap{room:0.0} no landing";
                }
                else LastReason += $" {d:0.0}:h{h:0.0} room{room:0.0}";
            }
            return null;
        }

        void TryClimb()
        {
            if (Climbing || body.Stance != Stance.Stand) return;
            var m = FindLedge(transform, cc); if (m == null) return;
            StartCoroutine(Do(m.Value));
        }

        IEnumerator Do(Move m)
        {
            Climbing = true; if (m.vault) Vaults++; else Climbs++;
            var a = Anim(); if (a && hasClimb) a.SetTrigger("Climb");
            cc.enabled = false; body.enabled = false;
            var from = transform.position;
            var up = new Vector3(from.x, m.over.y + (m.vault ? 0.25f : 0.05f), from.z);
            float h = m.over.y - from.y, rise = Mathf.Lerp(0.35f, 0.6f, Mathf.InverseLerp(MinHeight, MaxHeight, h));
            for (float t = 0; t < rise; t += Time.deltaTime) { transform.position = Vector3.Lerp(from, up, Mathf.SmoothStep(0, 1, t / rise)); yield return null; }
            var across = new Vector3(m.over.x, up.y, m.over.z);
            for (float t = 0; t < 0.2f; t += Time.deltaTime) { transform.position = Vector3.Lerp(up, across, t / 0.2f); yield return null; }
            if (m.vault)
            {   // over the sill and down the other side
                var above = new Vector3(m.land.x, up.y, m.land.z);
                for (float t = 0; t < 0.2f; t += Time.deltaTime) { transform.position = Vector3.Lerp(across, above, t / 0.2f); yield return null; }
                float drop = Mathf.Max(0.12f, Mathf.Sqrt(Mathf.Max(0f, above.y - m.land.y) / 11f));
                for (float t = 0; t < drop; t += Time.deltaTime) { transform.position = Vector3.Lerp(above, m.land, (t / drop) * (t / drop)); yield return null; }
            }
            transform.position = m.land + Vector3.up * 0.03f;
            cc.enabled = true; body.enabled = true; Climbing = false;
        }
    }
}

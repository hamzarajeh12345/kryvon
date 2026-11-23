// KryvonSwim – the body actually swims in the sea instead of walking chest-deep.
//  • moving  → front crawl: body flat on the water, arms reach over the head one after the other, pull under the belly,
//              elbow-high recovery; flutter kick from the hips; a splash where each hand enters, foam and a wake behind
//  • still   → treading water: upright, arms sculling side to side, legs eggbeater
//  • the gun goes on the back (hidden) while swimming and comes back when you walk out
// Runs after the animator, the foot planting and the two-hand gun IK, and bends the bones on top of them.
using UnityEngine;

namespace Kryvon
{
    [DefaultExecutionOrder(150)]
    public class KryvonSwim : MonoBehaviour
    {
        PlayerController body; Animator anim; Transform model;
        Transform uaL, laL, haL, uaR, laR, haR, ulL, llL, ftL, ulR, llR, ftR, head;
        Quaternion modelBaseRot; bool bound;
        float swimW, moveW, phase, kick, wakeT, lastL = 1f, lastR = 1f;
        Transform[] guns; bool[] gunWas;
        static AudioClip splash;

        public bool Active => swimW > 0.01f;
        public string Debug { get; private set; } = "";

        void Awake() { body = GetComponent<PlayerController>(); }

        void Bind()
        {
            anim = GetComponentInChildren<Animator>(); bound = true;
            if (!anim || !anim.isHuman) { anim = null; return; }
            model = anim.transform; modelBaseRot = model.localRotation;
            Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
            uaL = B(HumanBodyBones.LeftUpperArm); laL = B(HumanBodyBones.LeftLowerArm); haL = B(HumanBodyBones.LeftHand);
            uaR = B(HumanBodyBones.RightUpperArm); laR = B(HumanBodyBones.RightLowerArm); haR = B(HumanBodyBones.RightHand);
            ulL = B(HumanBodyBones.LeftUpperLeg); llL = B(HumanBodyBones.LeftLowerLeg); ftL = B(HumanBodyBones.LeftFoot);
            ulR = B(HumanBodyBones.RightUpperLeg); llR = B(HumanBodyBones.RightLowerLeg); ftR = B(HumanBodyBones.RightFoot);
            head = B(HumanBodyBones.Head);
            guns = System.Array.FindAll(GetComponentsInChildren<Transform>(true), t => t.name.StartsWith("Gun_"));
            gunWas = new bool[guns.Length];
        }

        void LateUpdate()
        {
            if (!body) return;
            if (!bound || (anim && !anim.isActiveAndEnabled)) Bind();
            if (!anim || !uaL || !laL || !haL || !uaR || !laR || !haR) return;
            float dt = Time.deltaTime;
            bool swimming = body.IsSwimming;
            bool was = swimW > 0.01f;
            swimW = Mathf.MoveTowards(swimW, swimming ? 1f : 0f, dt * 2.5f);
            if (swimW <= 0.01f) { if (was) { model.localRotation = modelBaseRot; HideGuns(false); } return; }
            if (!was) HideGuns(true);

            float speed = body.PlanarVelocity.magnitude;
            moveW = Mathf.MoveTowards(moveW, Mathf.Clamp01((speed - 0.25f) / 0.9f), dt * 1.6f);
            phase += dt * Mathf.Lerp(2.6f, 4.4f, moveW);        // one arm cycle ≈ 1.4–2.4 s
            kick += dt * Mathf.Lerp(5f, 9f, moveW);

            // ---- the body: flat on the water when crawling, upright when treading – pivot at the chest so the head stays up ----
            float pitch = Mathf.Lerp(12f, 74f, moveW) * swimW;
            var pr = Quaternion.AngleAxis(pitch, Vector3.right);
            model.localRotation = pr * modelBaseRot;
            Vector3 pivot = Vector3.up * 1.25f;
            model.localPosition += pivot - pr * pivot + Vector3.up * (0.06f * Mathf.Sin(phase * 2f) * moveW);   // a little bob with each stroke

            Quaternion W = transform.rotation * pr;
            Vector3 up = W * Vector3.up, belly = W * Vector3.forward, right = W * Vector3.right;

            // ---- arms ----
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                float ph = phase + (s == 0 ? 0f : Mathf.PI);
                float sn = Mathf.Sin(ph), cs = Mathf.Cos(ph);
                // crawl: overhead (0) → pull under the belly (π/2) → past the hip (π) → recovery over the back (3π/2)
                Vector3 crawl = (up * cs + belly * Mathf.Max(sn, sn * 0.55f) + right * side * (0.22f + 0.15f * Mathf.Max(0f, -sn))).normalized;
                float recov = Mathf.Max(0f, -sn);
                Vector3 crawlFore = Vector3.Slerp(crawl, (belly + up * 0.2f).normalized, 0.65f * recov);   // high elbow, hand hangs to the water
                // treading: sculling out to the side, forearms sweeping back and forth
                Vector3 tread = (right * side * 0.85f + belly * (0.3f + 0.25f * Mathf.Sin(phase * 2.2f + side)) - up * 0.4f).normalized;
                Vector3 treadFore = (belly * 0.8f + right * side * (0.25f + 0.35f * Mathf.Sin(phase * 2.2f + side)) - up * 0.2f).normalized;
                Vector3 ua = Vector3.Slerp(tread, crawl, moveW), la = Vector3.Slerp(treadFore, crawlFore, moveW);
                Aim(s == 0 ? uaL : uaR, s == 0 ? laL : laR, ua);
                Aim(s == 0 ? laL : laR, s == 0 ? haL : haR, la);

                // the hand goes in: splash where it enters the water
                float last = s == 0 ? lastL : lastR;
                if (moveW > 0.4f && last < 0f && cs >= 0f && sn < 0f) Splash((s == 0 ? haL : haR).position, 0.7f);
                if (s == 0) lastL = cs; else lastR = cs;
            }

            // ---- legs ----
            if (ulL && llL && ftL && ulR && llR && ftR)
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f, k = kick + (s == 0 ? 0f : Mathf.PI);
                    Vector3 flutter = (-up + belly * 0.2f * Mathf.Sin(k) + right * side * 0.06f).normalized;
                    Vector3 flutterLow = (-up + belly * 0.28f * Mathf.Sin(k - 0.9f) - belly * 0.12f).normalized;
                    Vector3 egg = (-up + belly * 0.45f + right * side * 0.2f).normalized;                         // knees forward
                    Vector3 eggLow = (-up - belly * 0.15f + right * side * 0.35f * Mathf.Sin(kick * 0.6f + side)).normalized;
                    Aim(s == 0 ? ulL : ulR, s == 0 ? llL : llR, Vector3.Slerp(egg, flutter, moveW));
                    Aim(s == 0 ? llL : llR, s == 0 ? ftL : ftR, Vector3.Slerp(eggLow, flutterLow, moveW));
                }

            // ---- depth: a crawling swimmer lies IN the water – back at the surface, the head just breaking it ----
            if (ulL && ulR && uaL && uaR && body.WaterY > -1e5f && moveW > 0.01f)
            {
                float torso = (ulL.position.y + ulR.position.y + uaL.position.y + uaR.position.y) * 0.25f;
                float sink = (body.WaterY - 0.03f) - torso;
                model.position += Vector3.up * sink * moveW * swimW;
            }
            if (ulL && llL && ftL && uaL && laL)
                Debug = $"pitch {pitch:0} moveW {moveW:0.00} · spine vs bodyUp {Vector3.Angle((head ? head.position : uaL.position) - ulL.position, up):0}° · thigh vs -up {Vector3.Angle(llL.position - ulL.position, -up):0}° · shin vs -up {Vector3.Angle(ftL.position - llL.position, -up):0}° · hip y-water {ulL.position.y - body.WaterY:0.00} · head y-water {(head ? head.position.y - body.WaterY : 0):0.00} · bodyUp {up}";
            // ---- head: lifts a little so the face looks ahead, turns to breathe every other stroke ----
            if (head)
            {
                float breathe = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * 0.5f)), 6f) * moveW;
                var hr = Quaternion.AngleAxis(-35f * moveW * swimW, right) * Quaternion.AngleAxis(55f * breathe * swimW, up);
                head.rotation = hr * head.rotation;
            }

            // ---- water around the body: foam ring when still, a wake when moving ----
            wakeT -= dt;
            if (wakeT <= 0f && body.WaterY > -1e5f)
            {
                wakeT = moveW > 0.3f ? 0.12f : 0.45f;
                var p = transform.position; p.y = body.WaterY + 0.02f;
                KryvonFX.Foam(p + transform.forward * (moveW > 0.3f ? 0.9f : 0f), moveW > 0.3f ? 0.9f : 0.55f);
            }
        }

        // turn a bone so its child points along dir (blended by the swim weight)
        void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (!bone || !child) return;
            Vector3 cur = child.position - bone.position; if (cur.sqrMagnitude < 1e-6f) return;
            var q = Quaternion.FromToRotation(cur.normalized, dir);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, q, swimW) * bone.rotation;
        }

        void Splash(Vector3 at, float k)
        {
            if (body.WaterY > -1e5f) at.y = body.WaterY + 0.03f;
            KryvonFX.Splash(at, k);
            if (!splash) splash = MakeSplash();
            KryvonAudio.PlayAt(splash, at, 0.35f * k);
        }

        void HideGuns(bool hide)
        {
            if (guns == null) return;
            for (int i = 0; i < guns.Length; i++)
            {
                if (!guns[i]) continue;
                if (hide) { gunWas[i] = guns[i].gameObject.activeSelf; guns[i].gameObject.SetActive(false); }
                else if (gunWas[i]) guns[i].gameObject.SetActive(true);
            }
        }

        // a short watery slap: filtered noise burst with a low "plop"
        static AudioClip MakeSplash()
        {
            int rate = 22050, n = rate * 35 / 100; var d = new float[n]; uint s = 0x9E3779B9u; float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5; float noise = (s / (float)uint.MaxValue) * 2f - 1f;
                float t = i / (float)rate, env = Mathf.Exp(-t * 14f) * Mathf.Min(1f, t * 400f);
                lp += (noise - lp) * 0.35f;
                d[i] = (lp * 0.8f + Mathf.Sin(2 * Mathf.PI * (180f - 300f * t) * t) * 0.35f * Mathf.Exp(-t * 20f)) * env;
            }
            var c = AudioClip.Create("swim_splash", n, 1, rate, false); c.SetData(d, 0); return c;
        }
    }
}

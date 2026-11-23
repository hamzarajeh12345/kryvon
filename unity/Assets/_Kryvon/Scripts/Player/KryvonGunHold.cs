// Holding the gun properly. The rifles are parented to the right hand and were aligned once, in the AIM pose, at build
// time – so in every other pose (walking, ready, reloading) the gun followed the wrist and pointed at the floor or
// backwards. Every frame, after the animation:
//   1. the gun's barrel is pointed where it should be: at the crosshair while aiming, forward and a little down
//      ("low ready") while walking, diagonal across the chest while sprinting, flat when prone;
//   2. the LEFT hand is pulled onto the handguard with a two-bone IK (shoulder → elbow → hand, elbow bent down and out),
//      so the rifle is held with both hands instead of hanging from one.
// Pistols point forward-down and keep the left hand free when not aiming.
using UnityEngine;

namespace Kryvon
{
    [DefaultExecutionOrder(100)]       // after the animator and PlayerAnimatorBridge (which moves the model to the ground)
    public class KryvonGunHold : MonoBehaviour
    {
        Animator anim; WeaponController wc; PlayerController body; KryvonBot bot; Camera cam;
        Transform armL, foreL, handL; Transform[] guns; Transform active; float gunLen = 0.9f; bool pistol;
        Vector3 dirNow; float ikW;

        void Start() { wc = GetComponent<WeaponController>(); body = GetComponent<PlayerController>(); bot = GetComponent<KryvonBot>(); dirNow = transform.forward; Bind(); }
        /// the active hero model (the hero can be switched after Start): its arm bones and the guns in its hand
        void Bind()
        {
            anim = GetComponentInChildren<Animator>();          // the active hero only
            armL = foreL = handL = null;
            if (anim && anim.isHuman)
            {
                armL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); foreL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            }
            guns = anim ? System.Array.FindAll(anim.GetComponentsInChildren<Transform>(true), t => t.name.StartsWith("Gun_")) : null;
            active = null;
        }

        Transform Active()
        {
            if (guns == null) return null;
            foreach (var g in guns) if (g && g.gameObject.activeInHierarchy) return g;
            return null;
        }

        void LateUpdate()
        {
            if ((!anim || !anim.isActiveAndEnabled) && Time.frameCount % 20 == 0) Bind();       // hero switched (checked a few times a second)
            if (!anim || guns == null || guns.Length == 0) return;
            var g = Active();
            if (g != active)
            {
                active = g;
                if (g)
                {   // length of this gun (for the handguard point) and whether it is a pistol
                    var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); gunLen = Mathf.Clamp(b.size.magnitude * 0.9f, 0.2f, 1.6f); }
                    pistol = g.name.Contains("pistol");
                }
            }
            if (!active) { ikW = 0; return; }

            // ---- where the barrel should point ----
            cam ??= Camera.main;
            bool aiming = wc ? wc.IsAiming : bot && bot.S == KryvonBot.State.Fight;
            bool sprint = body && body.IsSprinting && !aiming;
            bool prone = body && body.Stance == Stance.Prone;
            bool reloading = wc && wc.IsReloading;
            var fwd = transform.forward; var right = transform.right;
            Vector3 want;
            if (aiming && wc && cam) want = cam.transform.forward;                                         // at the crosshair
            else if (aiming && bot) want = Quaternion.AngleAxis(-2f, right) * fwd;
            else if (prone) want = Quaternion.AngleAxis(2f, right) * fwd;
            else if (sprint) want = Quaternion.AngleAxis(-38f, Vector3.up) * Quaternion.AngleAxis(-20f, right) * fwd;   // across the chest, muzzle up-left
            else if (reloading) want = Quaternion.AngleAxis(-25f, Vector3.up) * Quaternion.AngleAxis(25f, right) * fwd;  // tilted in to the body, mag towards you
            else want = Quaternion.AngleAxis(pistol ? 35f : 16f, right) * Quaternion.AngleAxis(pistol ? 0f : -8f, Vector3.up) * fwd;   // low ready
            dirNow = Vector3.Slerp(dirNow, want, 1f - Mathf.Exp(-(aiming ? 30f : 12f) * Time.deltaTime));
            active.rotation = Quaternion.LookRotation(dirNow, transform.up);

            // ---- left hand on the handguard (two-bone IK) ----
            bool twoHands = !pistol || aiming;
            float wantW = twoHands && !reloading && armL && foreL && handL ? 1f : 0f;
            ikW = Mathf.MoveTowards(ikW, wantW, Time.deltaTime * 6f);
            if (ikW <= 0.001f) return;
            var grip = active.position + active.forward * (pistol ? 0.02f : gunLen * 0.3f) - active.up * 0.035f - active.right * (pistol ? 0.03f : 0.01f);
            var target = Vector3.Lerp(handL.position, grip, ikW);
            var pole = armL.position - transform.up * 0.6f - right * 0.35f + fwd * 0.1f;                  // elbow down and out
            SolveTwoBone(armL, foreL, handL, target, pole);
        }

        /// analytic two-bone IK in world space: bends shoulder and elbow so the tip reaches target, elbow towards pole
        static void SolveTwoBone(Transform root, Transform mid, Transform tip, Vector3 target, Vector3 pole)
        {
            Vector3 rootPos = root.position, midPos = mid.position, tipPos = tip.position;
            float a = Vector3.Distance(rootPos, midPos), b = Vector3.Distance(midPos, tipPos);
            if (a < 1e-4f || b < 1e-4f) return;
            float c = Mathf.Clamp(Vector3.Distance(rootPos, target), 0.01f, a + b - 0.001f);
            var toTarget = (target - rootPos).normalized;
            float angRoot = Mathf.Acos(Mathf.Clamp((a * a + c * c - b * b) / (2f * a * c), -1f, 1f)) * Mathf.Rad2Deg;
            var n = Vector3.Cross(toTarget, pole - rootPos); if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(toTarget, Vector3.up); n.Normalize();
            // the elbow sits on the side of the pole: try both rotations, keep the one closer to the pole
            var m1 = rootPos + Quaternion.AngleAxis(angRoot, n) * toTarget * a;
            var m2 = rootPos + Quaternion.AngleAxis(-angRoot, n) * toTarget * a;
            var midWanted = (m1 - pole).sqrMagnitude < (m2 - pole).sqrMagnitude ? m1 : m2;
            root.rotation = Quaternion.FromToRotation(midPos - rootPos, midWanted - rootPos) * root.rotation;
            midPos = mid.position; tipPos = tip.position;
            mid.rotation = Quaternion.FromToRotation(tipPos - midPos, target - midPos) * mid.rotation;
        }
    }
}

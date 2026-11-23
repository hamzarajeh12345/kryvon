// PlayerAnimatorBridge – feeds the Animator from the gameplay scripts, so your character art just plugs in.
// Create these parameters in your Animator Controller (missing ones are skipped silently):
//   Float  MoveX, MoveY (local velocity m/s), Speed, Lean (-1..1), AimPitch (degrees)
//   Int    Stance (0 stand, 1 crouch, 2 prone), WeaponIndex
//   Bool   Grounded, Sprinting, Aiming, ADS, Knocked, Dead, Reloading
//   Trigger Jump, Fire, Reload, Land
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] float dampTime = 0.1f;

        PlayerController body;
        WeaponController weapons;
        Health health;
        ThirdPersonCamera cameraRig;
        readonly HashSet<int> has = new();

        static readonly int MoveX = Hash("MoveX"), MoveY = Hash("MoveY"), Speed = Hash("Speed"), Lean = Hash("Lean"), AimPitch = Hash("AimPitch"),
            StanceP = Hash("Stance"), WeaponIndex = Hash("WeaponIndex"), Grounded = Hash("Grounded"), Sprinting = Hash("Sprinting"),
            Aiming = Hash("Aiming"), Ads = Hash("ADS"), Knocked = Hash("Knocked"), Dead = Hash("Dead"), Reloading = Hash("Reloading"),
            Jump = Hash("Jump"), Fire = Hash("Fire"), ReloadT = Hash("Reload"), Land = Hash("Land");
        static int Hash(string s) => Animator.StringToHash(s);

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (!GetComponent<KryvonGunHold>()) gameObject.AddComponent<KryvonGunHold>();
            if (!GetComponent<KryvonSwim>() && GetComponent<PlayerController>()) gameObject.AddComponent<KryvonSwim>();   // front crawl / treading water in the sea   // both hands on the gun, barrel where it belongs
            body = GetComponent<PlayerController>();
            weapons = GetComponent<WeaponController>();
            health = GetComponent<Health>();
            if (Camera.main != null) cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            if (animator != null) foreach (var p in animator.parameters) has.Add(p.nameHash);

            if (body != null) { body.OnJumped += () => Trigger(Jump); body.OnLanded += _ => Trigger(Land); }
            if (weapons != null) { weapons.OnShot += _ => Trigger(Fire); weapons.OnReloadStarted += _ => Trigger(ReloadT); }
        }

        float leanNow, proneW, armedW = 1f;

        // ---- feet on the ground ----
        // The generated characters' soles don't sit exactly at the model origin, and the controller keeps a skin gap, so the
        // body hovered a few centimetres (more on slopes and stairs). After the animation each frame: measure the ground
        // under each foot and lower / raise the whole model so the lower sole touches it. Smoothed, clamped, bones only.
        public float GroundOffset => groundOff;
        Vector3 modelBase; bool baseSet; float groundOff; Transform footL, footR, toeL, toeR;
        readonly RaycastHit[] footHits = new RaycastHit[8];
        float GroundBelow(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 0.6f, Vector3.down, footHits, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            for (int i = 0; i < n; i++)
            {
                var c = footHits[i].collider; if (c.transform.IsChildOf(transform)) continue;
                if (float.IsNaN(best) || footHits[i].point.y > best) { if (footHits[i].point.y <= p.y + 0.6f) best = footHits[i].point.y; }
            }
            return best;
        }
        void PlantFeet()
        {
            var model = animator.transform; if (model == transform) return;
            if (!baseSet) { modelBase = model.localPosition; baseSet = true; footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot); footR = animator.GetBoneTransform(HumanBodyBones.RightFoot); toeL = animator.GetBoneTransform(HumanBodyBones.LeftToes); toeR = animator.GetBoneTransform(HumanBodyBones.RightToes); }
            if (!footL || !footR) return;
            bool on = body.IsGrounded && body.Stance != Stance.Prone && (health == null || (health.IsAlive && !health.IsKnocked));
            float target = 0f;
            if (on)
            {
                // the sole: a little under the ankle (or under the toes when they are lower)
                float soleK = 0.085f * Mathf.Max(0.5f, animator.humanScale);
                float sL = Mathf.Min(footL.position.y - soleK, toeL ? toeL.position.y - 0.025f : float.MaxValue);
                float sR = Mathf.Min(footR.position.y - soleK, toeR ? toeR.position.y - 0.025f : float.MaxValue);
                float gL = GroundBelow(footL.position), gR = GroundBelow(footR.position);
                float gap = float.MaxValue;
                if (!float.IsNaN(gL)) gap = Mathf.Min(gap, sL - gL);
                if (!float.IsNaN(gR)) gap = Mathf.Min(gap, sR - gR);
                if (gap < float.MaxValue) target = Mathf.Clamp(groundOff - gap, -0.35f, 0.2f);   // groundOff is already in the bones
            }
            groundOff = Mathf.Lerp(groundOff, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            model.localPosition = modelBase + model.parent.InverseTransformVector(Vector3.up * groundOff);
        }

        void LateUpdate()
        {
            if (animator == null || !animator.isHuman || body == null) return;
            PlantFeet();
            // lying down: tip the hips forward so the whole body is flat, chest on the ground
            proneW = Mathf.MoveTowards(proneW, body.Stance == Stance.Prone ? 1f : 0f, Time.deltaTime * 3f);
            bool gun = weapons != null && !weapons.IsMelee;
            armedW = Mathf.MoveTowards(armedW, gun ? 1f : 0f, Time.deltaTime * 4f);
            if (animator.layerCount > 1) animator.SetLayerWeight(1, armedW * (1f - proneW));   // empty hands swing naturally
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips); var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (proneW > 0.001f && hips && head)
            {
                bool upright = head.position.y - hips.position.y > 0.35f;          // the crawl clip may already be horizontal
                if (upright) hips.rotation = Quaternion.AngleAxis(84f * proneW, transform.right) * hips.rotation;
                var hp = hips.position; hp.y = Mathf.Lerp(hp.y, transform.position.y + 0.2f, proneW); hips.position = hp;
            }
            leanNow = Mathf.Lerp(leanNow, body.Lean, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (Mathf.Abs(leanNow) < 0.01f) return;
            var fwd = transform.forward;
            foreach (var (bone, deg) in new[] { (HumanBodyBones.Spine, 9f), (HumanBodyBones.Chest, 10f), (HumanBodyBones.UpperChest, 7f), (HumanBodyBones.Neck, -6f) })
            {
                var t = animator.GetBoneTransform(bone); if (t) t.rotation = Quaternion.AngleAxis(-leanNow * deg, fwd) * t.rotation;
            }
        }

        void Update()
        {
            if (animator == null) return;
            if (body != null)
            {
                SetF(MoveX, body.LocalMove.x, true); SetF(MoveY, body.LocalMove.y, true);
                SetF(Speed, body.PlanarVelocity.magnitude, true); SetF(Lean, body.Lean, false);
                SetI(StanceP, (int)body.Stance); SetB(Grounded, body.IsGrounded); SetB(Sprinting, body.IsSprinting);
            }
            if (weapons != null)
            {
                SetB(Aiming, weapons.IsAiming); SetB(Ads, weapons.IsAds); SetB(Reloading, weapons.IsReloading); SetI(WeaponIndex, weapons.CurrentIndex);
            }
            if (cameraRig != null) SetF(AimPitch, cameraRig.Pitch, false);
            if (health != null) { SetB(Knocked, health.IsKnocked); SetB(Dead, !health.IsAlive); }
        }

        void SetF(int id, float v, bool damp) { if (!has.Contains(id)) return; if (damp) animator.SetFloat(id, v, dampTime, Time.deltaTime); else animator.SetFloat(id, v); }
        void SetI(int id, int v) { if (has.Contains(id)) animator.SetInteger(id, v); }
        void SetB(int id, bool v) { if (has.Contains(id)) animator.SetBool(id, v); }
        void Trigger(int id) { if (animator != null && has.Contains(id)) animator.SetTrigger(id); }
    }
}

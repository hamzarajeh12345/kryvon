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
            body = GetComponent<PlayerController>();
            weapons = GetComponent<WeaponController>();
            health = GetComponent<Health>();
            if (Camera.main != null) cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            if (animator != null) foreach (var p in animator.parameters) has.Add(p.nameHash);

            if (body != null) { body.OnJumped += () => Trigger(Jump); body.OnLanded += _ => Trigger(Land); }
            if (weapons != null) { weapons.OnShot += _ => Trigger(Fire); weapons.OnReloadStarted += _ => Trigger(ReloadT); }
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

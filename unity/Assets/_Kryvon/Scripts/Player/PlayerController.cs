// PlayerController – smooth third-person movement for a tactical shooter.
//  • Stances: stand / crouch / prone, with smooth height changes and ceiling checks
//  • Speeds: walk (Ctrl), run (default), sprint (Shift, forward only); slower backwards and sideways
//  • Acceleration / deceleration instead of instant speed changes (no "ice", no "snapping")
//  • Lean (Q/E) for peeking corners, jump with coyote time + input buffer, air control, steep-slope sliding
//  • Free look (Alt): the camera orbits but you keep running the same way
// Exposes everything an Animator needs (see PlayerAnimatorBridge).
using System;
using UnityEngine;

namespace Kryvon
{
    public enum Stance { Stand, Crouch, Prone }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ThirdPersonCamera cameraRig;

        [Header("Speeds (m/s)")]
        [SerializeField] float walkSpeed = 1.9f;
        [SerializeField] float runSpeed = 4.4f;
        [SerializeField] float sprintSpeed = 6.0f;
        [SerializeField] float crouchSpeed = 2.4f;
        [SerializeField] float proneSpeed = 1.1f;
        [SerializeField] float knockedSpeed = 0.9f;
        [SerializeField, Range(0.3f, 1f)] float backwardMultiplier = 0.7f;
        [SerializeField, Range(0.3f, 1f)] float strafeMultiplier = 0.88f;
        [SerializeField, Range(0.3f, 1f)] float aimMultiplier = 0.65f;
        [Tooltip("Sprint only while the stick/keys point within this angle of forward.")]
        [SerializeField] float sprintMaxAngle = 50f;

        [Header("Acceleration (m/s²)")]
        [SerializeField] float groundAcceleration = 38f;
        [SerializeField] float groundDeceleration = 46f;
        [SerializeField] float airAcceleration = 6f;

        [Header("Jump & gravity")]
        [SerializeField] float jumpHeight = 1.05f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float coyoteTime = 0.12f;
        [SerializeField] float jumpBuffer = 0.12f;
        [SerializeField] float slideSpeed = 7f;

        [Header("Body heights (m)")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.2f;
        [SerializeField] float proneHeight = 0.6f;
        [SerializeField] float stanceChangeSpeed = 6f;

        [Header("Turning & lean")]
        [SerializeField] float turnSharpness = 14f;
        [SerializeField] float leanSpeed = 7f;

        // ---------- read-only state ----------
        public Stance Stance { get; private set; } = Stance.Stand;
        public bool IsSprinting { get; private set; }
        public bool IsWalking { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsMoving => PlanarVelocity.sqrMagnitude > 0.05f;
        public Vector3 PlanarVelocity { get; private set; }
        /// <summary>Velocity in the character's local space (x = right, y = forward) – for blend trees.</summary>
        public Vector2 LocalMove { get; private set; }
        public float Lean { get; private set; }               // -1 … 1
        const float SlideTime = 0.8f; float slideT; Vector3 slideDir;
        public bool IsSliding => slideT > 0f;
        public float EyeHeight => controller.height - 0.15f;
        public float SpeedMultiplier { get; set; } = 1f;      // e.g. weapon weight, boosts
        public bool SprintLocked;                             // out of stamina / leg wound (KryvonVitals)
        public float InjuryMul = 1f;                          // limping
        public float WaterY = float.MinValue;                 // the sea surface (set by KryvonVitals); below it you swim
        public bool IsSwimming { get; private set; }
        public event Action<float> OnLanded;                  // impact speed (m/s), for fall damage / sounds
        public event Action OnJumped;

        CharacterController controller;
        PlayerInputReader input;
        Health health;
        WeaponController weapons;
        float verticalVelocity, lastGroundedTime = -1f, lastJumpPressedTime = -1f, targetHeight;
        bool wasGrounded;

        void Awake()
        {
            // realistic tactical pacing (a soldier with kit): slow walk 1.5 m/s, jog 3.4, sprint 5.0 – the scene's older,
            // faster values are replaced here so every map and build gets the same feel
            walkSpeed = 1.2f; runSpeed = 2.6f; sprintSpeed = 3.8f; crouchSpeed = 1.1f; proneSpeed = 0.5f;   // owner: slower, heavier
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<Health>();
            weapons = GetComponent<WeaponController>();
            if (cameraRig == null && Camera.main != null) cameraRig = Camera.main.GetComponent<ThirdPersonCamera>();
            targetHeight = standHeight;
            ApplyHeight(standHeight);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (health != null && !health.IsAlive) return;
            bool knocked = health != null && health.IsKnocked;
            bool aiming = weapons != null ? weapons.IsAiming : input.AimHeld;

            // ---- stance ----
            if (knocked) Stance = Stance.Prone;
            else
            {
                // tactical slide: crouch while sprinting = drop into a slide that carries the sprint speed and bleeds it off
                if (input.CrouchPressed && IsSprinting && controller.isGrounded && Stance == Stance.Stand && slideT <= 0f)
                {
                    slideT = SlideTime; slideDir = PlanarVelocity.sqrMagnitude > 0.1f ? PlanarVelocity.normalized : transform.forward;
                    TrySetStance(Stance.Crouch); KryvonFX.Dust(transform.position + Vector3.up * 0.1f); CameraShake.Kick(0.12f);
                }
                else if (input.CrouchPressed) TrySetStance(Stance == Stance.Crouch ? Stance.Stand : Stance.Crouch);
                if (input.PronePressed) TrySetStance(Stance == Stance.Prone ? Stance.Stand : Stance.Prone);
            }
            targetHeight = Stance == Stance.Stand ? standHeight : Stance == Stance.Crouch ? crouchHeight : proneHeight;
            ApplyHeight(Mathf.MoveTowards(controller.height, targetHeight, stanceChangeSpeed * dt));

            // ---- direction (relative to the camera, or frozen during free look) ----
            float refYaw = cameraRig != null ? cameraRig.MoveReferenceYaw : transform.eulerAngles.y;
            Quaternion refRot = Quaternion.Euler(0f, refYaw, 0f);
            Vector2 m = input.Move;
            Vector3 wish = refRot * new Vector3(m.x, 0f, m.y);

            // ---- speed ----
            float angle = Vector2.Angle(Vector2.up, m);
            IsSprinting = input.SprintHeld && m.sqrMagnitude > 0.2f && angle <= sprintMaxAngle && Stance == Stance.Stand
                          && !aiming && !input.FireHeld && !knocked && !SprintLocked && !IsSwimming && (weapons == null || !weapons.IsReloadBlockingSprint);
            IsWalking = input.WalkHeld && !IsSprinting;
            float speed = knocked ? knockedSpeed
                        : Stance == Stance.Prone ? proneSpeed
                        : Stance == Stance.Crouch ? crouchSpeed
                        : IsSprinting ? sprintSpeed : IsWalking ? walkSpeed : runSpeed;
            if (!IsSprinting)
            {
                float fwd = Mathf.Clamp01(m.y), back = Mathf.Clamp01(-m.y), side = Mathf.Abs(m.x);
                float dirMul = (fwd + back * backwardMultiplier + side * strafeMultiplier) / Mathf.Max(0.0001f, fwd + back + side);
                speed *= dirMul;
            }
            if (IsSwimming) speed = 1.3f;                    // breaststroke
            if (aiming && !knocked) speed *= aimMultiplier;
            speed *= InjuryMul;
            speed *= SpeedMultiplier * KryvonClass.MoveMul * (weapons != null && weapons.Current != null ? weapons.Current.moveSpeedMultiplier : 1f);
            Vector3 targetVel = wish * speed;   // wish is ≤ 1 long, so analog sticks give analog speed
            if (slideT > 0f)
            {   // sliding: momentum along the slide direction, steering only a little
                slideT -= dt; IsSprinting = false;
                float k = Mathf.Clamp01(slideT / SlideTime);
                targetVel = Vector3.Slerp(slideDir, wish.sqrMagnitude > 0.01f ? wish.normalized : slideDir, 0.15f) * Mathf.Lerp(crouchSpeed, sprintSpeed * 1.35f, k);
                if (!controller.isGrounded || knocked) slideT = 0f;
            }

            // ---- accelerate ----
            IsGrounded = controller.isGrounded;
            // a body has weight: speeding up takes ~0.4 s, stopping ~0.3 s, a sprint builds up a little slower
            bool speeding = targetVel.sqrMagnitude > PlanarVelocity.sqrMagnitude;
            float accel = slideT > 0f ? 40f : IsGrounded ? (speeding ? (IsSprinting ? 9f : 12f) : 17f) : airAcceleration;
            PlanarVelocity = Vector3.MoveTowards(PlanarVelocity, targetVel, accel * dt);

            // ---- facing: camera direction while aiming/shooting/leaning, otherwise toward movement ----
            bool faceCamera = aiming || input.FireHeld || Mathf.Abs(Lean) > 0.05f || (weapons != null && weapons.IsReloading) || Stance == Stance.Prone;
            Vector3 face = faceCamera && !input.FreeLookHeld ? refRot * Vector3.forward : PlanarVelocity;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-(faceCamera ? turnSharpness : 9f) * dt));
            Vector3 local = transform.InverseTransformDirection(PlanarVelocity);
            LocalMove = new Vector2(local.x, local.z);

            // ---- lean (only standing/crouched, not while sprinting) ----
            float leanTarget = (Stance != Stance.Prone && !IsSprinting && !knocked) ? Mathf.Clamp(input.LeanInput, -1f, 1f) : 0f;
            Lean = Mathf.MoveTowards(Lean, leanTarget, leanSpeed * dt);

            // ---- jump, gravity, slopes ----
            if (IsGrounded) lastGroundedTime = Time.time;
            if (input.JumpPressed) lastJumpPressedTime = Time.time;
            bool canJump = Time.time - lastGroundedTime <= coyoteTime && verticalVelocity <= 0.1f && !knocked;
            if (Time.time - lastJumpPressedTime <= jumpBuffer && canJump)
            {
                lastJumpPressedTime = -1f;
                if (Stance != Stance.Stand) TrySetStance(Stance.Stand);   // first press stands you up
                else { verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); lastGroundedTime = -1f; OnJumped?.Invoke(); }
            }
            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            Vector3 slide = Vector3.zero;
            if (IsGrounded && Physics.SphereCast(transform.position + Vector3.up * (controller.radius + 0.05f), controller.radius * 0.9f, Vector3.down, out RaycastHit g, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(g.normal, Vector3.up);
                if (slope > controller.slopeLimit) slide = Vector3.ProjectOnPlane(Vector3.down, g.normal).normalized * slideSpeed;
            }

            // safety net: under the terrain (a glitch, a vehicle exit into a slope) → back on top
            if (Time.frameCount % 15 == 0 && Terrain.activeTerrain && controller.enabled)
            {
                var ter = Terrain.activeTerrain; float gy = ter.SampleHeight(transform.position) + ter.transform.position.y;
                bool onFloor = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var fh, 3f, ~0, QueryTriggerInteraction.Ignore) && !(fh.collider is TerrainCollider) && !fh.collider.transform.IsChildOf(transform);   // a cave / tomb floor cut below the hill is fine
                if (transform.position.y < gy - 0.6f && !onFloor) { controller.enabled = false; transform.position = new Vector3(transform.position.x, gy + 0.2f, transform.position.z); controller.enabled = true; verticalVelocity = 0; }
            }
            // ---- swimming: in the sea you float chest-deep at the surface instead of walking on the sea bed ----
            float swimY = WaterY - 1.3f;
            IsSwimming = WaterY > -1e6f && transform.position.y < swimY + 0.05f && !knocked;
            if (IsSwimming) { verticalVelocity = Mathf.Clamp((swimY - transform.position.y) * 3f, -1.5f, 2.5f); if (Stance != Stance.Stand) TrySetStance(Stance.Stand); }
            float fallSpeed = -verticalVelocity;
            controller.Move((PlanarVelocity + slide + Vector3.up * verticalVelocity) * dt);

            if (!wasGrounded && controller.isGrounded && fallSpeed > 2f) { OnLanded?.Invoke(fallSpeed); if (fallSpeed > 5f) CameraShake.Kick(Mathf.Clamp01((fallSpeed - 5f) / 14f) * 0.5f); }
            wasGrounded = controller.isGrounded;
        }

        public bool TrySetStance(Stance s)
        {
            if (s == Stance) return true;
            float h = s == Stance.Stand ? standHeight : s == Stance.Crouch ? crouchHeight : proneHeight;
            if (h > controller.height + 0.01f && !HasHeadroom(h)) return false;
            Stance = s;
            return true;
        }

        bool HasHeadroom(float height)
        {
            float r = controller.radius * 0.95f;
            Vector3 start = transform.position + Vector3.up * (controller.height - r);
            return !Physics.SphereCast(start, r, Vector3.up, out _, height - controller.height + 0.05f, ~0, QueryTriggerInteraction.Ignore);
        }

        void ApplyHeight(float h)
        {
            controller.height = h;
            controller.center = new Vector3(0f, h * 0.5f + controller.skinWidth, 0f);
        }
    }
}

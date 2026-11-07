// PlayerController – third-person movement: walk / sprint / crouch / jump, with gravity.
// Moves relative to where the camera looks. Faces the camera direction while aiming or shooting,
// otherwise turns toward the movement direction.
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The main camera (with ThirdPersonCamera). Movement is relative to its yaw.")]
        [SerializeField] Transform cameraTransform;

        [Header("Speeds (m/s)")]
        [SerializeField] float walkSpeed = 4.5f;
        [SerializeField] float sprintSpeed = 7.5f;
        [SerializeField] float crouchSpeed = 2.2f;
        [SerializeField] float knockedCrawlSpeed = 1.0f;
        [SerializeField, Range(0.2f, 1f)] float aimSpeedMultiplier = 0.6f;

        [Header("Jump & gravity")]
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -20f;

        [Header("Body")]
        [SerializeField] float standHeight = 2f;
        [SerializeField] float crouchHeight = 1.2f;
        [SerializeField] float turnSpeed = 12f;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public bool IsAiming => input.AimHeld;
        /// <summary>Horizontal speed this frame – handy for animation blend trees.</summary>
        public float PlanarSpeed { get; private set; }

        CharacterController controller;
        PlayerInputReader input;
        Health health;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            health = GetComponent<Health>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            SetHeight(standHeight);
        }

        void Update()
        {
            if (health != null && !health.IsAlive) return;
            bool knocked = health != null && health.IsKnocked;

            if (input.CrouchPressed && !knocked) TryCrouch(!IsCrouching);

            // camera-relative directions on the ground plane
            Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            Vector3 move = forward * input.Move.y + right * input.Move.x;
            if (move.sqrMagnitude > 1f) move.Normalize();

            IsSprinting = input.SprintHeld && input.Move.y > 0.1f && !IsCrouching && !input.AimHeld && !input.FireHeld && !knocked;
            float speed = knocked ? knockedCrawlSpeed : IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            if (input.AimHeld && !knocked) speed *= aimSpeedMultiplier;

            // facing
            Vector3 face = (input.AimHeld || input.FireHeld) ? forward : move;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));

            // jump & gravity
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f; // keeps us glued to slopes
            if (input.JumpPressed && controller.isGrounded && !knocked)
            {
                if (IsCrouching) TryCrouch(false);                       // first press stands up
                else verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
            PlanarSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;
        }

        void TryCrouch(bool crouch)
        {
            if (!crouch)
            {
                // don't stand up under a low ceiling
                float r = controller.radius * 0.95f;
                Vector3 bottom = transform.position + Vector3.up * (crouchHeight - r);
                if (Physics.SphereCast(bottom, r, Vector3.up, out _, standHeight - crouchHeight, ~0, QueryTriggerInteraction.Ignore)) return;
            }
            IsCrouching = crouch;
            SetHeight(crouch ? crouchHeight : standHeight);
        }

        void SetHeight(float h)
        {
            controller.height = h;
            controller.center = new Vector3(0f, h * 0.5f, 0f);
        }
    }
}

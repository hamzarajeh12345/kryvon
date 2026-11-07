// ThirdPersonCamera – over-the-shoulder orbit camera.
// Put it on the Main Camera and drag the Player into "Target".
// Tip: put the player on its own layer ("Player") and remove that layer from Collision Mask.
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerInputReader input;

        [Header("Sensitivity (degrees per mouse pixel)")]
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField, Range(0.1f, 1.5f)] float aimSensitivityMultiplier = 0.6f;
        [SerializeField] bool invertY;

        [Header("Framing")]
        [SerializeField] Vector3 shoulderOffset = new Vector3(0.55f, 1.65f, 0f);
        [SerializeField] float distance = 3.6f;
        [SerializeField] float aimDistance = 1.7f;
        [SerializeField] float fov = 70f;
        [SerializeField] float aimFov = 48f;
        [SerializeField] float minPitch = -70f, maxPitch = 75f;

        [Header("Collision")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionRadius = 0.2f;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public bool Aiming { get; private set; }

        Camera cam;
        float yaw, pitch, currentDistance;
        Vector2 recoil;              // pending recoil, applied smoothly

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target != null && input == null) input = target.GetComponent<PlayerInputReader>();
            yaw = target != null ? target.eulerAngles.y : 0f;
            currentDistance = distance;
        }

        /// <summary>Called by WeaponController after each shot (degrees).</summary>
        public void AddRecoil(float up, float side) => recoil += new Vector2(side, up);

        public void SetSensitivity(float degreesPerPixel) => sensitivity = degreesPerPixel;

        void LateUpdate()
        {
            if (target == null) return;
            Aiming = input != null && input.AimHeld;

            Vector2 look = input != null ? input.Look : Vector2.zero;
            float s = sensitivity * (Aiming ? aimSensitivityMultiplier : 1f);
            Vector2 kick = recoil * Mathf.Min(1f, Time.deltaTime * 25f);
            recoil -= kick;
            yaw += look.x * s + kick.x;
            pitch = Mathf.Clamp(pitch + look.y * s * (invertY ? -1f : 1f) + kick.y, minPitch, maxPitch);

            Quaternion rot = Quaternion.Euler(-pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * shoulderOffset.y + Quaternion.Euler(0f, yaw, 0f) * new Vector3(shoulderOffset.x, 0f, shoulderOffset.z);
            Vector3 back = rot * Vector3.back;

            // pull the camera in front of walls instead of clipping through them
            float wanted = Aiming ? aimDistance : distance;
            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, wanted, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.2f, hit.distance - 0.05f);
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-10f * Time.deltaTime));

            transform.SetPositionAndRotation(pivot + back * currentDistance, rot);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Aiming ? aimFov : fov, 1f - Mathf.Exp(-14f * Time.deltaTime));
        }
    }
}

// ThirdPersonCamera – over-the-shoulder camera with exact, situation-based sensitivity.
// Put it on the Main Camera and drag the Player into "Target".
//  • Sensitivity comes from SensitivityService (camera / free look / parachute / vehicle / shoulder zoom /
//    per-scope ADS & firing, × weapon class × this weapon, ÷ scope zoom)
//  • Free look (Alt): orbit freely, snaps back on release; movement keeps its direction
//  • Follows stance height smoothly, shifts with lean, pulls in front of walls
//  • Recoil: kicks are applied over a few frames; part of the climb settles back after you stop shooting
//  • Gyroscope on phones (off / scope only / always)
// Tip: put the player on a "Player" layer and remove it from Collision Mask.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public enum CameraMode { OnFoot, Parachute, Vehicle }

    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Framing (on foot)")]
        [SerializeField] float shoulderX = 0.55f;
        [SerializeField] float distance = 3.4f;
        [SerializeField] float shoulderZoomDistance = 2.2f;
        [SerializeField] float adsDistance = 1.2f;
        [SerializeField] float leanShift = 0.6f;
        [SerializeField] float leanRoll = 6f;
        [SerializeField] float minPitch = -75f, maxPitch = 80f;
        [SerializeField] float parachuteDistance = 9f, vehicleDistance = 7f;

        [Header("Smoothing")]
        [SerializeField] float positionSharpness = 18f;
        [SerializeField] float fovSharpness = 16f;
        [SerializeField] float freeLookReturnSpeed = 540f; // deg/s

        [Header("Collision")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionRadius = 0.2f;

        public CameraMode Mode { get; set; } = CameraMode.OnFoot;
        public float Yaw => yaw + freeYaw;
        public float Pitch => pitch;
        /// <summary>The yaw movement is relative to (frozen during free look).</summary>
        public float MoveReferenceYaw => yaw;
        public float CurrentDegreesPerPixel { get; private set; }
        public SensContext CurrentContext { get; private set; }

        Camera cam;
        PlayerInputReader input;
        PlayerController body;
        WeaponController weapons;
        float yaw, pitch, freeYaw, freePitch, currentDistance, recoilPool, eyeHeight = 1.65f;
        Vector2 pendingRecoil;
        float lastRecoilTime;
        Vector3 smoothedPivot;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (target != null) SetTarget(target);
            if (UnityEngine.InputSystem.Gyroscope.current != null) InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
        }

        public void SetTarget(Transform t)
        {
            target = t;
            input = t.GetComponent<PlayerInputReader>();
            body = t.GetComponent<PlayerController>();
            weapons = t.GetComponent<WeaponController>();
            yaw = t.eulerAngles.y;
            smoothedPivot = t.position + Vector3.up * eyeHeight;
            currentDistance = distance;
        }

        /// <summary>Called by WeaponController for each shot (degrees): x = sideways, y = up.</summary>
        public void AddRecoil(Vector2 kick) { pendingRecoil += kick; lastRecoilTime = Time.time; }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            var S = SensitivityService.Current;

            // ---------- which sensitivity applies right now ----------
            bool ads = weapons != null && weapons.IsAds;
            bool shoulder = weapons != null && weapons.IsShoulderZoom;
            bool freeLook = input != null && input.FreeLookHeld && !ads;
            ScopeType scope = weapons != null ? weapons.CurrentScope : ScopeType.None;
            CurrentContext = Mode == CameraMode.Parachute ? SensContext.Parachute
                           : Mode == CameraMode.Vehicle ? SensContext.Vehicle
                           : ads ? SensContext.Ads
                           : freeLook ? SensContext.FreeLook
                           : shoulder ? SensContext.ShoulderZoom : SensContext.Camera;
            bool firing = input != null && input.FireHeld;
            CurrentDegreesPerPixel = S.DegreesPerPixel(CurrentContext, scope, weapons != null ? weapons.Current : null, firing);

            // ---------- look input (+ gyro) ----------
            Vector2 look = input != null ? input.Look * CurrentDegreesPerPixel : Vector2.zero;
            var gyro = UnityEngine.InputSystem.Gyroscope.current;
            if (gyro != null && (S.gyroMode == GyroMode.Always || (S.gyroMode == GyroMode.ScopeOnly && ads)))
            {
                Vector3 w = gyro.angularVelocity.ReadValue() * Mathf.Rad2Deg * dt * S.GyroDegreesScale(ads, scope);
                look += new Vector2(-w.y, w.x);
            }
            if (S.invertY) look.y = -look.y;

            if (freeLook) { freeYaw += look.x; freePitch = Mathf.Clamp(freePitch + look.y, minPitch - pitch, maxPitch - pitch); }
            else
            {
                yaw += look.x; pitch = Mathf.Clamp(pitch + look.y, minPitch, maxPitch);
                freeYaw = Mathf.MoveTowardsAngle(freeYaw, 0f, freeLookReturnSpeed * dt);
                freePitch = Mathf.MoveTowards(freePitch, 0f, freeLookReturnSpeed * dt);
            }

            // ---------- recoil: apply over a few frames, settle part of it back afterwards ----------
            Vector2 kick = pendingRecoil * Mathf.Min(1f, dt * 30f);
            pendingRecoil -= kick;
            yaw += kick.x; pitch = Mathf.Clamp(pitch + kick.y, minPitch, maxPitch);
            recoilPool += kick.y;
            if (Time.time - lastRecoilTime > 0.12f && recoilPool > 0f)
            {
                float recovery = weapons != null && weapons.Current != null ? weapons.Current.recoilRecovery : 0.5f;
                float back = Mathf.Min(recoilPool, recoilPool * 8f * dt);
                pitch -= back * recovery; recoilPool -= back;
            }
            else if (input != null && Mathf.Abs(input.Look.y) > 0.5f) recoilPool = Mathf.Max(0f, recoilPool - Mathf.Abs(look.y)); // player pulled down themselves

            // ---------- framing ----------
            float lean = body != null ? body.Lean : 0f;
            eyeHeight = Mathf.Lerp(eyeHeight, body != null ? body.EyeHeight : 1.65f, 1f - Mathf.Exp(-12f * dt));
            float wanted = Mode == CameraMode.Parachute ? parachuteDistance : Mode == CameraMode.Vehicle ? vehicleDistance
                         : ads ? adsDistance : shoulder ? shoulderZoomDistance : distance;
            Quaternion yawRot = Quaternion.Euler(0f, yaw + freeYaw, 0f);
            Vector3 pivotTarget = target.position + Vector3.up * eyeHeight + yawRot * new Vector3(shoulderX + lean * leanShift, 0f, 0f);
            smoothedPivot = Vector3.Lerp(smoothedPivot, pivotTarget, 1f - Mathf.Exp(-positionSharpness * dt));

            Quaternion rot = Quaternion.Euler(-(pitch + freePitch), yaw + freeYaw, -lean * leanRoll);
            Vector3 back3 = rot * Vector3.back;
            if (Physics.SphereCast(smoothedPivot, collisionRadius, back3, out RaycastHit hit, wanted, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.15f, hit.distance - 0.05f);
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-10f * dt));
            transform.SetPositionAndRotation(smoothedPivot + back3 * currentDistance, rot);

            // ---------- field of view ----------
            float baseFov = S.fieldOfView;
            float fov = ads ? baseFov / Scopes.Zoom(scope) : shoulder ? baseFov * 0.85f : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-fovSharpness * dt));
        }
    }
}

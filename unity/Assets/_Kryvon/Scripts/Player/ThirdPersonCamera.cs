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
        /// looking through a 2x or stronger sight (the HUD draws the scope)
        public bool IsScoped { get; private set; }
        /// first-person match (lobby PERSPECTIVE = FPP): the camera sits in the head on foot
        public static bool FPP => PlayerPrefs.GetInt("kryvon.fpp", 0) == 1;
        bool fppCached; bool fppRead; int fppSeen = -1; static int fppVersion;
        /// in-match view switch (the eye button / V key): flips TPP ⇄ FPP straight away
        public static void ToggleView() { PlayerPrefs.SetInt("kryvon.fpp", FPP ? 0 : 1); PlayerPrefs.Save(); fppVersion++; }

        // ---------- aim assist (touch): slows the crosshair over an enemy and drifts gently toward the chest ----------
        public static bool AimAssistOn
        {
            get => PlayerPrefs.GetInt("kryvon.aimassist", KryvonTouchControls.Wanted ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt("kryvon.aimassist", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public KryvonBot AssistTarget { get; private set; }
        float assistCheck; Health selfHp;
        Vector2 AimAssist(Vector2 look, bool ads, bool firing, float dt)
        {
            if (!selfHp && target) selfHp = target.GetComponent<Health>();
            if (Time.time >= assistCheck)
            {   // pick the enemy nearest the crosshair, in sight, inside a small cone (wider up close)
                assistCheck = Time.time + 0.1f; AssistTarget = null; float best = 999f;
                Vector3 eye = transform.position, fwd = transform.forward;
                foreach (var b in KryvonBot.All)
                {
                    if (!b || !b.Hp || !b.Hp.IsAlive || (selfHp && b.Hp.teamId == selfHp.teamId)) continue;
                    Vector3 chest = b.transform.position + Vector3.up * 1.25f, to = chest - eye; float d = to.magnitude;
                    if (d > 160f || d < 0.5f) continue;
                    float ang = Vector3.Angle(fwd, to), cone = Mathf.Clamp(Mathf.Atan2(1.2f, d) * Mathf.Rad2Deg * 2.2f, 1.2f, 9f);
                    if (ang > cone || ang / cone >= best) continue;
                    Vector3 from = smoothedPivot + (chest - smoothedPivot).normalized * 0.7f;   // from the head, past our own body
                    if (Physics.Linecast(from, chest, out var h, collisionMask, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(b.transform)) continue;
                    best = ang / cone; AssistTarget = b;
                }
            }
            if (!AssistTarget) return look;
            Vector3 c = AssistTarget.transform.position + Vector3.up * 1.25f - transform.position;
            float cone2 = Mathf.Clamp(Mathf.Atan2(1.2f, c.magnitude) * Mathf.Rad2Deg * 2.2f, 1.2f, 9f);
            float near = 1f - Mathf.Clamp01(Vector3.Angle(transform.forward, c) / cone2);
            look *= Mathf.Lerp(1f, ads ? 0.55f : 0.7f, near);                        // friction: the crosshair sticks a little
            if (firing || (input != null && input.Look.sqrMagnitude > 0.5f))          // pull only while the player is aiming/shooting, never alone
            {
                float wantYaw = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg, wantPitch = Mathf.Asin(Mathf.Clamp(c.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float rate = (ads ? 7f : 4.5f) * (0.4f + near) * dt;                  // degrees per second: gentle
                yaw = Mathf.MoveTowardsAngle(yaw, wantYaw - freeYaw, rate);
                pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(wantPitch - freePitch, minPitch, maxPitch), rate);
            }
            return look;
        }
        Renderer[] bodyRenderers; bool bodyHidden; float bobPhase, bobAmp;
        void HideBody(bool hide)
        {
            if (hide == bodyHidden || target == null) return;
            bodyHidden = hide;
            if (hide) bodyRenderers = target.GetComponentsInChildren<Renderer>();
            if (bodyRenderers != null) foreach (var r in bodyRenderers) if (r) r.forceRenderingOff = hide;
        }
        /// <summary>> 0 = fixed camera distance (plane, freefall, parachute).</summary>
        public float DistanceOverride { get; set; } = -1f;
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
            if (input != null && input.ViewTogglePressed) { ToggleView(); KryvonKillFeed.Notice(FPP ? "FIRST-PERSON VIEW" : "THIRD-PERSON VIEW"); }

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
            if (AimAssistOn && Mode == CameraMode.OnFoot && weapons != null && !weapons.IsMelee && (ads || shoulder || firing))
                look = AimAssist(look, ads, firing, dt);

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
            bool scoped = ads && Mode == CameraMode.OnFoot && Scopes.Zoom(scope) >= 2f;   // 2x–8x look through the scope; iron sight / red dot / holo stay over the shoulder
            if (!fppRead || fppSeen != fppVersion) { fppCached = FPP; fppRead = true; fppSeen = fppVersion; }
            bool fp = fppCached && Mode == CameraMode.OnFoot && DistanceOverride <= 0f;
            IsScoped = scoped; HideBody(scoped || fp);
            eyeHeight = Mathf.Lerp(eyeHeight, body != null ? body.EyeHeight : 1.65f, 1f - Mathf.Exp(-12f * dt));
            float wanted = Mode == CameraMode.Parachute ? parachuteDistance : Mode == CameraMode.Vehicle ? vehicleDistance
                         : ads ? adsDistance : shoulder ? shoulderZoomDistance : distance;
            if (DistanceOverride > 0f) wanted = DistanceOverride;
            if (scoped || fp) wanted = 0f;
            Quaternion yawRot = Quaternion.Euler(0f, yaw + freeYaw, 0f);
            Vector3 pivotTarget = target.position + Vector3.up * eyeHeight + yawRot * new Vector3((scoped || fp ? 0.1f : shoulderX) + lean * leanShift, 0f, scoped || fp ? 0.15f : 0f);
            smoothedPivot = scoped || fp ? pivotTarget : Vector3.Lerp(smoothedPivot, pivotTarget, 1f - Mathf.Exp(-positionSharpness * dt));

            // step sway: grows with speed, nothing while aiming / scoped / in the air
            float spd = body != null && body.IsGrounded && Mode == CameraMode.OnFoot && !ads && !scoped ? body.PlanarVelocity.magnitude : 0f;
            bobAmp = Mathf.Lerp(bobAmp, Mathf.Clamp01(spd / 6.4f), 1f - Mathf.Exp(-6f * dt));
            bobPhase += dt * Mathf.Lerp(7f, 11f, Mathf.Clamp01(spd / 6.4f));
            smoothedPivot += Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) - 0.5f) * 0.05f * bobAmp + (Quaternion.Euler(0, yaw, 0) * Vector3.right) * Mathf.Sin(bobPhase * 0.5f) * 0.03f * bobAmp;
            Quaternion rot = Quaternion.Euler(-(pitch + freePitch), yaw + freeYaw, -lean * leanRoll + Mathf.Sin(bobPhase * 0.5f) * 0.6f * bobAmp);
            Vector3 back3 = rot * Vector3.back;
            if (Physics.SphereCast(smoothedPivot, collisionRadius, back3, out RaycastHit hit, wanted, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = scoped || fp ? 0f : Mathf.Max(0.15f, hit.distance - 0.05f);
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-10f * dt));
            transform.SetPositionAndRotation(smoothedPivot + back3 * currentDistance, rot);

            // ---------- field of view ----------
            float baseFov = S.fieldOfView;
            float fov = ads ? baseFov / Scopes.Zoom(scope) : shoulder ? baseFov * 0.85f : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-fovSharpness * dt));
        }
    }
}

// PlayerInputReader – keyboard/mouse + gamepad through the new Input System, built in code (no asset needed).
// Other scripts read simple values from here. For mobile, add On-Screen Stick / Button components bound to
// the gamepad paths below (leftStick, rightStick, buttonSouth …) or call the Touch* methods from your own UI.
//
// Default keys:  WASD move · Mouse look · Shift sprint · Ctrl walk · Space jump · C crouch · Z prone
//                Q/E lean · Left Alt free look · LMB fire · RMB aim · R reload · F interact
//                1/2 weapons · mouse wheel switch · B fire mode
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Gamepad right-stick speed in pixels per second at full tilt (matched to mouse feel).")]
        [SerializeField] float stickLookPixelsPerSecond = 900f;
        [SerializeField] bool lockCursor = true;

        public Vector2 Move { get; private set; }
        /// <summary>Look delta this frame in input pixels (mouse + stick + touch). Multiply by degrees/pixel.</summary>
        public Vector2 Look { get; private set; }
        public bool LookFromTouch { get; private set; }

        public bool SprintHeld => sprint.IsPressed() || touchSprint;
        public bool WalkHeld => walk.IsPressed();
        public bool FireHeld => fire.IsPressed() || touchFire;
        public bool FirePressed => fire.WasPressedThisFrame() || (touchFire && !touchFirePrev);
        public bool AimHeld => aim.IsPressed() || touchAim;
        public bool AimPressed => aim.WasPressedThisFrame() || (touchAim && !touchAimPrev);
        public bool AimReleased => aim.WasReleasedThisFrame() || (!touchAim && touchAimPrev);
        public bool JumpPressed => jump.WasPressedThisFrame() || Consume(ref touchJump);
        public bool CrouchPressed => crouch.WasPressedThisFrame() || Consume(ref touchCrouch);
        public bool PronePressed => prone.WasPressedThisFrame() || Consume(ref touchProne);
        public bool ReloadPressed => reload.WasPressedThisFrame() || Consume(ref touchReload);
        public bool InteractPressed => interact.WasPressedThisFrame() || Consume(ref touchInteract);
        public bool FireModePressed => fireMode.WasPressedThisFrame();
        public bool FreeLookHeld => freeLook.IsPressed() || touchFreeLook;
        /// <summary>-1 = lean left, +1 = lean right, 0 = none.</summary>
        public float LeanInput => (leanRight.IsPressed() ? 1f : 0f) - (leanLeft.IsPressed() ? 1f : 0f) + touchLean;
        /// <summary>0 or 1 when a weapon slot key was pressed this frame, -1 otherwise. ±10 for next/previous.</summary>
        public int WeaponSwitch { get; private set; } = -1;

        InputAction move, mouseLook, stickLook, jump, sprint, walk, crouch, prone, fire, aim, reload, interact,
                    leanLeft, leanRight, freeLook, slot1, slot2, scroll, fireMode;

        // ---- hooks for an on-screen touch UI ----
        bool touchFire, touchFirePrev, touchAim, touchAimPrev, touchSprint, touchFreeLook, touchJump, touchCrouch, touchProne, touchReload, touchInteract;
        float touchLean; Vector2 touchMove, touchLook;
        public void TouchMove(Vector2 v) => touchMove = Vector2.ClampMagnitude(v, 1f);
        public void TouchLook(Vector2 pixels) => touchLook += pixels;
        public void TouchFire(bool down) => touchFire = down;
        public void TouchAim(bool down) => touchAim = down;
        public void TouchSprint(bool on) => touchSprint = on;
        public void TouchFreeLook(bool on) => touchFreeLook = on;
        public void TouchLean(float v) => touchLean = Mathf.Clamp(v, -1f, 1f);
        public void TouchJump() => touchJump = true;
        public void TouchCrouch() => touchCrouch = true;
        public void TouchProne() => touchProne = true;
        public void TouchReload() => touchReload = true;
        public void TouchInteract() => touchInteract = true;
        static bool Consume(ref bool flag) { bool v = flag; flag = false; return v; }

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            mouseLook = new InputAction("MouseLook", InputActionType.Value, "<Mouse>/delta");
            stickLook = new InputAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick");

            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            walk = Button("Walk", "<Keyboard>/leftCtrl", null);
            crouch = Button("Crouch", "<Keyboard>/c", "<Gamepad>/buttonEast");
            prone = Button("Prone", "<Keyboard>/z", "<Gamepad>/rightStickPress");
            fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            aim = Button("Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            interact = Button("Interact", "<Keyboard>/f", "<Gamepad>/buttonNorth");
            leanLeft = Button("LeanLeft", "<Keyboard>/q", "<Gamepad>/dpad/left");
            leanRight = Button("LeanRight", "<Keyboard>/e", "<Gamepad>/dpad/right");
            freeLook = Button("FreeLook", "<Keyboard>/leftAlt", "<Gamepad>/dpad/up");
            slot1 = Button("Slot1", "<Keyboard>/1", null);
            slot2 = Button("Slot2", "<Keyboard>/2", "<Gamepad>/rightShoulder");
            fireMode = Button("FireMode", "<Keyboard>/b", "<Gamepad>/dpad/down");
            scroll = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");
        }

        static InputAction Button(string name, string key, string pad)
        {
            var a = new InputAction(name, InputActionType.Button, key);
            if (pad != null) a.AddBinding(pad);
            return a;
        }

        InputAction[] All => new[] { move, mouseLook, stickLook, jump, sprint, walk, crouch, prone, fire, aim, reload, interact, leanLeft, leanRight, freeLook, slot1, slot2, scroll, fireMode };

        void OnEnable()
        {
            foreach (var a in All) a.Enable();
            if (lockCursor) SetCursorLocked(true);
        }
        void OnDisable() { foreach (var a in All) a.Disable(); }
        void OnDestroy() { foreach (var a in All) a.Dispose(); }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>() + touchMove, 1f);

            var sens = SensitivityService.Current;
            Vector2 stick = stickLook.ReadValue<Vector2>();
            stick *= stick.magnitude;                                        // response curve: fine aim near the centre
            Vector2 touch = touchLook * (sens.touchMultiplier / 100f);
            LookFromTouch = touchLook.sqrMagnitude > 0f;
            Look = mouseLook.ReadValue<Vector2>() + stick * stickLookPixelsPerSecond * Time.deltaTime + touch;
            touchLook = Vector2.zero;

            WeaponSwitch = slot1.WasPressedThisFrame() ? 0 : slot2.WasPressedThisFrame() ? 1 : -1;
            float s = scroll.ReadValue<float>();
            if (WeaponSwitch < 0 && Mathf.Abs(s) > 0.01f) WeaponSwitch = s > 0 ? -10 : 10;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            if (lockCursor && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
        }

        void LateUpdate() { touchFirePrev = touchFire; touchAimPrev = touchAim; }
    }
}

// PlayerInputReader – reads keyboard/mouse and gamepad through the new Input System.
// Actions are created in code, so no .inputactions asset is needed to get started.
// Other scripts read simple values from here (Move, Look, FireHeld, JumpPressed …).
// Mobile: add On-Screen Stick / On-Screen Button components bound to <Gamepad>/leftStick etc.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Degrees per second for the gamepad right stick at full tilt.")]
        [SerializeField] float stickLookSpeed = 160f;
        [Tooltip("Lock and hide the mouse cursor while playing.")]
        [SerializeField] bool lockCursor = true;

        public Vector2 Move { get; private set; }
        /// <summary>Look delta for this frame, in "mouse pixels" (already frame-rate independent).</summary>
        public Vector2 Look { get; private set; }
        public bool SprintHeld => sprint.IsPressed();
        public bool FireHeld => fire.IsPressed();
        public bool AimHeld => aim.IsPressed();
        public bool FirePressed => fire.WasPressedThisFrame();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool CrouchPressed => crouch.WasPressedThisFrame();
        public bool ReloadPressed => reload.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();

        InputAction move, mouseLook, stickLook, jump, sprint, crouch, fire, aim, reload, interact;

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
            crouch = Button("Crouch", "<Keyboard>/c", "<Gamepad>/buttonEast");
            fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            aim = Button("Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
            reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
            interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");
        }

        static InputAction Button(string name, string key, string pad)
        {
            var a = new InputAction(name, InputActionType.Button, key);
            a.AddBinding(pad);
            return a;
        }

        InputAction[] All => new[] { move, mouseLook, stickLook, jump, sprint, crouch, fire, aim, reload, interact };

        void OnEnable()
        {
            foreach (var a in All) a.Enable();
            if (lockCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        void OnDisable() { foreach (var a in All) a.Disable(); }
        void OnDestroy() { foreach (var a in All) a.Dispose(); }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            // mouse delta is already "per frame"; the stick is a rate, so scale it by time
            Look = mouseLook.ReadValue<Vector2>() + stickLook.ReadValue<Vector2>() * stickLookSpeed * Time.deltaTime * 8f;

            // Esc frees the mouse, clicking the game window grabs it again
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (lockCursor && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }
}

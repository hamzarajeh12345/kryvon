// KryvonDebugHUD – quick on-screen HUD (IMGUI) for testing before the real UI exists:
// crosshair that grows with spread, health, ammo, stance, current sensitivity and the zone timer.
// F1 toggles a small sensitivity panel so you can tune the camera / scopes / this weapon while playing.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class KryvonDebugHUD : MonoBehaviour
    {
        [SerializeField] Health player;
        [SerializeField] WeaponController weapons;
        [SerializeField] PlayerController body;
        [SerializeField] ThirdPersonCamera cameraRig;
        [SerializeField] SafeZone zone;

        bool panel;
        float hitMarkerUntil;
        GUIStyle big, small;

        void Start()
        {
            if (weapons != null) weapons.OnHit += (_, _, head) => hitMarkerUntil = Time.time + (head ? 0.25f : 0.12f);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                panel = !panel;
                var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
                if (input != null) input.SetCursorLocked(!panel);
            }
        }

        void OnGUI()
        {
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            float w = Screen.width, h = Screen.height;

            // crosshair (hidden while scoped in with a magnified sight)
            if (weapons != null && !(weapons.IsAds && Scopes.Zoom(weapons.CurrentScope) >= 2f))
            {
                float gap = 6f + (weapons.CurrentSpread * 9f);
                Color c = Time.time < hitMarkerUntil ? Color.red : Color.white;
                Bar(w / 2 - 1, h / 2 - gap - 8, 2, 8, c); Bar(w / 2 - 1, h / 2 + gap, 2, 8, c);
                Bar(w / 2 - gap - 8, h / 2 - 1, 8, 2, c); Bar(w / 2 + gap, h / 2 - 1, 8, 2, c);
            }
            else if (weapons != null)
            {   // simple scope overlay
                Bar(w / 2 - 0.5f, h * 0.2f, 1, h * 0.6f, Color.black); Bar(w * 0.3f, h / 2 - 0.5f, w * 0.4f, 1, Color.black);
            }

            // health
            if (player != null)
            {
                float hp = Mathf.Clamp01(player.Current / player.Max);
                Bar(w / 2 - 160, h - 40, 320, 14, new Color(0, 0, 0, 0.5f));
                Bar(w / 2 - 158, h - 38, 316 * hp, 10, player.IsKnocked ? Color.red : hp > 0.6f ? Color.white : hp > 0.3f ? new Color(1f, 0.7f, 0.3f) : Color.red);
                if (!player.IsAlive) GUI.Label(new Rect(w / 2 - 80, h / 2 - 60, 300, 40), "ELIMINATED", big);
                else if (player.IsKnocked) GUI.Label(new Rect(w / 2 - 70, h / 2 + 40, 300, 40), "KNOCKED", big);
            }

            // weapon & ammo
            if (weapons != null && weapons.Current != null)
            {
                var s = weapons.CurrentSlot;
                GUI.Label(new Rect(w - 300, h - 90, 290, 30), $"{weapons.Current.displayName}  [{s.Mode}]", small);
                GUI.Label(new Rect(w - 300, h - 66, 290, 40), $"{s.ammo} / {weapons.Reserve(weapons.Current.ammoType)}" + (weapons.IsReloading ? "  reloading…" : ""), big);
                GUI.Label(new Rect(w - 300, h - 112, 290, 30), $"Sight: {weapons.CurrentScope}  {(weapons.IsAds ? "ADS" : weapons.IsShoulderZoom ? "Shoulder" : "Hip")}", small);
            }

            // status line
            string stance = body != null ? body.Stance + (body.IsSprinting ? " · sprint" : body.IsWalking ? " · walk" : "") + (Mathf.Abs(body.Lean) > 0.1f ? $" · lean {body.Lean:+0.0;-0.0}" : "") : "";
            string sens = cameraRig != null ? $"{cameraRig.CurrentContext}  {cameraRig.CurrentDegreesPerPixel:0.0000}°/px" : "";
            GUI.Label(new Rect(12, h - 60, 600, 24), stance, small);
            GUI.Label(new Rect(12, h - 38, 600, 24), sens + "   (F1: sensitivity)", small);

            // zone
            if (zone != null && zone.Running)
                GUI.Label(new Rect(w / 2 - 150, 12, 400, 30), $"Al-Tawq {(zone.Shrinking ? "closing" : "closes in")} {Mathf.CeilToInt(zone.Timer)}s · phase {zone.PhaseIndex + 1}" + (player != null && !zone.IsInside(player.transform.position) ? "  — OUTSIDE!" : ""), small);

            if (panel) SensitivityPanel();
        }

        Vector2 scroll;
        void SensitivityPanel()
        {
            var S = SensitivityService.Current;
            GUILayout.BeginArea(new Rect(12, 12, 360, Screen.height - 110), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Sensitivity (%) – changes apply live", small);
            S.camera = Slider("Camera", S.camera);
            S.freeLook = Slider("Free look", S.freeLook);
            S.shoulderZoom = Slider("Shoulder zoom", S.shoulderZoom);
            foreach (var sc in S.scopes) { sc.ads = Slider($"{sc.scope} ADS", sc.ads); sc.firing = Slider($"{sc.scope} firing", sc.firing); }
            if (weapons != null && weapons.Current != null)
            {
                float cur = S.WeaponMultiplier(weapons.Current.weaponId) * 100f;
                float nv = Slider($"This weapon ({weapons.Current.weaponId})", cur, 10, 200);
                if (!Mathf.Approximately(nv, cur)) S.SetWeaponMultiplier(weapons.Current.weaponId, nv);
            }
            S.fieldOfView = Slider("Field of view", S.fieldOfView, 60, 100);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Aim: " + S.aimMode)) S.aimMode = (AimMode)(((int)S.aimMode + 1) % 3);
            if (GUILayout.Button("Invert Y: " + S.invertY)) S.invertY = !S.invertY;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) SensitivityService.Save();
            if (GUILayout.Button("Defaults")) SensitivityService.ResetToDefaults();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static float Slider(string label, float v, float min = 1, float max = 300)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170));
            v = Mathf.Round(GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(120)));
            GUILayout.Label(v.ToString("0"), GUILayout.Width(36));
            GUILayout.EndHorizontal();
            return v;
        }

        static void Bar(float x, float y, float bw, float bh, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, bw, bh), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

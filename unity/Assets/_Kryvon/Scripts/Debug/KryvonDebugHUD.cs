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

        static GUIStyle zoomStyle;
        /// crosshair (hip fire), iron sight / red dot / scope (aiming); called by KryvonHUD
        public static void DrawAim(WeaponController weapons, float hitMarkerUntil = 0f)
        {
            if (weapons == null || KryvonTrailer.HideHud) return;
            float w = Screen.width, h = Screen.height;
            if (!weapons.IsAds)
            {
                if (weapons.IsMelee) { Bar(w / 2 - 2, h / 2 - 2, 4, 4, new Color(1, 1, 1, 0.7f)); return; }
                float gap = 6f + (weapons.CurrentSpread * 9f);
                Color c = Time.time < hitMarkerUntil ? Color.red : Color.white;
                Bar(w / 2 - 1, h / 2 - gap - 8, 2, 8, c); Bar(w / 2 - 1, h / 2 + gap, 2, 8, c);
                Bar(w / 2 - gap - 8, h / 2 - 1, 8, 2, c); Bar(w / 2 + gap, h / 2 - 1, 8, 2, c);
                return;
            }
            float zoom = Scopes.Zoom(weapons.CurrentScope);
            if (zoom >= 2f) DrawScopeS(w, h, zoom);
            else if (weapons.CurrentScope == ScopeType.None) DrawIronSightS(w, h);
            else { GUI.color = new Color(1f, 0.15f, 0.1f, 0.95f); GUI.DrawTexture(new Rect(w / 2 - 4, h / 2 - 4, 8, 8), Dot()); GUI.color = Color.white; }
        }
        static void DrawIronSightS(float w, float h)
        {
            float cx = w / 2, cy = h / 2, u = h / 1080f; var steel = new Color(0.06f, 0.06f, 0.065f, 0.97f);
            Bar(cx - 3 * u, cy, 6 * u, h * 0.16f, steel); Bar(cx - 26 * u, cy + h * 0.16f, 52 * u, 10 * u, steel);
            Bar(cx - 210 * u, cy + 14 * u, 175 * u, 70 * u, steel); Bar(cx + 35 * u, cy + 14 * u, 175 * u, 70 * u, steel);
            Bar(cx - 35 * u, cy + 60 * u, 70 * u, 24 * u, steel); Bar(0, h * 0.86f, w, h * 0.14f, new Color(0.04f, 0.04f, 0.045f, 0.9f));
        }
        static void DrawScopeS(float w, float h, float zoom)
        {
            float d = h * 1.02f, x0 = (w - d) / 2, y0 = (h - d) / 2;
            Bar(0, 0, x0 + 1, h, Color.black); Bar(x0 + d - 1, 0, w - x0 - d + 1, h, Color.black);
            GUI.DrawTexture(new Rect(x0, y0, d, d), Lens());
            var ink = new Color(0.02f, 0.02f, 0.02f, 0.95f); float cx = w / 2, cy = h / 2, R = d * 0.46f, gap = d * 0.06f;
            Bar(cx - R, cy - 2, R - gap * 2.2f, 4, ink); Bar(cx + gap * 2.2f, cy - 2, R - gap * 2.2f, 4, ink); Bar(cx - 2, cy + gap * 2.2f, 4, R - gap * 2.2f, ink);
            Bar(cx - gap * 2.2f, cy - 0.5f, gap * 4.4f, 1, ink); Bar(cx - 0.5f, cy - gap * 2.2f, 1, gap * 4.4f, ink);
            for (int i = 1; i <= 4; i++) { float o = gap * 0.5f * i; Bar(cx + o - 1.5f, cy - 1.5f, 3, 3, ink); Bar(cx - o - 1.5f, cy - 1.5f, 3, 3, ink); Bar(cx - 1.5f, cy + o - 1.5f, 3, 3, ink); }
            if (zoom >= 6f) { GUI.color = new Color(1f, 0.15f, 0.1f, 0.9f); GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Dot()); GUI.color = Color.white; }
            zoomStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, 1, 1, 0.6f) } };
            GUI.Label(new Rect(x0 + d * 0.5f - 60, y0 + d * 0.86f, 120, 24), $"{zoom:0}x", zoomStyle);
        }

        static Texture2D lens, dotTex;
        static Texture2D Dot()
        {
            if (dotTex) return dotTex;
            dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f; dotTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 3f))); }
            dotTex.Apply(); return dotTex;
        }
        /// black surround with a round lens, soft edge and a dark ring, like looking down a scope tube
        static Texture2D Lens()
        {
            if (lens) return lens;
            const int N = 512; lens = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 0.97f, r));             // tube edge
                a = Mathf.Max(a, Mathf.SmoothStep(0f, 0.55f, Mathf.InverseLerp(0.62f, 0.9f, r)));   // vignette inside the glass
                lens.SetPixel(x, y, new Color(0, 0, 0, a));
            }
            lens.Apply(); return lens;
        }
        /// iron sights: the rear aperture and the front post, as seen down the barrel
        void DrawIronSight(float w, float h)
        {
            float cx = w / 2, cy = h / 2, u = h / 1080f; var steel = new Color(0.06f, 0.06f, 0.065f, 0.97f);
            Bar(cx - 3 * u, cy, 6 * u, h * 0.16f, steel);                                   // front post, its tip on the aim point
            Bar(cx - 26 * u, cy + h * 0.16f, 52 * u, 10 * u, steel);                        // front sight base
            Bar(cx - 210 * u, cy + 14 * u, 175 * u, 70 * u, steel); Bar(cx + 35 * u, cy + 14 * u, 175 * u, 70 * u, steel);   // rear sight wings
            Bar(cx - 35 * u, cy + 60 * u, 70 * u, 24 * u, steel);
            Bar(0, h * 0.86f, w, h * 0.14f, new Color(0.04f, 0.04f, 0.045f, 0.9f));            // receiver at the bottom of the view
        }
        void DrawScope(float w, float h, float zoom)
        {
            float d = h * 1.02f, x0 = (w - d) / 2, y0 = (h - d) / 2;
            Bar(0, 0, x0 + 1, h, Color.black); Bar(x0 + d - 1, 0, w - x0 - d + 1, h, Color.black);
            GUI.DrawTexture(new Rect(x0, y0, d, d), Lens());
            // reticle: thick posts from the edge, a fine centre cross, mil dots
            var ink = new Color(0.02f, 0.02f, 0.02f, 0.95f); float cx = w / 2, cy = h / 2, R = d * 0.46f, gap = d * 0.06f;
            Bar(cx - R, cy - 2, R - gap * 2.2f, 4, ink); Bar(cx + gap * 2.2f, cy - 2, R - gap * 2.2f, 4, ink); Bar(cx - 2, cy + gap * 2.2f, 4, R - gap * 2.2f, ink);
            Bar(cx - gap * 2.2f, cy - 0.5f, gap * 4.4f, 1, ink); Bar(cx - 0.5f, cy - gap * 2.2f, 1, gap * 4.4f, ink);
            for (int i = 1; i <= 4; i++) { float o = gap * 0.5f * i; Bar(cx + o - 1.5f, cy - 1.5f, 3, 3, ink); Bar(cx - o - 1.5f, cy - 1.5f, 3, 3, ink); Bar(cx - 1.5f, cy + o - 1.5f, 3, 3, ink); }
            if (zoom >= 6f) { GUI.color = new Color(1f, 0.15f, 0.1f, 0.9f); GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Dot()); GUI.color = Color.white; }
            GUI.Label(new Rect(x0 + d * 0.5f - 60, y0 + d * 0.86f, 120, 24), $"{zoom:0}x", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, 1, 1, 0.6f) } });
        }

        void OnGUI()
        {
            if (KryvonTrailer.HideHud) return;
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };
            float w = Screen.width, h = Screen.height;

            DrawAim(weapons, hitMarkerUntil);
            if (false)
            {
                float gap = 6f + (weapons.CurrentSpread * 9f);
                Color c = Time.time < hitMarkerUntil ? Color.red : Color.white;
                Bar(w / 2 - 1, h / 2 - gap - 8, 2, 8, c); Bar(w / 2 - 1, h / 2 + gap, 2, 8, c);
                Bar(w / 2 - gap - 8, h / 2 - 1, 8, 2, c); Bar(w / 2 + gap, h / 2 - 1, 8, 2, c);
            }
            else if (false) DrawScope(w, h, Scopes.Zoom(weapons.CurrentScope));
            else if (false) DrawIronSight(w, h);
            if (false)
            {   // red dot / holographic
                var dot = Dot(); GUI.color = new Color(1f, 0.15f, 0.1f, 0.95f); GUI.DrawTexture(new Rect(w / 2 - 4, h / 2 - 4, 8, 8), dot); GUI.color = Color.white;
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

// Settings, in four tabs: GENERAL · SENSITIVITY · GYROSCOPE · CONTROLS.
// Everything here writes straight into the systems the match reads: SensitivityService (camera / free look / per scope /
// gyro), ThirdPersonCamera (TPP/FPP, aim assist), WeaponController (sniper quick shot) and KryvonTouchControls (auto-sprint).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kryvon
{
    public partial class KryvonLobby
    {
        RectTransform sPage;                       // the page Row/Slider/Pct build into
        RectTransform SP => sPage ? sPage : settings;
        readonly List<RectTransform> sPages = new();
        readonly List<Image> sTabs = new();

        static readonly ScopeType[] SensScopes = { ScopeType.None, ScopeType.RedDot, ScopeType.X2, ScopeType.X3, ScopeType.X4, ScopeType.X6, ScopeType.X8 };
        static string ScopeName(ScopeType s) => s == ScopeType.None ? "IRON SIGHTS" : s == ScopeType.RedDot ? "RED DOT / HOLO" : s.ToString().Substring(1) + "x SCOPE";

        void BuildSettingsTabs(System.Action general)
        {
            string[] names = { "GENERAL", "SENSITIVITY", "GYROSCOPE", "CONTROLS" };
            System.Action[] build = { general, BuildSensPage, BuildGyroPage, BuildControlsPage };
            float w = 1120f / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var t = Panel(settings, "Tab_" + names[i], Ink, round); Place(t, new Vector2(0.5f, 0.5f), new Vector2(-560 + i * w + (w - 10) / 2, 322), new Vector2(w - 10, 46));
                Label(t, names[i], bodyBold, 22, Color.white, TextAnchor.MiddleCenter);
                sTabs.Add(t.GetComponent<Image>());
                var page = new GameObject("Page_" + names[i], typeof(RectTransform)).GetComponent<RectTransform>(); page.SetParent(settings, false); Fill(page);
                page.anchoredPosition = new Vector2(0, -44);
                sPages.Add(page); sPage = page; build[i](); sPage = null;
                Btn(t, () => ShowSettingsPage(idx));
            }
            ShowSettingsPage(0);
        }
        void ShowSettingsPage(int i)
        {
            if (i != 0) SensitivityService.Save();
            for (int k = 0; k < sPages.Count; k++) { sPages[k].gameObject.SetActive(k == i); sTabs[k].color = k == i ? GoldDeep : Ink; }
        }

        /// a compact percentage slider with its value shown – x = column centre
        void Pct(string name, float x, float y, float value, float min, float max, System.Action<float> set, string unit = "%")
        {
            const float W = 520f;
            var l = Label(SP, name, bodyBold, 21, Dim); Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x - W / 2, y + 28), new Vector2(380, 28), new Vector2(0, 0.5f));
            var val = Label(SP, "", bodyBold, 21, Gold, TextAnchor.MiddleRight); Place(val.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x + W / 2, y + 28), new Vector2(140, 28), new Vector2(1, 0.5f));
            var bg = Panel(SP, "Track", Line, pill); Place(bg, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(W, 10));
            var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); fillArea.SetParent(bg, false); Fill(fillArea);
            var fill = Panel(fillArea, "Fill", Gold, pill); Fill(fill);
            var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); handleArea.SetParent(bg, false); Fill(handleArea);
            var handle = Panel(handleArea, "Handle", Color.white, pill); handle.sizeDelta = new Vector2(28, 28);
            var s = bg.gameObject.AddComponent<Slider>(); s.fillRect = fill; s.handleRect = handle; s.targetGraphic = handle.GetComponent<Image>();
            handle.GetComponent<Image>().raycastTarget = true; bg.GetComponent<Image>().raycastTarget = true;
            s.minValue = min; s.maxValue = max; s.wholeNumbers = true; s.value = Mathf.Clamp(value, min, max);
            val.text = Mathf.RoundToInt(s.value) + unit;
            s.onValueChanged.AddListener(v => { val.text = Mathf.RoundToInt(v) + unit; set(v); });
        }
        Text Note(string text, float y)
        {
            var n = Label(SP, text, body, 20, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
            Place(n.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(1120, 28)); return n;
        }

        void BuildSensPage()
        {
            var S = SensitivityService.Current;
            // left: moving the camera without aiming · right: aiming down each sight
            Pct("CAMERA (TPP / FPP)", -300, 250, S.camera, 1, 300, v => S.camera = v);
            Pct("FREE LOOK (EYE)", -300, 165, S.freeLook, 1, 300, v => S.freeLook = v);
            Pct("SHOULDER ZOOM", -300, 80, S.shoulderZoom, 1, 300, v => S.shoulderZoom = v);
            Pct("PARACHUTE", -300, -5, S.parachute, 1, 300, v => S.parachute = v);
            Pct("VEHICLE", -300, -90, S.vehicle, 1, 300, v => S.vehicle = v);
            Pct("TOUCH SPEED", -300, -175, S.touchMultiplier, 10, 300, v => S.touchMultiplier = v);
            Pct("FIELD OF VIEW", -300, -260, S.fieldOfView, 60, 100, v => S.fieldOfView = v, "°");
            float y = 250;
            foreach (var sc in SensScopes)
            {
                var t = sc; Pct(ScopeName(t), 300, y, S.Scope(t).ads, 1, 300, v => { S.Scope(t).ads = v; if (t == ScopeType.RedDot) S.Scope(ScopeType.Holo).ads = v; });
                y -= 85;
            }
            Note("100% feels the same on every scope – the zoom is already taken into account.", -302);
        }

        void BuildGyroPage()
        {
            var S = SensitivityService.Current;
            Row("GYROSCOPE  ·  tilt the phone to aim", 250, new[] { "OFF", "SCOPE ONLY", "ALWAYS ON" }, () => (int)S.gyroMode, v => S.gyroMode = (GyroMode)v);
            Pct("GYRO CAMERA (TPP / FPP)", -300, 150, S.gyroCamera, 1, 400, v => S.gyroCamera = v);
            float y = 150; int col = 1;
            foreach (var sc in SensScopes)
            {
                var t = sc; float x = col == 0 ? -300 : 300;
                Pct("GYRO · " + ScopeName(t), x, y, S.Scope(t).gyro, 1, 400, v => { S.Scope(t).gyro = v; if (t == ScopeType.RedDot) S.Scope(ScopeType.Holo).gyro = v; });
                col = 1 - col; if (col == 0) y -= 90;
            }
            Note(SystemInfo.supportsGyroscope ? "Your phone has a gyroscope." : "No gyroscope on this device – these apply on a phone.", -230);
        }

        void BuildControlsPage()
        {
            var S = SensitivityService.Current;
            Row("AIM BUTTON", 250, new[] { "HOLD", "TAP (TOGGLE)", "TAP = SCOPE · HOLD = SHOULDER" }, () => (int)S.aimMode, v => S.aimMode = (AimMode)v);
            Row("AIM ASSIST  ·  sticks gently to enemies", 150, new[] { "OFF", "ON" }, () => ThirdPersonCamera.AimAssistOn ? 1 : 0, v => ThirdPersonCamera.AimAssistOn = v == 1);
            Row("SNIPER QUICK SHOT  ·  press = scope, lift = fire", 50, new[] { "OFF", "ON" }, () => WeaponController.QuickShotOn ? 1 : 0, v => WeaponController.QuickShotOn = v == 1);
            Row("AUTO-SPRINT  ·  hold the stick up, let go", -50, new[] { "OFF", "ON" }, () => KryvonTouchControls.AutoSprintOn ? 1 : 0, v => KryvonTouchControls.AutoSprintOn = v == 1);
            Row("INVERT LOOK (UP / DOWN)", -150, new[] { "NORMAL", "INVERTED" }, () => S.invertY ? 1 : 0, v => S.invertY = v == 1);
            Note("In a match:  V / eye button = TPP ⇄ FPP   ·   Z / sight button = next scope you carry", -250);
        }
    }
}

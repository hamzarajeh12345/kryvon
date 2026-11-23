// SensitivitySettings – every sensitivity the player can tune, saved to PlayerPrefs as JSON.
//
// How the final number is built (degrees of camera rotation per mouse pixel / per touch pixel):
//   BASE (0.12°) × context% / 100
//   context = camera / free-look / parachute / vehicle            (no aiming)
//           = scope ADS%  (or scope FIRING% while shooting)       (aiming down sights)
//             × weapon-class% × this-weapon%                       (per weapon tuning)
//             ÷ scope zoom                                         (so 100 % "feels the same" on every scope)
// Gyro uses its own per-scope % on top of the gyro camera %.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public enum ScopeType { None, RedDot, Holo, X2, X3, X4, X6, X8 }
    public enum AimMode { Hold, Toggle, TapAndHold }   // TapAndHold: tap = scope in/out, hold = shoulder zoom
    public enum GyroMode { Off, ScopeOnly, Always }
    public enum SensContext { Camera, FreeLook, Parachute, Vehicle, ShoulderZoom, Ads }

    public static class Scopes
    {
        /// <summary>Optical zoom of each sight (None = iron sights, a slight zoom).</summary>
        public static float Zoom(ScopeType s) => s switch
        {
            ScopeType.None => 1.1f, ScopeType.RedDot => 1.2f, ScopeType.Holo => 1.2f,
            ScopeType.X2 => 2f, ScopeType.X3 => 3f, ScopeType.X4 => 4f, ScopeType.X6 => 6f, ScopeType.X8 => 8f, _ => 1f,
        };
        public static string Arabic(ScopeType s) => s switch
        {
            ScopeType.None => "بدون سكوب", ScopeType.RedDot => "ريد دوت", ScopeType.Holo => "هولو",
            ScopeType.X2 => "2x", ScopeType.X3 => "3x", ScopeType.X4 => "4x", ScopeType.X6 => "6x", ScopeType.X8 => "8x", _ => s.ToString(),
        };
    }

    [Serializable]
    public class ScopeSensitivity
    {
        public ScopeType scope;
        [Range(1, 300)] public float ads = 100f;      // aiming, not shooting
        [Range(1, 300)] public float firing = 100f;   // aiming while the trigger is held
        [Range(1, 400)] public float gyro = 100f;
    }

    [Serializable]
    public class ClassSensitivity
    {
        public WeaponClass weaponClass;
        [Range(10, 200)] public float multiplier = 100f;
    }

    [Serializable]
    public class WeaponSensitivity
    {
        public string weaponId;                       // WeaponData.weaponId
        [Range(10, 200)] public float multiplier = 100f;
    }

    [Serializable]
    public class SensitivitySettings
    {
        public const float BaseDegreesPerPixel = 0.12f;

        [Header("Free camera (%)")]
        [Range(1, 300)] public float camera = 100f;
        [Range(1, 300)] public float freeLook = 100f;
        [Range(1, 300)] public float parachute = 100f;
        [Range(1, 300)] public float vehicle = 100f;
        [Range(1, 300)] public float shoulderZoom = 90f;

        [Header("Per scope (%)")]
        public List<ScopeSensitivity> scopes = DefaultScopes();

        [Header("Per weapon (%)")]
        public List<ClassSensitivity> classes = DefaultClasses();
        public List<WeaponSensitivity> weapons = new();

        [Header("Devices")]
        [Range(10, 300)] public float touchMultiplier = 100f;
        public GyroMode gyroMode = GyroMode.Off;
        [Range(1, 400)] public float gyroCamera = 100f;
        public bool invertY;

        [Header("Aiming")]
        public AimMode aimMode = AimMode.TapAndHold;
        [Range(60, 100)] public float fieldOfView = 80f;

        static List<ScopeSensitivity> DefaultScopes()
        {
            var l = new List<ScopeSensitivity>();
            foreach (ScopeType s in Enum.GetValues(typeof(ScopeType))) l.Add(new ScopeSensitivity { scope = s });
            return l;
        }
        static List<ClassSensitivity> DefaultClasses()
        {
            var l = new List<ClassSensitivity>();
            foreach (WeaponClass c in Enum.GetValues(typeof(WeaponClass))) l.Add(new ClassSensitivity { weaponClass = c });
            return l;
        }

        public ScopeSensitivity Scope(ScopeType s)
        {
            var r = scopes.Find(x => x.scope == s);
            if (r == null) { r = new ScopeSensitivity { scope = s }; scopes.Add(r); }
            return r;
        }
        public float ClassMultiplier(WeaponClass c) { var r = classes.Find(x => x.weaponClass == c); return r != null ? r.multiplier / 100f : 1f; }
        public float WeaponMultiplier(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1f;
            var r = weapons.Find(x => x.weaponId == id); return r != null ? r.multiplier / 100f : 1f;
        }
        public void SetWeaponMultiplier(string id, float percent)
        {
            var r = weapons.Find(x => x.weaponId == id);
            if (r == null) weapons.Add(new WeaponSensitivity { weaponId = id, multiplier = percent }); else r.multiplier = percent;
        }

        /// <summary>Degrees per input pixel for the current situation.</summary>
        public float DegreesPerPixel(SensContext ctx, ScopeType scope, WeaponData weapon, bool firing)
        {
            float pct = ctx switch
            {
                SensContext.FreeLook => freeLook,
                SensContext.Parachute => parachute,
                SensContext.Vehicle => vehicle,
                SensContext.ShoulderZoom => shoulderZoom,
                SensContext.Ads => (firing ? Scope(scope).firing : Scope(scope).ads),
                _ => camera,
            };
            float deg = BaseDegreesPerPixel * pct / 100f;
            if (ctx == SensContext.Ads)
            {
                deg /= Scopes.Zoom(scope);
                if (weapon != null) deg *= ClassMultiplier(weapon.weaponClass) * WeaponMultiplier(weapon.weaponId);
            }
            return deg;
        }

        public float GyroDegreesScale(bool ads, ScopeType scope) =>
            gyroCamera / 100f * (ads ? Scope(scope).gyro / 100f / Scopes.Zoom(scope) : 1f);
    }

    /// <summary>Global access + persistence. Call Save() after the settings UI changes something.</summary>
    public static class SensitivityService
    {
        const string Key = "kryvon.sensitivity";
        static SensitivitySettings current;
        public static event Action Changed;

        public static SensitivitySettings Current
        {
            get
            {
                if (current != null) return current;
                var json = PlayerPrefs.GetString(Key, "");
                current = string.IsNullOrEmpty(json) ? new SensitivitySettings() : JsonUtility.FromJson<SensitivitySettings>(json) ?? new SensitivitySettings();
                return current;
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetToDefaults() { current = new SensitivitySettings(); Save(); }

        /// <summary>Share codes: copy your whole setup as text, paste it on another device.</summary>
        public static string Export() => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(Current)));
        public static bool Import(string code)
        {
            try
            {
                var s = JsonUtility.FromJson<SensitivitySettings>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim())));
                if (s == null) return false;
                current = s; Save(); return true;
            }
            catch { return false; }
        }
    }
}

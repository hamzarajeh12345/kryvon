// WeaponData – one asset per weapon (Create → KRYVON → Weapon Data).
// Everything about how a gun feels lives here, so designers tune it without touching code.
using UnityEngine;

namespace Kryvon
{
    public enum WeaponClass { AssaultRifle, SMG, DMR, Sniper, Shotgun, LMG, Pistol }
    public enum AmmoType { A556, A762, A9mm, A45, Gauge12, A300 }
    public enum FireMode { Single, Burst, Auto }

    [CreateAssetMenu(menuName = "KRYVON/Weapon Data", fileName = "NewWeapon")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used for per-weapon sensitivity and saves, e.g. \"saqr\".")]
        public string weaponId = "saqr";
        public string displayName = "Al-Saqr";
        public string arabicName = "الصقر";
        public WeaponClass weaponClass = WeaponClass.AssaultRifle;
        public AmmoType ammoType = AmmoType.A556;

        [Header("Damage")]
        public float damage = 40f;
        public float headshotMultiplier = 2.1f;
        public float limbMultiplier = 0.9f;
        [Tooltip("Pellets per shot (shotguns > 1).")]
        public int pellets = 1;
        public float range = 350f;
        [Tooltip("Damage starts dropping after this fraction of the range, down to 60 % at max range.")]
        [Range(0f, 1f)] public float falloffStart = 0.6f;

        [Header("Fire")]
        public float roundsPerMinute = 660f;
        public FireMode[] fireModes = { FireMode.Single, FireMode.Auto };
        public int burstCount = 3;
        public int magazineSize = 30;
        public float reloadSeconds = 2.1f;
        [Tooltip("Faster reload when the magazine still has a round chambered.")]
        public float tacticalReloadSeconds = 1.8f;
        [Tooltip("Seconds to raise the sights.")]
        public float adsSeconds = 0.18f;
        [Tooltip("Seconds to pull out this weapon.")]
        public float drawSeconds = 0.45f;

        [Header("Accuracy – cone half-angle in degrees")]
        public float hipSpread = 1.7f;
        public float shoulderSpread = 0.9f;
        public float adsSpread = 0.15f;
        [Tooltip("Extra spread per shot, recovers over time.")]
        public float bloomPerShot = 0.12f;
        public float bloomRecoveryPerSecond = 2.5f;
        public float maxBloom = 1.5f;
        public float movingMultiplier = 1.6f;
        public float airborneMultiplier = 3f;
        public float crouchMultiplier = 0.8f;
        public float proneMultiplier = 0.6f;

        [Header("Recoil (degrees)")]
        [Tooltip("Shot-by-shot kick: x = sideways, y = up. Repeats the last entry when the burst is longer.")]
        public Vector2[] recoilPattern = {
            new(0f, 0.55f), new(0.05f, 0.6f), new(-0.05f, 0.65f), new(0.1f, 0.7f), new(0.12f, 0.7f),
            new(-0.1f, 0.65f), new(-0.15f, 0.6f), new(0.15f, 0.55f), new(0.2f, 0.55f), new(-0.2f, 0.5f),
        };
        [Tooltip("Random wobble added to each kick.")]
        public float recoilJitter = 0.06f;
        public float adsRecoilMultiplier = 0.75f;
        public float crouchRecoilMultiplier = 0.85f;
        public float proneRecoilMultiplier = 0.65f;
        [Tooltip("How much of the climb the camera settles back after you stop shooting (0–1).")]
        [Range(0f, 1f)] public float recoilRecovery = 0.6f;
        public float recoilResetSeconds = 0.35f;

        [Header("Sights")]
        public ScopeType defaultScope = ScopeType.None;
        public ScopeType[] allowedScopes = { ScopeType.None, ScopeType.RedDot, ScopeType.Holo, ScopeType.X2, ScopeType.X3, ScopeType.X4, ScopeType.X6 };

        [Header("Handling")]
        [Tooltip("Movement speed multiplier while holding this weapon.")]
        public float moveSpeedMultiplier = 1f;

        [Header("Presentation (optional)")]
        public GameObject viewPrefab;
        public AudioClip shotSound, suppressedShotSound, reloadSound, drySound;
    }
}

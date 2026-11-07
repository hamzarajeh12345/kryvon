// WeaponController – hitscan shooting from the centre of the screen, spread, recoil, ammo and reload.
// Shots ignore the shooter's own colliders and never hurt teammates (same Health.teamId).
using System.Collections;
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(PlayerInputReader))]
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] WeaponData weapon;
        [Tooltip("Camera used to aim (the Main Camera).")]
        [SerializeField] Transform cameraTransform;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] int reserveAmmo = 120;

        [Header("Optional effects")]
        [SerializeField] ParticleSystem muzzleFlash;
        [SerializeField] AudioSource audioSource;
        [SerializeField] GameObject impactPrefab;

        public WeaponData Weapon => weapon;
        public int Ammo { get; private set; }
        public int Reserve => reserveAmmo;
        public bool IsReloading { get; private set; }

        /// <summary>(target, damage, headshot) – hook the hit marker / kill feed here.</summary>
        public event System.Action<Health, float, bool> OnHit;

        PlayerInputReader input;
        ThirdPersonCamera thirdPersonCamera;
        Health self;
        float nextShotTime;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            self = GetComponent<Health>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) thirdPersonCamera = cameraTransform.GetComponent<ThirdPersonCamera>();
            if (weapon != null) Ammo = weapon.magazineSize;
        }

        /// <summary>Swap to another weapon (from the inventory or a pickup).</summary>
        public void Equip(WeaponData data, int loadedAmmo)
        {
            StopAllCoroutines();
            IsReloading = false;
            weapon = data;
            Ammo = Mathf.Clamp(loadedAmmo, 0, data.magazineSize);
        }

        public void AddReserve(int amount) => reserveAmmo += amount;

        void Update()
        {
            if (weapon == null || (self != null && (!self.IsAlive || self.IsKnocked))) return;

            if (input.ReloadPressed) TryReload();

            bool trigger = weapon.automatic ? input.FireHeld : input.FirePressed;
            if (!trigger || IsReloading || Time.time < nextShotTime) return;

            if (Ammo <= 0) { TryReload(); return; }
            Shoot();
        }

        void Shoot()
        {
            nextShotTime = Time.time + 60f / weapon.roundsPerMinute;
            Ammo--;

            bool aiming = input.AimHeld;
            float spread = aiming ? weapon.aimSpread : weapon.hipSpread;
            for (int p = 0; p < Mathf.Max(1, weapon.pellets); p++)
                FireRay(SpreadDirection(spread));

            thirdPersonCamera?.AddRecoil(weapon.recoilUp * (aiming ? 0.6f : 1f), Random.Range(-1f, 1f) * weapon.recoilSide);
            if (muzzleFlash) muzzleFlash.Play();
            if (audioSource && weapon.shotSound) audioSource.PlayOneShot(weapon.shotSound);
        }

        Vector3 SpreadDirection(float halfAngleDeg)
        {
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            return (cameraTransform.forward + cameraTransform.right * r.x + cameraTransform.up * r.y).normalized;
        }

        void FireRay(Vector3 dir)
        {
            int n = Physics.RaycastNonAlloc(cameraTransform.position, dir, hits, weapon.range, hitMask, QueryTriggerInteraction.Ignore);
            // closest hit that isn't part of ourselves (the camera ray passes through our own body)
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;
                if (best < 0 || hits[i].distance < hits[best].distance) best = i;
            }
            if (best < 0) { Debug.DrawRay(cameraTransform.position, dir * weapon.range, Color.yellow, 0.2f); return; }

            RaycastHit hit = hits[best];
            Debug.DrawLine(cameraTransform.position, hit.point, Color.red, 0.2f);
            if (impactPrefab) Destroy(Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal)), 3f);

            var box = hit.collider.GetComponent<HitBox>();
            Health target = box != null ? box.owner : hit.collider.GetComponentInParent<Health>();
            if (target == null || target == self) return;
            if (self != null && target.teamId == self.teamId) return; // no friendly fire

            float t = hit.distance / weapon.range;
            float falloff = t <= weapon.falloffStart ? 1f : Mathf.Lerp(1f, 0.6f, (t - weapon.falloffStart) / (1f - weapon.falloffStart));
            bool head = box != null && box.isHead;
            float dmg = weapon.damage * falloff * (head ? weapon.headshotMultiplier : 1f) * (box != null ? box.damageMultiplier : 1f);

            target.TakeDamage(new DamageInfo { Amount = dmg, Headshot = head, Source = gameObject, Cause = weapon.displayName });
            OnHit?.Invoke(target, dmg, head);
        }

        public void TryReload()
        {
            if (IsReloading || weapon == null || Ammo >= weapon.magazineSize || reserveAmmo <= 0) return;
            StartCoroutine(Reload());
        }

        IEnumerator Reload()
        {
            IsReloading = true;
            if (audioSource && weapon.reloadSound) audioSource.PlayOneShot(weapon.reloadSound);
            yield return new WaitForSeconds(weapon.reloadSeconds);
            int take = Mathf.Min(weapon.magazineSize - Ammo, reserveAmmo);
            Ammo += take;
            reserveAmmo -= take;
            IsReloading = false;
        }
    }
}

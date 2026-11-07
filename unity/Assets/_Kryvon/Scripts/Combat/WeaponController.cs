// WeaponController – two weapon slots, aiming, scopes, fire modes, recoil patterns and hitscan shooting.
//  Aiming (from SensitivitySettings.aimMode):
//   Hold        – hold RMB = aim down sights
//   Toggle      – tap RMB = sights in / out
//   TapAndHold  – tap = sights in / out, hold = shoulder zoom (closer camera, no scope) until release
//  Accuracy: base cone (hip / shoulder / ADS) × movement, airborne, crouch, prone + bloom per shot.
//  Recoil: per-weapon pattern (shot 1, 2, 3 …) + jitter, reduced by ADS / crouch / prone; the camera settles part back.
//  Shots ignore the shooter's own colliders and never damage teammates.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    [Serializable]
    public class WeaponSlot
    {
        public WeaponData data;
        public ScopeType scope = ScopeType.None;
        public int ammo;
        public int fireModeIndex;
        [Tooltip("Attachments (see AttachmentData later): extended mag, compensator, suppressor…")]
        public bool extendedMag, compensator, suppressor, stock;

        public int MagazineSize => data == null ? 0 : Mathf.RoundToInt(data.magazineSize * (extendedMag ? 1.5f : 1f));
        public float RecoilMultiplier => (compensator ? 0.75f : 1f) * (stock ? 0.85f : 1f);
        public FireMode Mode => data == null || data.fireModes.Length == 0 ? FireMode.Single : data.fireModes[Mathf.Clamp(fireModeIndex, 0, data.fireModes.Length - 1)];
    }

    [Serializable] public class AmmoStack { public AmmoType type; public int amount; }

    [RequireComponent(typeof(PlayerInputReader))]
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] WeaponSlot[] slots = { new WeaponSlot(), new WeaponSlot() };
        [SerializeField] List<AmmoStack> ammo = new() { new AmmoStack { type = AmmoType.A556, amount = 150 } };
        [SerializeField] Transform cameraTransform;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] float tapThreshold = 0.2f;

        [Header("Optional effects")]
        [SerializeField] ParticleSystem muzzleFlash;
        [SerializeField] AudioSource audioSource;
        [SerializeField] GameObject impactPrefab;

        // ---------- state other scripts read ----------
        public int CurrentIndex { get; private set; }
        public WeaponSlot CurrentSlot => slots[CurrentIndex];
        public WeaponData Current => CurrentSlot.data;
        public ScopeType CurrentScope => Current != null ? CurrentSlot.scope : ScopeType.None;
        public bool IsAds { get; private set; }
        public bool IsShoulderZoom { get; private set; }
        public bool IsAiming => IsAds || IsShoulderZoom;
        public bool IsReloading { get; private set; }
        public bool IsDrawing { get; private set; }
        public bool IsReloadBlockingSprint => false;      // set true if you want reloading to stop sprinting
        public float CurrentSpread { get; private set; }  // degrees, for a dynamic crosshair
        public int Reserve(AmmoType t) => FindAmmo(t)?.amount ?? 0;

        public event Action<Health, float, bool> OnHit;        // target, damage, headshot
        public event Action<WeaponSlot> OnShot;
        public event Action<WeaponSlot> OnReloadStarted;
        public event Action<int> OnWeaponSwitched;

        PlayerInputReader input;
        PlayerController body;
        ThirdPersonCamera cameraRig;
        Health self;
        float nextShotTime, bloom, aimPressTime, lastShotTime;
        int shotIndex, burstLeft;
        bool aimHeldLong, adsToggled;
        Coroutine reloadRoutine;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            body = GetComponent<PlayerController>();
            self = GetComponent<Health>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) cameraRig = cameraTransform.GetComponent<ThirdPersonCamera>();
            foreach (var s in slots)
                if (s.data != null) { if (s.ammo <= 0) s.ammo = s.MagazineSize; if (s.scope == ScopeType.None) s.scope = s.data.defaultScope; }
            if (Current == null && slots.Length > 1 && slots[1].data != null) CurrentIndex = 1;
        }

        // ---------- public API (inventory / pickups) ----------
        public void Give(int slot, WeaponData data, int loadedAmmo, ScopeType scope = ScopeType.None)
        {
            slots[slot] = new WeaponSlot { data = data, ammo = Mathf.Min(loadedAmmo, data.magazineSize), scope = scope == ScopeType.None ? data.defaultScope : scope };
            if (Current == null) Switch(slot);
        }
        public bool AttachScope(ScopeType scope)
        {
            var s = CurrentSlot;
            if (s.data == null || Array.IndexOf(s.data.allowedScopes, scope) < 0) return false;
            s.scope = scope; return true;
        }
        public void AddAmmo(AmmoType t, int n)
        {
            var a = FindAmmo(t);
            if (a == null) ammo.Add(new AmmoStack { type = t, amount = n }); else a.amount += n;
        }
        AmmoStack FindAmmo(AmmoType t) => ammo.Find(a => a.type == t);

        void Update()
        {
            float dt = Time.deltaTime;
            bloom = Current != null ? Mathf.MoveTowards(bloom, 0f, Current.bloomRecoveryPerSecond * dt) : 0f;
            if (self != null && (!self.IsAlive || self.IsKnocked)) { IsAds = IsShoulderZoom = false; return; }

            HandleSwitching();
            if (Current == null) { IsAds = IsShoulderZoom = false; return; }
            HandleAiming();
            if (input.FireModePressed && Current.fireModes.Length > 1) CurrentSlot.fireModeIndex = (CurrentSlot.fireModeIndex + 1) % Current.fireModes.Length;
            if (input.ReloadPressed) TryReload();

            CurrentSpread = ComputeSpread();
            if (Time.time - lastShotTime > Current.recoilResetSeconds) shotIndex = 0;
            HandleTrigger();
        }

        void HandleSwitching()
        {
            int w = input.WeaponSwitch;
            if (w == -1) return;
            int target = w == 10 || w == -10 ? (CurrentIndex + 1) % slots.Length : w;
            if (target != CurrentIndex && target < slots.Length && slots[target].data != null) Switch(target);
        }

        void Switch(int index)
        {
            if (reloadRoutine != null) { StopCoroutine(reloadRoutine); IsReloading = false; }
            CurrentIndex = index;
            IsAds = IsShoulderZoom = adsToggled = false;
            shotIndex = 0; bloom = 0f;
            StartCoroutine(Draw());
            OnWeaponSwitched?.Invoke(index);
        }

        IEnumerator Draw()
        {
            IsDrawing = true;
            yield return new WaitForSeconds(Current != null ? Current.drawSeconds : 0.3f);
            IsDrawing = false;
        }

        void HandleAiming()
        {
            var mode = SensitivityService.Current.aimMode;
            bool sprinting = body != null && body.IsSprinting;
            if (input.AimPressed) { aimPressTime = Time.time; aimHeldLong = false; }

            switch (mode)
            {
                case AimMode.Hold:
                    IsAds = input.AimHeld; IsShoulderZoom = false; break;
                case AimMode.Toggle:
                    if (input.AimPressed) adsToggled = !adsToggled;
                    IsAds = adsToggled; IsShoulderZoom = false; break;
                case AimMode.TapAndHold:
                    if (input.AimHeld && Time.time - aimPressTime > tapThreshold) aimHeldLong = true;
                    if (input.AimReleased && !aimHeldLong) adsToggled = !adsToggled;   // quick tap
                    IsShoulderZoom = input.AimHeld && aimHeldLong && !adsToggled;
                    IsAds = adsToggled;
                    break;
            }
            if (sprinting || IsDrawing) { IsAds = IsShoulderZoom = false; adsToggled = false; }
        }

        float ComputeSpread()
        {
            var d = Current;
            float s = IsAds ? d.adsSpread : IsShoulderZoom ? d.shoulderSpread : d.hipSpread;
            if (body != null)
            {
                if (!body.IsGrounded) s *= d.airborneMultiplier;
                else if (body.IsMoving) s *= Mathf.Lerp(1f, d.movingMultiplier, Mathf.Clamp01(body.PlanarVelocity.magnitude / 4.8f));
                if (body.Stance == Stance.Crouch) s *= d.crouchMultiplier;
                else if (body.Stance == Stance.Prone) s *= d.proneMultiplier;
            }
            return s + bloom;
        }

        void HandleTrigger()
        {
            var slot = CurrentSlot;
            if (IsReloading || IsDrawing || (body != null && body.IsSprinting)) { burstLeft = 0; return; }

            bool wantShot = false;
            switch (slot.Mode)
            {
                case FireMode.Auto: wantShot = input.FireHeld; break;
                case FireMode.Single: wantShot = input.FirePressed; break;
                case FireMode.Burst:
                    if (input.FirePressed && burstLeft == 0) burstLeft = Current.burstCount;
                    wantShot = burstLeft > 0; break;
            }
            if (!wantShot || Time.time < nextShotTime) return;
            if (slot.ammo <= 0)
            {
                burstLeft = 0;
                if (input.FirePressed && audioSource && Current.drySound) audioSource.PlayOneShot(Current.drySound);
                TryReload();
                return;
            }
            Shoot();
            if (burstLeft > 0) burstLeft--;
        }

        void Shoot()
        {
            var slot = CurrentSlot; var d = slot.data;
            nextShotTime = Time.time + 60f / d.roundsPerMinute;
            lastShotTime = Time.time;
            slot.ammo--;

            for (int p = 0; p < Mathf.Max(1, d.pellets); p++) FireRay(SpreadDirection(CurrentSpread + (d.pellets > 1 ? d.hipSpread : 0f)));
            bloom = Mathf.Min(d.maxBloom, bloom + d.bloomPerShot);

            // recoil from the pattern
            Vector2 kick = d.recoilPattern.Length > 0 ? d.recoilPattern[Mathf.Min(shotIndex, d.recoilPattern.Length - 1)] : new Vector2(0f, 0.5f);
            kick += UnityEngine.Random.insideUnitCircle * d.recoilJitter;
            float mul = slot.RecoilMultiplier * (IsAds ? d.adsRecoilMultiplier : 1f);
            if (body != null) mul *= body.Stance == Stance.Crouch ? d.crouchRecoilMultiplier : body.Stance == Stance.Prone ? d.proneRecoilMultiplier : 1f;
            cameraRig?.AddRecoil(kick * mul);
            shotIndex++;

            if (muzzleFlash && !slot.suppressor) muzzleFlash.Play();
            var clip = slot.suppressor && d.suppressedShotSound ? d.suppressedShotSound : d.shotSound;
            if (audioSource && clip) audioSource.PlayOneShot(clip);
            OnShot?.Invoke(slot);
        }

        Vector3 SpreadDirection(float halfAngleDeg)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            return (cameraTransform.forward + cameraTransform.right * r.x + cameraTransform.up * r.y).normalized;
        }

        void FireRay(Vector3 dir)
        {
            var d = Current;
            int n = Physics.RaycastNonAlloc(cameraTransform.position, dir, hits, d.range, hitMask, QueryTriggerInteraction.Ignore);
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.transform.IsChildOf(transform)) continue;   // the camera ray passes through our own body
                if (best < 0 || hits[i].distance < hits[best].distance) best = i;
            }
            if (best < 0) return;
            RaycastHit hit = hits[best];
            Debug.DrawLine(cameraTransform.position, hit.point, Color.red, 0.2f);
            if (impactPrefab) Destroy(Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal)), 3f);

            var box = hit.collider.GetComponent<HitBox>();
            Health target = box != null ? box.owner : hit.collider.GetComponentInParent<Health>();
            if (target == null || target == self) return;
            if (self != null && target.teamId == self.teamId) return;   // no friendly fire

            float t = hit.distance / d.range;
            float falloff = t <= d.falloffStart ? 1f : Mathf.Lerp(1f, 0.6f, (t - d.falloffStart) / (1f - d.falloffStart));
            bool head = box != null && box.isHead;
            float part = head ? d.headshotMultiplier : box != null ? box.damageMultiplier : 1f;
            float dmg = d.damage * falloff * part;
            target.TakeDamage(new DamageInfo { Amount = dmg, Headshot = head, Source = gameObject, Cause = d.displayName });
            OnHit?.Invoke(target, dmg, head);
        }

        public void TryReload()
        {
            var slot = CurrentSlot;
            if (IsReloading || slot.data == null || slot.ammo >= slot.MagazineSize || Reserve(slot.data.ammoType) <= 0) return;
            reloadRoutine = StartCoroutine(Reload(slot));
        }

        IEnumerator Reload(WeaponSlot slot)
        {
            IsReloading = true;
            IsAds = false; adsToggled = false;
            OnReloadStarted?.Invoke(slot);
            if (audioSource && slot.data.reloadSound) audioSource.PlayOneShot(slot.data.reloadSound);
            yield return new WaitForSeconds(slot.ammo > 0 ? slot.data.tacticalReloadSeconds : slot.data.reloadSeconds);
            var stack = FindAmmo(slot.data.ammoType);
            int take = Mathf.Min(slot.MagazineSize - slot.ammo, stack != null ? stack.amount : 0);
            slot.ammo += take;
            if (stack != null) stack.amount -= take;
            IsReloading = false;
            reloadRoutine = null;
        }
    }
}

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
        [SerializeField] WeaponSlot[] slots = { new WeaponSlot(), new WeaponSlot(), new WeaponSlot(), new WeaponSlot() };
        /// Battle royale rule: everybody lands with empty hands and loots everything.
        public static bool StartUnarmed = true;
        public const int Primary1 = 0, Primary2 = 1, PistolSlot = 2, MeleeSlot = 3;
        public static readonly string[] SlotNames = { "PRIMARY 1", "PRIMARY 2", "PISTOL", "MELEE" };
        public WeaponSlot Slot(int i) => i >= 0 && i < slots.Length ? slots[i] : null;
        public bool IsMelee => Current == null || Current.weaponClass == WeaponClass.Melee;
        public string HeldName => Current != null ? Current.displayName : "Fists";
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
            if (StartUnarmed) { slots = new[] { new WeaponSlot(), new WeaponSlot(), new WeaponSlot(), new WeaponSlot() }; ammo.Clear(); CurrentIndex = 0; }
            else if (slots.Length < 4) { var s4 = new WeaponSlot[4]; for (int i = 0; i < 4; i++) s4[i] = i < slots.Length ? slots[i] : new WeaponSlot(); slots = s4; }
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
        /// Puts a picked-up weapon in the right slot: rifles etc. into one of the two primary slots, pistols into the pistol
        /// slot, melee into the melee slot. Returns the weapon it replaced (to drop on the ground), or null.
        public WeaponData Take(WeaponData data, int loadedAmmo)
        {
            int slot;
            if (data.weaponClass == WeaponClass.Melee) slot = MeleeSlot;
            else if (data.weaponClass == WeaponClass.Pistol) slot = PistolSlot;
            else slot = slots[Primary1].data == null ? Primary1 : slots[Primary2].data == null ? Primary2 : (CurrentIndex == Primary2 ? Primary2 : Primary1);
            var old = slots[slot].data;
            slots[slot] = new WeaponSlot { data = data, ammo = data.weaponClass == WeaponClass.Melee ? 0 : Mathf.Min(loadedAmmo, data.magazineSize), scope = data.defaultScope };
            if (Current == null || slot == CurrentIndex || old == null) Switch(slot);
            return old;
        }
        public bool AttachScope(ScopeType scope)
        {
            var s = CurrentSlot;
            if (scope != ScopeType.None) ownedScopes.Add(scope);       // a scope you picked up stays in the pouch for quick swaps
            if (s.data == null || Array.IndexOf(s.data.allowedScopes, scope) < 0) return false;
            s.scope = scope; return true;
        }

        // ---------- quick scope swap: one tap cycles the sights you carry (iron → red dot → 4x …) without the bag ----------
        readonly HashSet<ScopeType> ownedScopes = new();
        public IReadOnlyCollection<ScopeType> OwnedScopes => ownedScopes;
        public bool CanCycleScope => Current != null && !IsMelee && ScopeChoices().Count > 1;
        List<ScopeType> ScopeChoices()
        {
            var l = new List<ScopeType> { ScopeType.None };
            if (Current == null) return l;
            foreach (ScopeType t in Enum.GetValues(typeof(ScopeType)))
                if (t != ScopeType.None && ownedScopes.Contains(t) && Array.IndexOf(Current.allowedScopes, t) >= 0) l.Add(t);
            if (CurrentScope != ScopeType.None && !l.Contains(CurrentScope)) l.Add(CurrentScope);
            return l;
        }
        public ScopeType CycleScope()
        {
            if (!CanCycleScope) return CurrentScope;
            var l = ScopeChoices(); int i = l.IndexOf(CurrentScope);
            if (CurrentScope != ScopeType.None) ownedScopes.Add(CurrentScope);
            CurrentSlot.scope = l[(i + 1) % l.Count];
            return CurrentSlot.scope;
        }

        // ---------- sniper quick shot (touch): press = scope in, lift the finger = fire and scope out ----------
        public static bool QuickShotOn
        {
            get => PlayerPrefs.GetInt("kryvon.quickshot", KryvonTouchControls.Wanted ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt("kryvon.quickshot", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        bool qsHeld;
        public bool IsQuickShotting => qsHeld;
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
            ShowHeldGun();
            if (body != null && body.IsSwimming) { IsAds = IsShoulderZoom = false; return; }   // no shooting while swimming
            if (IsMelee) { IsAds = IsShoulderZoom = false; HandleMelee(); return; }
            HandleAiming();
            if (input.FireModePressed && Current.fireModes.Length > 1) CurrentSlot.fireModeIndex = (CurrentSlot.fireModeIndex + 1) % Current.fireModes.Length;
            if (input.ReloadPressed) TryReload();
            if (input.ScopeCyclePressed && CanCycleScope && !IsReloading)
            {
                var sc = CycleScope(); IsAds = adsToggled = false;
                KryvonKillFeed.Notice("SIGHT: " + (sc == ScopeType.None ? "IRON SIGHTS" : sc == ScopeType.RedDot ? "RED DOT" : sc == ScopeType.Holo ? "HOLO" : sc.ToString().Substring(1) + "x SCOPE"));
            }

            CurrentSpread = ComputeSpread();
            if (Time.time - lastShotTime > Current.recoilResetSeconds) shotIndex = 0;
            HandleTrigger();
            // auto reload: the magazine runs dry → the reload starts by itself (no extra tap)
            if (CurrentSlot.ammo <= 0 && !IsReloading && !IsDrawing && !qsHeld && Time.time > nextShotTime + 0.15f && Reserve(Current.ammoType) > 0) TryReload();
        }

        void HandleSwitching()
        {
            int w = input.WeaponSwitch;
            if (w == -1) return;
            int target = w;
            if (w == 10 || w == -10)            // mouse wheel: next / previous slot that holds something
            {
                target = CurrentIndex;
                for (int k = 1; k <= slots.Length; k++) { int i = ((CurrentIndex + (w == 10 ? k : -k)) % slots.Length + slots.Length) % slots.Length; if (slots[i].data != null) { target = i; break; } }
            }
            if (target != CurrentIndex && target >= 0 && target < slots.Length) Switch(target);   // an empty slot = fists
        }

        void Switch(int index)
        {
            if (reloadRoutine != null) { StopCoroutine(reloadRoutine); IsReloading = false; }
            CurrentIndex = index; qsHeld = false;
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
            if (!sprinting && !IsDrawing && !IsReloading && QuickShotOn && Current.weaponClass == WeaponClass.Sniper && input.FirePressed && !IsAds && CurrentSlot.ammo > 0)
                qsHeld = true;
            if (qsHeld) { IsAds = true; IsShoulderZoom = false; }
            if (sprinting || IsDrawing) { IsAds = IsShoulderZoom = false; adsToggled = false; qsHeld = false; }
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
            if (IsAds || IsShoulderZoom) s *= KryvonClass.AimSpreadMul;      // sniper class: steadier aim
            return s + bloom;
        }

        // ---------- melee: fists or a melee weapon ----------
        float nextSwing;
        void HandleMelee()
        {
            if (IsDrawing || (body != null && body.IsSprinting) || !input.FirePressed || Time.time < nextSwing) return;
            var d = Current;
            float dmg = d ? d.damage : 12f, reach = d ? d.range : 1.5f;
            nextSwing = Time.time + (d ? 60f / Mathf.Max(1f, d.roundsPerMinute) : 0.5f);
            if (audioSource && d && d.shotSound) audioSource.PlayOneShot(d.shotSound, 0.8f);
            var fwd = cameraTransform ? cameraTransform.forward : transform.forward;
            var from = transform.position + Vector3.up * 1.3f;
            foreach (var hit in Physics.SphereCastAll(from, 0.35f, fwd, reach, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                var box = hit.collider.GetComponent<HitBox>();
                if (box && box.owner && box.owner != self) { box.owner.TakeDamage(new DamageInfo { Amount = dmg * (box.isHead ? 1.5f : 1f), Headshot = box.isHead, Source = gameObject, Cause = d ? d.displayName : "Fists" }); break; }
                var thing = hit.collider.GetComponentInParent<IDamageable>();
                if (thing != null && !(thing is Health h && h == self)) { thing.TakeDamage(new DamageInfo { Amount = dmg, Source = gameObject, Cause = d ? d.displayName : "Fists" }); break; }
            }
        }

        // the rifle model in the hero's hands only shows while a gun is held
        // the held gun takes the shape of the weapon in hand (one hidden model per gun type on the hand bone)
        string gunShown = "?"; Transform[] heldGuns;
        void ShowHeldGun()
        {
            string want = IsMelee ? "" : string.IsNullOrEmpty(Current.model) ? "w_ar" : Current.model;
            string key = want + "|" + (Current ? Current.weaponId : "") + "|" + CurrentScope; if (key == gunShown) return; gunShown = key;
            heldGuns ??= System.Array.FindAll(GetComponentsInChildren<Transform>(true), t => t.name.StartsWith("Gun_"));
            if (want != "" && !System.Array.Exists(heldGuns, g => g && g.name == "Gun_" + want)) want = "w_ar";
            foreach (var g in heldGuns) if (g) g.gameObject.SetActive(want != "" && g.name == "Gun_" + want);
            // a gun is picked up bare: its optic (the "Scope" part of the model) only shows once a scope is attached
            bool optic = CurrentScope != ScopeType.None;
            foreach (var g in heldGuns) if (g && g.gameObject.activeSelf) foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Scope")) t.gameObject.SetActive(optic);
            KryvonSkins.ApplyHeld(this);      // this gun's equipped skin
        }

        void HandleTrigger()
        {
            var slot = CurrentSlot;
            if (IsReloading || IsDrawing || (body != null && body.IsSprinting)) { burstLeft = 0; qsHeld = false; return; }
            if (qsHeld)
            {   // hold to look through the scope; the round leaves when the finger comes up
                burstLeft = 0;
                if (input.FireHeld && !input.FireReleased) return;
                qsHeld = false;
                if (slot.ammo > 0 && Time.time >= nextShotTime) Shoot();
                IsAds = adsToggled = false;
                return;
            }

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
            float mul = slot.RecoilMultiplier * (IsAds ? d.adsRecoilMultiplier : 1f) * KryvonClass.RecoilMul;
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

        /// muzzle velocity (game scale, m/s) per weapon class: what the bullet drop is computed from
        public static float MuzzleVelocity(WeaponClass c) => c switch
        {
            WeaponClass.Sniper => 820f, WeaponClass.DMR => 760f, WeaponClass.AssaultRifle => 620f, WeaponClass.LMG => 620f,
            WeaponClass.SMG => 380f, WeaponClass.Pistol => 330f, WeaponClass.Shotgun => 360f, _ => 600f,
        };
        /// bullet drop: the shot is traced once to find how far it goes, then re-aimed lower by ½·g·t² over that flight time
        /// (nothing under 60 m; ~1.2 m at 300 m with a rifle, so long shots need holding over like a real one)
        Vector3 Dropped(Vector3 dir, WeaponData d)
        {
            int n = Physics.RaycastNonAlloc(cameraTransform.position, dir, hits, d.range, hitMask, QueryTriggerInteraction.Ignore);
            float dist = d.range;
            for (int i = 0; i < n; i++) if (!hits[i].collider.transform.IsChildOf(transform) && hits[i].distance < dist) dist = hits[i].distance;   // not our own body
            if (dist < 60f) return dir;
            float t = dist / MuzzleVelocity(d.weaponClass), drop = 0.5f * 9.81f * t * t;
            return (dir * dist - Vector3.up * drop).normalized;
        }

        void FireRay(Vector3 dir)
        {
            var d = Current;
            if (d.weaponClass != WeaponClass.Melee) dir = Dropped(dir, d);
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
            if (target == null)   // glass, doors, barrels … anything breakable
            {
                var thing = hit.collider.GetComponentInParent<IDamageable>();
                thing?.TakeDamage(new DamageInfo { Amount = d.damage, Source = gameObject, Cause = d.displayName });
                return;
            }
            if (target == self) return;
            if (self != null && target.teamId == self.teamId) return;   // no friendly fire

            float t = hit.distance / d.range;
            float falloff = t <= d.falloffStart ? 1f : Mathf.Lerp(1f, 0.6f, (t - d.falloffStart) / (1f - d.falloffStart));
            bool head = box != null && box.isHead;
            // hit zones: head ×headshotMultiplier (helmet), torso ×1 (vest), legs ×0.75 (no armour) – legs are the part of the
            // body capsule below the hips (the body is one capsule, so the zone is read from the hit height)
            float above = hit.point.y - target.transform.position.y;
            bool legs = !head && above < 0.85f && target.GetComponent<CharacterController>() is var tc && (!tc || tc.height > 1.2f);   // not when crouched / prone
            float part = head ? d.headshotMultiplier : legs ? 0.75f : box != null ? box.damageMultiplier : 1f;
            float dmg = d.damage * falloff * part;
            target.TakeDamage(new DamageInfo { Amount = dmg, Headshot = head, Limb = legs, Source = gameObject, Cause = d.displayName, IgnoreArmor = d.ignoresArmor });
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
            yield return new WaitForSeconds((slot.ammo > 0 ? slot.data.tacticalReloadSeconds : slot.data.reloadSeconds) * KryvonClass.ReloadMul);   // assault class: fast hands
            var stack = FindAmmo(slot.data.ammoType);
            int take = Mathf.Min(slot.MagazineSize - slot.ammo, stack != null ? stack.amount : 0);
            slot.ammo += take;
            if (stack != null) stack.amount -= take;
            IsReloading = false;
            reloadRoutine = null;
        }
    }
}

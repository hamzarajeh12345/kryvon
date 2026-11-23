// The KRAYVON arsenal: 22 weapons across AR, SMG, sniper, DMR, shotgun, LMG and pistol, with damage, fire rate,
// magazines, recoil character, ammo calibre, model, and which ones only come from supply drops.
// Original names; each maps to a realistic generated model in Models/Weapons.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        public class Arm
        {
            public string id, name, model, sound; public WeaponClass cls; public AmmoType ammo;
            public float dmg, rpm, range, reload, len, kickUp, kickSide, spread = 1.7f, headMul = 2.1f; public int mag, pellets = 1, tier = 1;
            public FireMode[] modes; public ScopeType scope = ScopeType.None; public bool airdrop, pierce;
        }
        static readonly FireMode[] AUTO = { FireMode.Auto, FireMode.Single }, BURST = { FireMode.Burst, FireMode.Single }, SEMI = { FireMode.Single };
        public static readonly Arm[] Arsenal =
        {
            // ---------------- assault rifles ----------------
            new() { id = "zawbaa",  name = "AK-12",       cls = WeaponClass.AssaultRifle, ammo = AmmoType.A762, dmg = 49, rpm = 750, mag = 30, reload = 2.6f, range = 300, kickUp = 0.95f, kickSide = 0.35f, modes = AUTO, model = "w_bullpup2", len = 0.75f, sound = "gun_ar", airdrop = true, tier = 3 },
            new() { id = "shahin",  name = "HK416",        cls = WeaponClass.AssaultRifle, ammo = AmmoType.A556, dmg = 42, rpm = 700, mag = 30, reload = 2.3f, range = 380, kickUp = 0.45f, kickSide = 0.1f, modes = AUTO, model = "w_bullpup", len = 0.8f, sound = "gun_ar", airdrop = true, tier = 3 },
            new() { id = "saqr4",   name = "M4A1",        cls = WeaponClass.AssaultRifle, ammo = AmmoType.A556, dmg = 41, rpm = 690, mag = 30, reload = 2.1f, range = 380, kickUp = 0.55f, kickSide = 0.15f, modes = AUTO, model = "w_ar", len = 0.9f, sound = "gun_ar", tier = 2 },
            new() { id = "nimr47",  name = "AK-47",       cls = WeaponClass.AssaultRifle, ammo = AmmoType.A762, dmg = 48, rpm = 600, mag = 30, reload = 2.4f, range = 350, kickUp = 1.1f, kickSide = 0.3f, modes = AUTO, model = "w_ak", len = 0.88f, sound = "gun_ar", tier = 2 },
            new() { id = "burj16",  name = "M16A4",       cls = WeaponClass.AssaultRifle, ammo = AmmoType.A556, dmg = 43, rpm = 800, mag = 30, reload = 2.2f, range = 450, kickUp = 0.6f, kickSide = 0.1f, modes = BURST, model = "w_ar", len = 1.0f, sound = "gun_ar", tier = 2 },
            new() { id = "jabal36", name = "G36C",      cls = WeaponClass.AssaultRifle, ammo = AmmoType.A556, dmg = 41, rpm = 650, mag = 30, reload = 2.2f, range = 380, kickUp = 0.45f, kickSide = 0.12f, modes = AUTO, model = "w_ar", len = 0.85f, sound = "gun_ar", tier = 1 },
            new() { id = "barq762", name = "AKM",      cls = WeaponClass.AssaultRifle, ammo = AmmoType.A762, dmg = 46, rpm = 700, mag = 30, reload = 2.4f, range = 330, kickUp = 0.9f, kickSide = 0.55f, modes = AUTO, model = "w_ak", len = 0.86f, sound = "gun_ar", tier = 2 },
            // ---------------- SMGs ----------------
            new() { id = "zalzal45", name = "UMP45",    cls = WeaponClass.SMG, ammo = AmmoType.A45, dmg = 41, rpm = 650, mag = 25, reload = 2.0f, range = 180, kickUp = 0.35f, kickSide = 0.12f, modes = AUTO, model = "w_smg", len = 0.68f, sound = "gun_smg", tier = 1 },
            new() { id = "asifav",  name = "Vector",       cls = WeaponClass.SMG, ammo = AmmoType.A9mm, dmg = 31, rpm = 1100, mag = 19, reload = 1.8f, range = 120, kickUp = 0.3f, kickSide = 0.18f, modes = AUTO, model = "w_vector", len = 0.62f, sound = "gun_smg", tier = 2 },
            new() { id = "asifa",   name = "MP5",         cls = WeaponClass.SMG, ammo = AmmoType.A9mm, dmg = 34, rpm = 950, mag = 32, reload = 1.9f, range = 130, kickUp = 0.35f, kickSide = 0.2f, modes = AUTO, model = "w_uzi", len = 0.45f, sound = "gun_smg", tier = 1 },
            new() { id = "tabl",    name = "Thompson",          cls = WeaponClass.SMG, ammo = AmmoType.A45, dmg = 40, rpm = 700, mag = 50, reload = 3.2f, range = 150, kickUp = 0.5f, kickSide = 0.25f, modes = AUTO, model = "w_drum", len = 0.85f, sound = "gun_smg", tier = 1 },
            // ---------------- snipers + DMRs ----------------
            new() { id = "malik300", name = "AWM .300", cls = WeaponClass.Sniper, ammo = AmmoType.A300, dmg = 105, rpm = 40, mag = 5, reload = 3.6f, range = 1000, kickUp = 3.2f, modes = SEMI, model = "w_bigsniper", len = 1.25f, sound = "gun_sniper", scope = ScopeType.X8, airdrop = true, pierce = true, headMul = 2.5f, spread = 3.5f, tier = 3 },
            new() { id = "qannas24", name = "M24", cls = WeaponClass.Sniper, ammo = AmmoType.A762, dmg = 79, rpm = 45, mag = 5, reload = 3.2f, range = 800, kickUp = 2.6f, modes = SEMI, model = "w_sniper", len = 1.15f, sound = "gun_sniper", scope = ScopeType.X4, headMul = 2.5f, spread = 3.5f, tier = 3 },
            new() { id = "qannas98", name = "Kar98k", cls = WeaponClass.Sniper, ammo = AmmoType.A762, dmg = 75, rpm = 40, mag = 5, reload = 3.4f, range = 750, kickUp = 2.5f, modes = SEMI, model = "w_sniper", len = 1.1f, sound = "gun_sniper", scope = ScopeType.X4, headMul = 2.5f, spread = 3.5f, tier = 2 },
            new() { id = "rami14",  name = "M14 EBR",       cls = WeaponClass.DMR, ammo = AmmoType.A762, dmg = 62, rpm = 380, mag = 10, reload = 3.0f, range = 650, kickUp = 1.6f, kickSide = 0.3f, modes = SEMI, model = "w_dmr", len = 1.1f, sound = "gun_sniper", scope = ScopeType.X4, tier = 3 },
            new() { id = "ramiskr", name = "SKS",      cls = WeaponClass.DMR, ammo = AmmoType.A762, dmg = 60, rpm = 360, mag = 10, reload = 2.9f, range = 600, kickUp = 1.5f, kickSide = 0.25f, modes = SEMI, model = "w_dmr", len = 1.05f, sound = "gun_sniper", tier = 2 },
            new() { id = "sahm14",  name = "Mini-14",       cls = WeaponClass.DMR, ammo = AmmoType.A556, dmg = 47, rpm = 420, mag = 20, reload = 2.6f, range = 650, kickUp = 0.9f, kickSide = 0.1f, modes = SEMI, model = "w_dmr", len = 0.98f, sound = "gun_ar", tier = 2 },
            // ---------------- shotguns ----------------
            new() { id = "raad97",  name = "M1897",       cls = WeaponClass.Shotgun, ammo = AmmoType.Gauge12, dmg = 26, pellets = 9, rpm = 60, mag = 5, reload = 4.0f, range = 45, kickUp = 2.2f, modes = SEMI, model = "w_shotgun", len = 1.0f, sound = "gun_shotgun", spread = 4.5f, tier = 1 },
            new() { id = "raads12", name = "Saiga-12",      cls = WeaponClass.Shotgun, ammo = AmmoType.Gauge12, dmg = 22, pellets = 9, rpm = 260, mag = 5, reload = 3.0f, range = 40, kickUp = 1.8f, modes = SEMI, model = "w_autoshot", len = 0.98f, sound = "gun_shotgun", spread = 5f, tier = 2 },
            new() { id = "raaddb",  name = "Double Barrel",       cls = WeaponClass.Shotgun, ammo = AmmoType.Gauge12, dmg = 26, pellets = 9, rpm = 200, mag = 14, reload = 5.2f, range = 45, kickUp = 2f, modes = SEMI, model = "w_dbshot", len = 0.8f, sound = "gun_shotgun", spread = 4.2f, tier = 3 },
            // ---------------- LMGs ----------------
            new() { id = "jabal249", name = "M249",    cls = WeaponClass.LMG, ammo = AmmoType.A556, dmg = 45, rpm = 750, mag = 100, reload = 7.0f, range = 450, kickUp = 0.6f, kickSide = 0.3f, modes = AUTO, model = "w_lmg", len = 1.05f, sound = "gun_lmg", tier = 3 },
            new() { id = "sahn28",  name = "DP-28",       cls = WeaponClass.LMG, ammo = AmmoType.A762, dmg = 51, rpm = 550, mag = 47, reload = 5.5f, range = 420, kickUp = 0.8f, kickSide = 0.2f, modes = AUTO, model = "w_dp", len = 1.2f, sound = "gun_lmg", tier = 2 },
            // ---------------- pistol ----------------
            new() { id = "saif19",  name = "Glock 19",       cls = WeaponClass.Pistol, ammo = AmmoType.A9mm, dmg = 30, rpm = 400, mag = 15, reload = 1.6f, range = 100, kickUp = 0.7f, kickSide = 0.15f, modes = SEMI, model = "w_pistol", len = 0.2f, sound = "gun_pistol", tier = 1 },
        };
        public static readonly Dictionary<WeaponData, Arm> ArmOf = new();

        public static WeaponData MakeWeapon(Arm a)
        {
            var path = $"{Root}/Data/W_{a.id}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null) { Directory.CreateDirectory(Root + "/Data"); w = ScriptableObject.CreateInstance<WeaponData>(); AssetDatabase.CreateAsset(w, path); }
            w.weaponId = a.id; w.model = a.model; w.modelLength = a.len; w.displayName = a.name; w.arabicName = a.name; w.weaponClass = a.cls; w.ammoType = a.ammo;
            w.damage = a.dmg; w.headshotMultiplier = a.headMul; w.pellets = a.pellets; w.range = a.range; w.roundsPerMinute = a.rpm; w.magazineSize = a.mag;
            w.reloadSeconds = a.reload; w.tacticalReloadSeconds = a.reload * 0.85f; w.fireModes = a.modes; w.defaultScope = a.scope; w.ignoresArmor = a.pierce;
            w.hipSpread = a.spread; w.shoulderSpread = a.spread * 0.55f; w.adsSpread = a.cls == WeaponClass.Shotgun ? a.spread * 0.7f : a.cls == WeaponClass.Sniper ? 0.02f : 0.12f;
            // recoil: a climbing pattern with this gun's character (vertical climb, sideways drift)
            var pat = new Vector2[30];
            for (int i = 0; i < pat.Length; i++) pat[i] = new Vector2(Mathf.Sin(i * 0.9f + a.id.Length) * a.kickSide * (0.4f + i * 0.03f), a.kickUp * (1f + Mathf.Min(i, 10) * 0.035f));
            w.recoilPattern = pat; w.recoilJitter = a.kickSide * 0.25f + 0.03f;
            w.allowedScopes = a.cls switch
            {
                WeaponClass.Sniper or WeaponClass.DMR => new[] { ScopeType.None, ScopeType.RedDot, ScopeType.Holo, ScopeType.X2, ScopeType.X3, ScopeType.X4, ScopeType.X6, ScopeType.X8 },
                WeaponClass.Shotgun or WeaponClass.Pistol => new[] { ScopeType.None, ScopeType.RedDot, ScopeType.Holo },
                WeaponClass.SMG => new[] { ScopeType.None, ScopeType.RedDot, ScopeType.Holo, ScopeType.X2, ScopeType.X3, ScopeType.X4 },
                _ => new[] { ScopeType.None, ScopeType.RedDot, ScopeType.Holo, ScopeType.X2, ScopeType.X3, ScopeType.X4, ScopeType.X6 },
            };
            w.shotSound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{a.sound}.wav");
            w.suppressedShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/gun_suppressed.wav");
            w.reloadSound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/reload.wav");
            w.drySound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/ui_dry_fire.wav");
            EditorUtility.SetDirty(w); ArmOf[w] = a; return w;
        }

        /// a ground-loot weapon for an area tier (1 low · 2 mid · 3 high). Airdrop-only guns never appear.
        static WeaponData PickWeapon(int tier)
        {
            if (lootGuns.Count == 0) EnsureLootGuns();
            for (int t = 0; t < 40; t++)
            {
                var w = lootGuns[rng.Next(lootGuns.Count)]; var a = ArmOf[w];
                if (a.airdrop) continue;
                if (tier == 1 && a.tier > 1 && rng.NextDouble() < 0.85) continue;
                if (tier == 2 && a.tier > 2 && rng.NextDouble() < 0.6) continue;
                if (tier == 3 && a.tier == 1 && rng.NextDouble() < 0.6) continue;
                return w;
            }
            return lootGuns[0];
        }
        static (string model, float len) ModelOf(WeaponData w)
        {
            var a = ArmOf.TryGetValue(w, out var x) ? x : null;
            if (a != null && File.Exists($"{Root}/Models/Weapons/{a.model}.glb")) return (a.model, a.len);
            return GunModel[w.weaponClass];
        }
    }
}

// Ground loot: weapons, ammo, medkits, vests, helmets. Press F (Interact) near an item to take it.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public enum LootKind { Weapon, Ammo, Medkit, Bandage, Vest, Helmet, Backpack, Drink, Syringe, Grenade, Scope, FlareGun, FirstAid, Painkiller, Molotov, SmokeGrenade, Flashbang, SpikeStrip, Shield, Ghillie, CloakDevice }

    public class KryvonLoot : MonoBehaviour
    {
        public LootKind kind;
        public WeaponData weapon;
        public AmmoType ammoType;
        public int amount = 30;
        [Range(0, 3)] public int level = 1;           // armour level / rarity
        public string label = "";
        [Range(0, 4)] public int rarity;              // 0 common · 1 uncommon · 2 rare · 3 epic · 4 legendary
        public static readonly Color[] RarityColors = { new(0.85f, 0.85f, 0.85f), new(0.42f, 0.85f, 0.42f), new(0.3f, 0.62f, 1f), new(0.72f, 0.42f, 1f), new(1f, 0.78f, 0.25f) };
        public static readonly string[] RarityNames = { "COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY" };

        public static readonly List<KryvonLoot> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Describe() => kind switch
        {
            LootKind.Weapon => weapon ? weapon.displayName : "Weapon",
            LootKind.Ammo => $"{AmmoName(ammoType)} Ammo x{amount}",
            LootKind.Medkit => "Med Kit  (100%)",
            LootKind.FirstAid => "First Aid Kit  (75%)",
            LootKind.Painkiller => "Painkillers  (+60 boost)",
            LootKind.Bandage => $"Bandages x{amount}",
            LootKind.Vest => $"Vest Lv.{level}",
            LootKind.Helmet => $"Helmet Lv.{level}",
            LootKind.Backpack => $"Backpack Lv.{level}",
            LootKind.Drink => "Energy Drink  (+30 boost)",
            LootKind.Syringe => "Adrenaline Syringe  (full boost)",
            LootKind.Grenade => "Frag Grenade",
            LootKind.Molotov => "Molotov Cocktail",
            LootKind.SmokeGrenade => "Smoke Grenade",
            LootKind.Flashbang => "Flashbang",
            LootKind.SpikeStrip => "Spike Strip  (Y to lay)",
            LootKind.Shield => "Deployable Shield  (U to plant)",
            LootKind.Ghillie => "LEVANT GHILLIE SUIT",
            LootKind.CloakDevice => "SHADOW CLOAK  (X, 3 charges)",
            LootKind.FlareGun => "FLARE GUN  (G = call a supply drop)",
            LootKind.Scope => $"{(level >= 3 ? "8x" : level == 2 ? "4x" : "2x")} Scope",
            _ => label,
        };

        /// returns true when the item was used up
        static KryvonThrowables T(GameObject p) => p.GetComponent<KryvonThrowables>() ?? p.AddComponent<KryvonThrowables>();
        public static Color AmmoColor(AmmoType t) => t switch { AmmoType.A556 => new Color(0.35f, 0.85f, 0.35f), AmmoType.A762 => new Color(0.95f, 0.75f, 0.25f), AmmoType.A9mm => new Color(0.35f, 0.6f, 1f), AmmoType.Gauge12 => new Color(1f, 0.32f, 0.28f), AmmoType.A300 => new Color(0.8f, 0.45f, 1f), _ => Color.white };
        public static string AmmoName(AmmoType t) => t switch { AmmoType.A556 => "5.56mm", AmmoType.A762 => "7.62mm", AmmoType.A9mm => "9mm", AmmoType.A45 => ".45 ACP", AmmoType.Gauge12 => "12 Gauge", AmmoType.A300 => ".300 Magnum", _ => t.ToString() };

        public bool Apply(GameObject player)
        {
            var w = player.GetComponent<WeaponController>(); var h = player.GetComponent<Health>(); var gear = player.GetComponent<KryvonGear>() ?? player.AddComponent<KryvonGear>();
            switch (kind)
            {
                case LootKind.Weapon:
                    if (!w || !weapon) return false;
                    int slot = w.CurrentSlot.data == null ? w.CurrentIndex : (w.CurrentIndex == 0 ? 1 : 0);
                    w.Give(slot, weapon, weapon.magazineSize); w.AddAmmo(weapon.ammoType, weapon.magazineSize * 2); return true;
                case LootKind.Ammo: if (!w) return false; w.AddAmmo(ammoType, amount); return true;
                case LootKind.Medkit: gear.Add(Consumable.MedKit); return true;
                case LootKind.FirstAid: gear.Add(Consumable.FirstAid); return true;
                case LootKind.Painkiller: gear.Add(Consumable.Painkillers); return true;
                case LootKind.Bandage: gear.Add(Consumable.Bandage, Mathf.Max(1, amount)); return true;
                case LootKind.Vest: if (!h) return false; h.vestReduction = new[] { 0f, 0.3f, 0.4f, 0.55f }[level]; h.vestDurability = 100f + 50f * level; return true;
                case LootKind.Helmet: if (!h) return false; h.helmetReduction = new[] { 0f, 0.3f, 0.4f, 0.55f }[level]; h.helmetDurability = 80f + 40f * level; return true;
                case LootKind.Backpack: if (level <= gear.backpack) return false; gear.SetBackpack(level); return true;
                case LootKind.Drink: gear.drinks++; return true;
                case LootKind.Syringe: gear.syringes++; return true;
                case LootKind.Grenade: T(player).counts[(int)Throwable.Frag]++; return true;
                case LootKind.Molotov: T(player).counts[(int)Throwable.Molotov]++; return true;
                case LootKind.SmokeGrenade: T(player).counts[(int)Throwable.Smoke]++; return true;
                case LootKind.Flashbang: T(player).counts[(int)Throwable.Flashbang]++; return true;
                case LootKind.SpikeStrip: T(player).spikes++; return true;
                case LootKind.Shield: T(player).shields++; return true;
                case LootKind.Ghillie: { var st = player.GetComponent<KryvonStealth>(); if (!st || st.HasGhillie) return false; st.EquipGhillie(); return true; }
                case LootKind.CloakDevice: { var st = player.GetComponent<KryvonStealth>(); if (!st) return false; st.cloakCharges += 3; return true; }
                case LootKind.FlareGun: gear.flares++; return true;
                case LootKind.Scope: return w && w.AttachScope(level >= 3 ? ScopeType.X8 : level == 2 ? ScopeType.X4 : ScopeType.X2);
            }
            return false;
        }
    }
}

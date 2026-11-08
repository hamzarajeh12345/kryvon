// Ground loot: weapons, ammo, medkits, vests, helmets. Press F (Interact) near an item to take it.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public enum LootKind { Weapon, Ammo, Medkit, Bandage, Vest, Helmet, Backpack, Drink, Syringe, Grenade, Scope, FlareGun }

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
            LootKind.Medkit => "First-aid kit",
            LootKind.Bandage => $"Bandages x{amount}",
            LootKind.Vest => $"Vest Lv.{level}",
            LootKind.Helmet => $"Helmet Lv.{level}",
            LootKind.Backpack => $"Backpack Lv.{level}",
            LootKind.Drink => "Energy Drink",
            LootKind.Syringe => "Adrenaline Syringe",
            LootKind.Grenade => "Frag Grenade",
            LootKind.FlareGun => "FLARE GUN  (G = call a supply drop)",
            LootKind.Scope => $"{(level >= 3 ? "8x" : level == 2 ? "4x" : "2x")} Scope",
            _ => label,
        };

        /// returns true when the item was used up
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
                case LootKind.Medkit: if (!h) return false; h.Heal(100f, 100f); return true;
                case LootKind.Bandage: if (!h) return false; h.Heal(10f * amount, 75f); return true;
                case LootKind.Vest: if (!h) return false; h.vestReduction = new[] { 0f, 0.3f, 0.4f, 0.55f }[level]; h.vestDurability = 100f + 50f * level; return true;
                case LootKind.Helmet: if (!h) return false; h.helmetReduction = new[] { 0f, 0.3f, 0.4f, 0.55f }[level]; h.helmetDurability = 80f + 40f * level; return true;
                case LootKind.Backpack: if (level <= gear.backpack) return false; gear.SetBackpack(level); return true;
                case LootKind.Drink: gear.drinks++; return true;
                case LootKind.Syringe: gear.syringes++; return true;
                case LootKind.Grenade: gear.grenades++; return true;
                case LootKind.FlareGun: gear.flares++; return true;
                case LootKind.Scope: return w && w.AttachScope(level >= 3 ? ScopeType.X8 : level == 2 ? ScopeType.X4 : ScopeType.X2);
            }
            return false;
        }
    }
}

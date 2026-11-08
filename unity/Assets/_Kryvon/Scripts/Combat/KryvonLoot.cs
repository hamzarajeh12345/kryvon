// Ground loot: weapons, ammo, medkits, vests, helmets. Press F (Interact) near an item to take it.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public enum LootKind { Weapon, Ammo, Medkit, Bandage, Vest, Helmet }

    public class KryvonLoot : MonoBehaviour
    {
        public LootKind kind;
        public WeaponData weapon;
        public AmmoType ammoType;
        public int amount = 30;
        [Range(0, 3)] public int level = 1;           // armour level / rarity
        public string label = "";

        public static readonly List<KryvonLoot> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Describe() => kind switch
        {
            LootKind.Weapon => weapon ? $"{weapon.arabicName} · {weapon.displayName}" : "سلاح",
            LootKind.Ammo => $"ذخيرة {ammoType} ×{amount}",
            LootKind.Medkit => "شنطة إسعاف",
            LootKind.Bandage => $"شاش ×{amount}",
            LootKind.Vest => $"درع مستوى {level}",
            LootKind.Helmet => $"خوذة مستوى {level}",
            _ => label,
        };

        /// returns true when the item was used up
        public bool Apply(GameObject player)
        {
            var w = player.GetComponent<WeaponController>(); var h = player.GetComponent<Health>();
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
            }
            return false;
        }
    }
}

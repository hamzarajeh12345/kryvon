// Melee weapons: the 4th slot. Real scanned models (Poly Haven, CC0) in Resources/Melee, placed around the map
// next to other loot when a match starts.
//   Atala (crowbar) · Balta (hatchet) · Asaya (bat) · Maqlaya (frying pan)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public static class KryvonMelee
    {
        // model, name, Arabic name, damage, swings per minute, reach (m), length on the ground (m)
        static readonly (string model, string name, string ar, float dmg, float rpm, float reach, float len)[] Kinds =
        {
            ("crowbar_01",   "Atala",   "عتلة",   34f, 85f, 1.9f, 0.75f),
            ("hatchet",      "Balta",   "بلطة",   45f, 65f, 1.8f, 0.45f),
            ("baseball_bat", "Asaya",   "عصاية",  38f, 75f, 2.1f, 0.85f),
            ("brass_pan_01", "Maqlaya", "مقلاية", 30f, 95f, 1.7f, 0.5f),
        };
        static List<WeaponData> catalog;
        public static IReadOnlyList<WeaponData> Catalog
        {
            get
            {
                if (catalog != null) return catalog;
                catalog = new List<WeaponData>();
                foreach (var k in Kinds)
                {
                    var w = ScriptableObject.CreateInstance<WeaponData>();
                    w.name = k.name; w.weaponId = k.model; w.displayName = k.name; w.arabicName = k.ar; w.weaponClass = WeaponClass.Melee;
                    w.damage = k.dmg; w.roundsPerMinute = k.rpm; w.range = k.reach; w.magazineSize = 0; w.drawSeconds = 0.35f;
                    catalog.Add(w);
                }
                return catalog;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded += (s, _) => { if (s.name == "Play") Scatter(); }; if (SceneManager.GetActiveScene().name == "Play") Scatter(); }

        /// put melee weapons next to some of the loot already on the map
        static void Scatter()
        {
            var spots = new List<KryvonLoot>(KryvonLoot.All);
            if (spots.Count < 50) return;
            int count = Mathf.Clamp(spots.Count / 25, 30, 90), made = 0;
            for (int i = 0; i < count * 3 && made < count; i++)
            {
                var near = spots[Random.Range(0, spots.Count)]; if (!near || near.kind == LootKind.Weapon) continue;
                int k = Random.Range(0, Kinds.Length);
                var prefab = Resources.Load<GameObject>($"Melee/{Kinds[k].model}/{Kinds[k].model}");
                if (!prefab) { Debug.LogWarning("KRYVON: melee model missing " + Kinds[k].model); return; }
                var p = near.transform.position + new Vector3(Random.Range(-0.6f, 0.6f), 0.6f, Random.Range(-0.6f, 0.6f));
                if (Physics.Raycast(p, Vector3.down, out var g, 3f, ~0, QueryTriggerInteraction.Ignore)) p = g.point;
                var root = new GameObject("Melee_" + Kinds[k].name);
                var m = Object.Instantiate(prefab, root.transform);
                foreach (var c in m.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                // size it, lay it flat, rest it on the ground
                var rs = m.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) { Object.Destroy(root); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                m.transform.localScale *= Kinds[k].len / Mathf.Max(0.01f, longest);
                if (b.size.y >= Mathf.Max(b.size.x, b.size.z)) m.transform.localRotation = Quaternion.Euler(0, 0, 90);
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                m.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
                root.transform.SetPositionAndRotation(p + Vector3.up * 0.01f, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                var loot = root.AddComponent<KryvonLoot>(); loot.kind = LootKind.Weapon; loot.weapon = Catalog[k]; loot.rarity = 1;
                made++;
            }
            Debug.Log($"KRYVON: {made} melee weapons placed");
        }
    }
}

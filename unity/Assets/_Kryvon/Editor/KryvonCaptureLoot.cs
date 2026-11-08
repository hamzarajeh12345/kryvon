// Close-up shots of ground loot (one per kind) and of an area's vehicles, for checking the look.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonCaptureLoot
    {
        public static void Shoot()
        {
            var cam = Camera.main; var dir = Directory.GetCurrentDirectory(); float fov = cam.fieldOfView; cam.fieldOfView = 40;
            var all = Object.FindObjectsByType<KryvonLoot>(FindObjectsInactive.Exclude);
            int i = 0;
            foreach (var kind in new[] { LootKind.Weapon, LootKind.Ammo, LootKind.Medkit, LootKind.Bandage, LootKind.Drink, LootKind.Syringe, LootKind.Grenade, LootKind.Vest, LootKind.Backpack, LootKind.Scope, LootKind.FlareGun, LootKind.Helmet })
            {
                var l = all.FirstOrDefault(x => x.kind == kind); if (!l) continue;
                var p = l.transform.position;
                cam.transform.position = p + new Vector3(0.9f, 0.75f, -0.9f); cam.transform.LookAt(p + Vector3.up * 0.1f);
                KryvonShowcase.Capture(Path.Combine(dir, $"Loot{i++}.png"), 480, 360);
            }
            var v = Object.FindObjectsByType<KryvonVehicle>(FindObjectsInactive.Exclude).Where(x => x.displayName == "Buggy").Skip(2).FirstOrDefault();
            if (v) { cam.fieldOfView = 55; cam.transform.position = v.transform.position + new Vector3(7, 3.5f, -7); cam.transform.LookAt(v.transform.position + Vector3.up); KryvonShowcase.Capture(Path.Combine(dir, "Cars.png"), 1280, 720); }
            cam.fieldOfView = fov;
        }
    }
}

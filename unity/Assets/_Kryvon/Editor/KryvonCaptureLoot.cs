// Close-up shots of ground loot (one per kind) and of an area's vehicles, for checking the look.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonCaptureLoot
    {
        public static void Arsenal()
        {
            var cam = Camera.main; var dir = Directory.GetCurrentDirectory(); float fov = cam.fieldOfView;
            var rack = new GameObject("ArsenalRack"); rack.transform.position = new Vector3(0, 300, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(rack.transform); floor.transform.localPosition = new Vector3(0, -0.05f, 0); floor.transform.localScale = new Vector3(12, 0.1f, 9);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Kryvon/Materials/M_Concrete.mat");
            var arms = KryvonWorld.Arsenal; int n = 0;
            foreach (var a in arms)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Kryvon/Models/Weapons/{a.model}.glb"); if (!src) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, rack.transform);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (a.len / b.size.z); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                int col = n % 2, row = n / 2;
                var target = rack.transform.position + new Vector3(-2.6f + col * 5.2f, 0.05f, 3.6f - row * 0.66f);
                g.transform.position += target - new Vector3(b.center.x, b.min.y, b.center.z);
                g.transform.RotateAround(target, Vector3.up, 90);
                n++;
            }
            cam.fieldOfView = 34; cam.transform.position = rack.transform.position + new Vector3(0, 11.5f, -2.2f); cam.transform.LookAt(rack.transform.position + new Vector3(0, 0, 0.1f));
            KryvonShowcase.Capture(Path.Combine(dir, "Arsenal.png"), 1600, 1600);
            cam.fieldOfView = fov; Object.DestroyImmediate(rack);
        }

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

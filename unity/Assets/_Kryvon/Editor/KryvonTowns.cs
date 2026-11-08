// Dense neighbourhoods (30 buildings each: 2–4 storey houses and 6–9 storey residential towers on a street grid),
// working doors and breakable windows for every house, armour models for the ground, and graves for the dead.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        // town centres, also flattened in the terrain
        public static readonly Vector2[] Towns = { new(-250, 200), new(230, -230), new(260, 210) };
        const int TownCols = 6, TownRows = 5; const float Cell = 26f;
        public static Vector2 TownHalf => new(TownCols * Cell / 2 + 8, TownRows * Cell / 2 + 8);

        static Material glassMat, doorMat;
        static AudioClip K(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/{n}.ogg");

        static void Specials(Builder b, Transform house)
        {
            if (glassMat == null)
            {
                glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                glassMat.SetFloat("_Surface", 1); glassMat.SetFloat("_Blend", 0); glassMat.SetOverrideTag("RenderType", "Transparent");
                glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                glassMat.SetInt("_ZWrite", 0); glassMat.renderQueue = 3000; glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glassMat.SetColor("_BaseColor", new Color(0.55f, 0.7f, 0.75f, 0.32f)); glassMat.SetFloat("_Smoothness", 0.95f); glassMat.SetFloat("_Metallic", 0.1f);
                glassMat.enableInstancing = true;
                AssetDatabase.DeleteAsset(Root + "/Materials/M_Glass.mat"); AssetDatabase.CreateAsset(glassMat, Root + "/Materials/M_Glass.mat");
                doorMat = PBRMat("M_Door", "laminate_floor_02", new Color(0.55f, 0.36f, 0.22f), 1.2f);
                doorMat.enableInstancing = true;
            }
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var glassSnd = K("impactGlass_heavy_000"); var doorSnd = K("impactWood_medium_000") ?? K("impactPlank_medium_000");
            var fx = new GameObject("DoorsWindows").transform; fx.SetParent(house, false);
            foreach (var (c, axis, w, h) in b.windows)
            {
                var g = new GameObject("Glass"); g.transform.SetParent(fx, false);
                g.transform.localPosition = c; g.transform.localRotation = Quaternion.Euler(0, axis == 'x' ? 0 : 90, 0);
                g.transform.localScale = new Vector3(w, h, 0.04f);
                g.AddComponent<MeshFilter>().sharedMesh = cube; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = glassMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.AddComponent<BoxCollider>();
                var win = g.AddComponent<KryvonWindow>(); Set(win, "shatterSound", glassSnd);
            }
            foreach (var (c, axis, w, h) in b.doors)
            {
                // pivot on the hinge edge; the hinge child rotates
                var pivot = new GameObject("Door"); pivot.transform.SetParent(fx, false);
                var along = axis == 'x' ? Vector3.right : Vector3.forward;
                pivot.transform.localPosition = c - along * (w / 2) - Vector3.up * (h / 2);
                pivot.transform.localRotation = Quaternion.Euler(0, axis == 'x' ? 0 : -90, 0);
                var hinge = new GameObject("Hinge"); hinge.transform.SetParent(pivot.transform, false);
                var panel = new GameObject("Panel"); panel.transform.SetParent(hinge.transform, false);
                panel.transform.localPosition = new Vector3(w / 2, h / 2, 0); panel.transform.localScale = new Vector3(w - 0.04f, h - 0.03f, 0.06f);
                panel.AddComponent<MeshFilter>().sharedMesh = cube; panel.AddComponent<MeshRenderer>().sharedMaterial = doorMat;
                panel.AddComponent<BoxCollider>();
                var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(knob.GetComponent<Collider>());
                knob.transform.SetParent(hinge.transform, false); knob.transform.localPosition = new Vector3(w - 0.12f, 1.0f, 0.05f); knob.transform.localScale = Vector3.one * 0.06f;
                knob.GetComponent<Renderer>().sharedMaterial = PBRMat("M_Rail", "concrete_floor_02", new Color(0.18f, 0.18f, 0.19f), 1f);
                var d = hinge.AddComponent<KryvonDoor>(); Set(d, "openSound", doorSnd); Set(d, "closeSound", doorSnd);
            }
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field); if (p == null) return;
            p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- armour lying on the ground ----------
        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(t); Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localRotation = Quaternion.Euler(euler);
            g.GetComponent<Renderer>().sharedMaterial = m; return g;
        }
        static GameObject ArmorModel(LootKind k, Transform parent, Vector3 p)
        {
            var root = new GameObject(k.ToString()); root.transform.SetParent(parent, false);
            root.transform.position = p; root.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
            var cloth = PBRMat("M_ArmorCloth", "concrete_floor_02", new Color(0.23f, 0.26f, 0.2f), 0.4f);
            var dark = PBRMat("M_ArmorDark", "concrete_floor_02", new Color(0.12f, 0.13f, 0.12f), 0.4f);
            if (k == LootKind.Vest)
            {
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0.05f, 0), new Vector3(0.46f, 0.09f, 0.56f), cloth);           // carrier
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0.11f, -0.02f), new Vector3(0.34f, 0.04f, 0.4f), dark);       // plate
                for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, root.transform, new Vector3(-0.13f + i * 0.13f, 0.14f, 0.18f), new Vector3(0.11f, 0.07f, 0.12f), cloth);  // mag pouches
                foreach (var sx in new[] { -0.17f, 0.17f }) Prim(PrimitiveType.Cube, root.transform, new Vector3(sx, 0.08f, -0.3f), new Vector3(0.08f, 0.03f, 0.16f), dark);    // shoulder straps
            }
            else
            {
                Prim(PrimitiveType.Sphere, root.transform, new Vector3(0, 0.08f, 0), new Vector3(0.3f, 0.24f, 0.34f), cloth);         // shell
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.02f, 0), new Vector3(0.32f, 0.012f, 0.36f), dark);       // rim
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0.17f, 0.15f), new Vector3(0.07f, 0.05f, 0.04f), dark);        // NVG mount
            }
            return root;
        }

        // ---------- graves ----------
        /// adds a hidden grave (tombstone, mound, loot crate) and an inventory to a character; KryvonDeath shows it on death
        public static void MakeGrave(GameObject who, float respawn)
        {
            if (lootGuns.Count == 0) EnsureLootGuns();
            rng ??= new System.Random(7);
            var stone = PBRMat("M_Tomb", "concrete_wall_008", new Color(0.78f, 0.76f, 0.72f), 1f);
            var dirt = PBRMat("M_GraveDirt", "dry_ground_01", new Color(0.55f, 0.45f, 0.35f), 1f);
            var wood = PBRMat("M_Crate", "laminate_floor_02", new Color(0.62f, 0.45f, 0.28f), 0.8f);
            var grave = new GameObject("Grave"); grave.transform.SetParent(who.transform, false);
            Prim(PrimitiveType.Sphere, grave.transform, new Vector3(0, 0.02f, 0.2f), new Vector3(0.9f, 0.25f, 1.9f), dirt);
            Prim(PrimitiveType.Cube, grave.transform, new Vector3(0, 0.45f, -0.75f), new Vector3(0.55f, 0.8f, 0.12f), stone);
            Prim(PrimitiveType.Cylinder, grave.transform, new Vector3(0, 0.85f, -0.75f), new Vector3(0.55f, 0.06f, 0.55f), stone, new Vector3(90, 0, 0));
            var crate = Prim(PrimitiveType.Cube, grave.transform, new Vector3(0.9f, 0.25f, 0.2f), new Vector3(0.7f, 0.5f, 0.5f), wood);
            Prim(PrimitiveType.Cube, crate.transform, new Vector3(0, 0.52f, 0), new Vector3(1.04f, 0.06f, 1.04f), PBRMat("M_Rail", "concrete_floor_02", new Color(0.18f, 0.18f, 0.19f), 1f));
            var light = new GameObject("Glow").AddComponent<Light>(); light.transform.SetParent(crate.transform, false); light.transform.localPosition = Vector3.up * 1.4f;
            light.type = LightType.Point; light.range = 3.5f; light.intensity = 1.6f; light.color = new Color(1f, 0.75f, 0.3f); light.shadows = LightShadows.None;
            crate.AddComponent<KryvonDeathBox>();
            grave.SetActive(false);

            var inv = new GameObject("Inventory"); inv.transform.SetParent(who.transform, false);
            void Item(LootKind k, System.Action<KryvonLoot> f) { var g = new GameObject(k.ToString()); g.transform.SetParent(inv.transform, false); var l = g.AddComponent<KryvonLoot>(); l.kind = k; f(l); }
            var gun = lootGuns[rng.Next(lootGuns.Count)];
            Item(LootKind.Weapon, l => l.weapon = gun);
            Item(LootKind.Ammo, l => { l.ammoType = gun.ammoType; l.amount = gun.magazineSize * 3; });
            Item(LootKind.Bandage, l => l.amount = 4);
            if (rng.NextDouble() < 0.6) Item(LootKind.Medkit, l => { });
            Item(LootKind.Vest, l => l.level = 1 + rng.Next(3));
            if (rng.NextDouble() < 0.6) Item(LootKind.Helmet, l => l.level = 1 + rng.Next(3));
            inv.SetActive(false);

            var death = who.GetComponent<KryvonDeath>() ?? who.AddComponent<KryvonDeath>();
            var so = new SerializedObject(death);
            so.FindProperty("grave").objectReferenceValue = grave; so.FindProperty("inventoryTemplate").objectReferenceValue = inv.transform;
            so.FindProperty("respawnAfter").floatValue = respawn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void CaptureTowns()
        {
            var cam = Camera.main; var dir = System.IO.Directory.GetCurrentDirectory(); float fov = cam.fieldOfView; cam.fieldOfView = 60;
            var ter = Terrain.activeTerrain; float G(float x, float z) => ter.SampleHeight(new Vector3(x, 0, z)) + ter.transform.position.y;
            var t = Towns[1];
            var shots = new (Vector3 p, Vector3 l)[] {
                (new Vector3(t.x - 110, G(t.x, t.y) + 55, t.y - 110), new Vector3(t.x, G(t.x, t.y), t.y)),                         // aerial
                (new Vector3(t.x - Cell * 2, G(t.x, t.y) + 1.7f, t.y - Cell * 2.5f + 0.5f), new Vector3(t.x + Cell * 2, G(t.x, t.y) + 4f, t.y - Cell * 2.5f)),   // down a street
                (new Vector3(t.x - Cell * 0.5f + 9, G(t.x, t.y) + 1.8f, t.y + Cell * 2.5f + 0.2f), new Vector3(t.x - Cell * 0.5f, G(t.x, t.y) + 3f, t.y + Cell * 1.5f)), // facade + door
                (new Vector3(t.x + 40, G(t.x, t.y) + 22, t.y - 75), new Vector3(t.x + 10, G(t.x, t.y) + 12, t.y)) };                // towers
            for (int i = 0; i < shots.Length; i++) { cam.transform.position = shots[i].p; cam.transform.LookAt(shots[i].l); KryvonShowcase.Capture(System.IO.Path.Combine(dir, $"Town{i}.png"), 1280, 720); }
            cam.fieldOfView = fov;
        }

        // ---------- vehicles: in every area two cars, a motorcycle and a buggy ----------
        static readonly Dictionary<string, (string name, float len, float wid, float speed, float accel, float turn, Vector3 seat, bool lean)> VehicleStats = new()
        {
            { "v_pickup", ("Pickup", 5.0f, 2.0f, 30, 8.5f, 62, new Vector3(-0.42f, 0.75f, 0.35f), false) },
            { "v_sedan", ("Sedan", 4.5f, 1.85f, 34, 10, 68, new Vector3(-0.4f, 0.5f, -0.1f), false) },
            { "v_jeep", ("Jeep", 3.9f, 1.85f, 31, 11, 72, new Vector3(-0.42f, 0.6f, -0.2f), false) },
            { "v_armored", ("Armored Car", 5.3f, 2.3f, 24, 6, 50, new Vector3(-0.5f, 0.95f, 0.3f), false) },
            { "v_minibus", ("Service Minibus", 5.6f, 2.05f, 25, 6.5f, 52, new Vector3(-0.5f, 0.8f, 1.6f), false) },
            { "v_moto", ("Motorcycle", 2.1f, 0.8f, 38, 13, 95, new Vector3(0, 0.65f, -0.15f), true) },
            { "v_buggy", ("Buggy", 3.4f, 1.9f, 33, 12, 82, new Vector3(-0.38f, 0.45f, -0.1f), false) },
        };
        public static GameObject MakeVehicle(string model, Vector3 pos, float yaw, Transform parent)
        {
            var st = VehicleStats[model];
            var g = Spawn("Vehicles/" + model, st.len, 'z', parent); if (g == null) return null;
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Collide(g);
            var v = g.AddComponent<KryvonVehicle>(); v.displayName = st.name;
            var so = new SerializedObject(v);
            so.FindProperty("maxSpeed").floatValue = st.speed; so.FindProperty("accel").floatValue = st.accel; so.FindProperty("turnRate").floatValue = st.turn;
            so.FindProperty("length").floatValue = st.len; so.FindProperty("width").floatValue = st.wid; so.FindProperty("seat").vector3Value = st.seat; so.FindProperty("lean").boolValue = st.lean;
            so.FindProperty("engine").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/vehicle_engine.wav");
            so.FindProperty("crash").objectReferenceValue = K("impactMetal_heavy_000");
            so.ApplyModifiedPropertiesWithoutUndo();
            return g;
        }
        static void PlaceVehicles(Transform parent)
        {
            Physics.SyncTransforms();
            var root = new GameObject("AreaVehicles").transform; root.SetParent(parent, false);
            var areas = new List<Vector2> { Vector2.zero };
            foreach (var t in Towns) areas.Add(t);
            foreach (var (c, _) in Sites) areas.Add(c);
            var hs = GameObject.Find("Houses");
            if (hs) for (int i = 0; i < hs.transform.childCount && areas.Count < 12; i += 4) { var p = hs.transform.GetChild(i).position; areas.Add(new Vector2(p.x, p.z)); }
            string[] cars = { "v_pickup", "v_sedan", "v_jeep", "v_pickup", "v_sedan", "v_minibus", "v_armored" };
            int made = 0;
            foreach (var a in areas)
            {
                var list = new List<string> { cars[rng.Next(cars.Length)], cars[rng.Next(cars.Length)], "v_moto", "v_buggy" };
                foreach (var m in list)
                {
                    var st = VehicleStats[m];
                    for (int t = 0; t < 60; t++)
                    {
                        float ang = R(0, Mathf.PI * 2), rad = R(12, 55);
                        float x = a.x + Mathf.Cos(ang) * rad, z = a.y + Mathf.Sin(ang) * rad;
                        if (!Physics.Raycast(new Vector3(x, 400, z), Vector3.down, out var hit, 800, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Vector3.Angle(hit.normal, Vector3.up) > 12) continue;
                        if (hit.point.y > 0.6f + (Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(hit.point) + Terrain.activeTerrain.transform.position.y : hit.point.y)) continue;   // not on a roof
                        float yaw = R(0, 360);
                        var half = new Vector3(st.wid / 2 + 0.4f, 0.8f, st.len / 2 + 0.4f);
                        if (Physics.CheckBox(hit.point + Vector3.up * 1.15f, half, Quaternion.Euler(0, yaw, 0), ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (MakeVehicle(m, hit.point, yaw, root)) { made++; Physics.SyncTransforms(); }
                        break;
                    }
                }
            }
            Debug.Log($"KRYVON: {made} drivable vehicles in {areas.Count} areas");
        }

        // ---------- flare guns: exactly five on the whole map ----------
        static void PlaceFlareGuns()
        {
            int placed = 0; var used = new HashSet<int>();
            while (placed < 5 && used.Count < lootSpotsUsed.Count)
            {
                int i = rng.Next(lootSpotsUsed.Count); if (!used.Add(i)) continue;
                var (parent, p) = lootSpotsUsed[i];
                DropLoot(parent, p + new Vector3(0.6f, 0, 0.4f), LootKind.FlareGun); placed++;
            }
            Debug.Log($"KRYVON: {placed} flare guns hidden on the map");
        }

        // ---------- supply drop template (cloned at runtime by KryvonAirdrop) ----------
        public static GameObject MakeAirdropTemplate(Transform parent)
        {
            if (lootGuns.Count == 0) EnsureLootGuns();
            rng ??= new System.Random(11);
            var root = new GameObject("AirdropTemplate"); root.transform.SetParent(parent, false);
            var crate = Spawn("Loot/l_crate", 1.3f, 'x', root.transform);
            if (crate) { Collide(crate); crate.AddComponent<KryvonDeathBox>(); }
            var chute = new GameObject("Chute").transform; chute.SetParent(root.transform, false);
            var cloth = PBRMat("M_Chute", "concrete_floor_02", new Color(0.9f, 0.3f, 0.12f), 1f);
            Prim(PrimitiveType.Sphere, chute, new Vector3(0, 7.5f, 0), new Vector3(7f, 2.6f, 7f), cloth);
            for (int i = 0; i < 4; i++) { float a = i * 90 + 45; Prim(PrimitiveType.Cylinder, chute, Quaternion.Euler(0, a, 0) * new Vector3(0, 4.2f, 1.7f), new Vector3(0.03f, 3.6f, 0.03f), cloth, new Vector3(25, a, 0)); }
            var light = new GameObject("Beacon").AddComponent<Light>(); light.transform.SetParent(root.transform, false); light.transform.localPosition = Vector3.up * 2f;
            light.type = LightType.Point; light.color = new Color(1f, 0.25f, 0.1f); light.range = 6; light.intensity = 3;
            var inv = new GameObject("Inventory"); inv.transform.SetParent(root.transform, false);
            void Item(LootKind k, System.Action<KryvonLoot> f) { var g = new GameObject(k.ToString()); g.transform.SetParent(inv.transform, false); var l = g.AddComponent<KryvonLoot>(); l.kind = k; f(l); }
            var best = lootGuns.Find(w => w.weaponClass == WeaponClass.Sniper) ?? lootGuns[0];
            var lmg = lootGuns.Find(w => w.weaponClass == WeaponClass.LMG) ?? lootGuns[0];
            Item(LootKind.Weapon, l => l.weapon = best); Item(LootKind.Ammo, l => { l.ammoType = best.ammoType; l.amount = 20; });
            Item(LootKind.Weapon, l => l.weapon = lmg); Item(LootKind.Ammo, l => { l.ammoType = lmg.ammoType; l.amount = 150; });
            Item(LootKind.Vest, l => l.level = 3); Item(LootKind.Helmet, l => l.level = 3); Item(LootKind.Backpack, l => l.level = 3);
            Item(LootKind.Scope, l => l.level = 3); Item(LootKind.Medkit, l => { }); Item(LootKind.Syringe, l => { }); Item(LootKind.Medkit, l => { });
            inv.SetActive(false);
            root.SetActive(false);
            return root;
        }

        // ---------- dense neighbourhoods ----------
        static void BuildTowns(Terrain terrain, Transform parent)
        {
            float Y(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            var road = PBRMat("M_TownRoad", "asphalt_02", new Color(0.85f, 0.85f, 0.85f), 6f);
            var pave = PBRMat("M_TownPave", "cobblestone_floor_08", new Color(1f, 0.96f, 0.9f), 2.5f);
            var root = new GameObject("Towns").transform; root.SetParent(parent, false);
            var lootRoot = new GameObject("TownLoot").transform; lootRoot.SetParent(parent, false);
            int meshId = 10000, made = 0, towers = 0, loot = 0;
            foreach (var t in Towns)
            {
                var town = new GameObject("Town").transform; town.SetParent(root, false);
                float gy = Y(t.x, t.y);
                var ground = new Builder();
                void Strip(Transform _, Material m, Vector3 c, Vector3 sz) => ground.Box(m, c - new Vector3(t.x, gy, t.y), sz);
                // street grid
                for (int i = 0; i <= TownCols; i++) Strip(town, road, new Vector3(t.x - TownCols * Cell / 2 + i * Cell, gy + 0.02f, t.y), new Vector3(7, 0.04f, TownRows * Cell + 7));
                for (int j = 0; j <= TownRows; j++) Strip(town, road, new Vector3(t.x, gy + 0.025f, t.y - TownRows * Cell / 2 + j * Cell), new Vector3(TownCols * Cell + 7, 0.04f, 7));
                for (int i = 0; i < TownCols; i++) for (int j = 0; j < TownRows; j++)
                {
                    float cx = t.x - TownCols * Cell / 2 + (i + 0.5f) * Cell, cz = t.y - TownRows * Cell / 2 + (j + 0.5f) * Cell;
                    Strip(town, pave, new Vector3(cx, gy + 0.08f, cz), new Vector3(Cell - 7, 0.16f, Cell - 7));
                    double roll = rng.NextDouble();
                    int floors = roll < 0.28 ? 2 : roll < 0.56 ? 3 : roll < 0.82 ? 4 : 6 + rng.Next(4);
                    bool tower = floors >= 6;
                    float w = tower ? R(15f, 17f) : R(10.5f, 14.5f), d = tower ? R(13f, 15f) : R(9.5f, 13f);
                    float yaw = (j < TownRows / 2 ? 180f : 0f) + (rng.NextDouble() < 0.2 ? 90f : 0f);   // face a street
                    float hi = gy + 0.16f;
                    for (int a = -1; a <= 1; a++) for (int c = -1; c <= 1; c++) hi = Mathf.Max(hi, Y(cx + a * w / 2, cz + c * d / 2));
                    var b = new Builder();
                    var spots = HouseShape(b, w, d, floors, 1.2f);
                    var go = b.Build(tower ? "Tower" : "House", town, new Vector3(cx, hi + 0.05f, cz), yaw, ref meshId);
                    foreach (var s in spots) if (rng.NextDouble() < (tower ? 0.55 : 0.9)) { DropLoot(lootRoot, go.transform.TransformPoint(s)); loot++; }
                    made++; if (tower) towers++;
                }
                ground.Build("Streets", town, new Vector3(t.x, gy, t.y), 0, ref meshId);
                blocked.Add(new Rect(t.x - TownHalf.x, t.y - TownHalf.y, TownHalf.x * 2, TownHalf.y * 2));
            }
            Debug.Log($"KRYVON: towns built – {made} buildings ({towers} towers), {loot} loot items");
        }
        static void Strip(Transform parent, Material m, Vector3 c, Vector3 s)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Street"; g.transform.SetParent(parent, false);
            g.transform.position = c; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}

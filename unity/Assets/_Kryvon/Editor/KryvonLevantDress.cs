// Dresses the Levant map for the match: cities from KryvonLevant.TownPlan on their flat plates, Petra, the Roman
// columns of Heliopolis, villages and farm houses spread over the land (never on a road, in the sea or on a cliff),
// Beirut port containers and hangars, olive groves / cedar forest / palms, cars, flare guns.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        [UnityEditor.MenuItem("KRYVON/World/Dress complete Levant map")]
        public static void BuildCompleteLevant()
        {
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Kryvon/Settings/LevantTerrain.asset") == null)
            {
                KryvonLevant.Build();
            }
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Kryvon/Scenes/Levant.unity");
                terrain = Terrain.activeTerrain;
            }
            if (terrain != null) DressLevant(terrain);
        }

        public static void DressLevant(Terrain terrain)
        {
            rng = new System.Random(1946); blocked.Clear(); lootSpotsUsed.Clear();
            var (W, L, SEA) = KryvonLevant.Size();
            var world = new GameObject("Outskirts").transform;
            var plan = KryvonLevant.TownPlan;
            Towns = new Vector2[plan.Length]; TownShapes = new (int, int, int, int, float)[plan.Length];
            for (int i = 0; i < plan.Length; i++) { Towns[i] = KryvonLevant.RegionPos(plan[i].region); TownShapes[i] = (plan[i].cols, plan[i].rows, plan[i].core, plan[i].edge, plan[i].towers); }
            Sites[0] = (KryvonLevant.RegionPos("Petra"), 75f);
            Sites[1] = (KryvonLevant.RegionPos("Heliopolis"), 70f);
            var bse = KryvonLevant.RegionPos("Waiting Area"); blocked.Add(new Rect(bse.x - 120, bse.y - 100, 240, 200));

            float Y(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            float Steep(float x, float z) => terrain.terrainData.GetSteepness((x - terrain.transform.position.x) / W, (z - terrain.transform.position.z) / L);
            bool Land(float x, float z, float r) => Mathf.Abs(x) < W / 2 - 40 && Mathf.Abs(z) < L / 2 - 40 && Y(x, z) > SEA + 2f && Steep(x, z) < 14f && Free(new Vector2(x, z), r)
                && Clear(new Vector3(x, Y(x, z) + 3, z), new Vector3(r, 4, r));
            bool Clear(Vector3 c, Vector3 half) { foreach (var col in Physics.OverlapBox(c, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) if (!(col is TerrainCollider)) return false; return true; }

            AssetDatabase.StartAssetEditing();
            try
            {
                BuildTowns(terrain, world); BuildPetra(terrain, world); BuildJerash(terrain, world);
                var qaia = KryvonLevant.RegionPos("QAIA");
                KryvonQaia.BuildAt(world, terrain, qaia);
                // the military hotspot: a real-size apron, hangars, tower, radar masts,
                // two helicopters, tanks/armoured vehicles and a dense compound layout.
                var waiting = KryvonLevant.RegionPos("Waiting Area");
                BuildMilitaryArea(world, terrain, waiting);
                foreach (var (sc, sr) in Sites) blocked.Add(new Rect(sc.x - sr, sc.y - sr, sr * 2, sr * 2));
                Physics.SyncTransforms();

                // ---- villages and lone farm houses ----
                var houses = new GameObject("Houses").transform; houses.SetParent(world, false);
                var loot = new GameObject("Loot").transform; loot.SetParent(world, false);
                int meshId = 0, made = 0;
                void House(float x, float z, int tier)
                {
                    double fr = rng.NextDouble(); int floors = fr < 0.3 ? 1 : fr < 0.78 ? 2 : 3;            // mostly low houses, a few three-storey
                    float w = floors == 1 ? R(8f, 11f) : R(9.5f, 14f), d = floors == 1 ? R(7.5f, 10f) : R(8.5f, 12f); float rad = Mathf.Max(w, d) * 0.75f;
                    if (!Land(x, z, rad)) return;
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) { float h = Y(x + i * w / 2, z + j * d / 2); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
                    var b = new Builder(); lootTier = tier;
                    var spots = HouseShape(b, w, d, floors, hi - lo + 0.6f);
                    var go = b.Build("House", houses, new Vector3(x, hi + 0.05f, z), rng.Next(4) * 90f, ref meshId);
                    foreach (var m in StairMarks) m.transform.SetParent(go.transform, false); StairMarks.Clear();
                    foreach (var s in spots) DropLoot(loot, go.transform.TransformPoint(s));
                    blocked.Add(new Rect(x - rad, z - rad, rad * 2, rad * 2)); Physics.SyncTransforms(); made++;
                }
                (string name, int clusters, float radius, int tier)[] villages =
                {
                    ("Joash & Alloun", 7, 90, 2), ("Olive Plains", 6, 130, 1), ("Tal Al-Rumman", 5, 90, 2), ("Wadi Araba", 4, 160, 1), ("Badia Desert", 4, 190, 1),
                    ("Cedar Hills", 4, 160, 2), ("Wadi Musa", 4, 120, 2), ("Heliopolis", 3, 110, 2), ("The Outskirts", 5, 150, 2), ("Kharbet Saba", 4, 110, 1), ("Dead Sea", 3, 130, 1),
                };
                foreach (var (name, n, radius, tier) in villages)
                {
                    var c = KryvonLevant.RegionPos(name);
                    for (int k = 0; k < n; k++)
                    {
                        float a = R(0, Mathf.PI * 2), rr = R(radius * 0.3f, radius);
                        float cx = c.x + Mathf.Cos(a) * rr, cz = c.y + Mathf.Sin(a) * rr;
                        int m = rng.NextDouble() < 0.4 ? 1 : rng.NextDouble() < 0.6 ? 2 : 3;
                        for (int h = 0; h < m; h++) House(cx + (h == 0 ? 0 : R(-20, 20)), cz + (h == 0 ? 0 : R(-20, 20)), tier);
                    }
                }
                for (int k = 0; k < 40; k++) House(R(-W / 2, W / 2), R(-L / 2, L / 2), 1);     // lone farms all over
                Debug.Log($"KRYVON: {made} enterable houses on the Levant map");

                // ---- Beirut port: container stacks and warehouses ----
                var port = KryvonLevant.RegionPos("Beirut Port");
                GameObject Put(string model, float x, float z, float yaw, float size, char axis, bool collide = true)
                {
                    if (System.IO.File.Exists($"{Root}/Models/{model}_b.glb") && rng.NextDouble() < 0.5) model += "_b";
                    var g = Spawn(model, size, axis, world); if (g == null) return null;
                    g.transform.position = new Vector3(x, Y(x, z) - 0.05f, z); g.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    if (collide) Collide(g); return g;
                }
                for (int k = 0; k < 46; k++)
                {
                    float x = port.x + R(-150, 150), z = port.y + R(-120, 120); if (!Land(x, z, 4)) continue;
                    var g = Put("Props/p_container", x, z, rng.Next(2) * 90, 6.1f, 'z');
                    if (g && rng.NextDouble() < 0.4) { var top = Put("Props/p_container", x, z, rng.Next(2) * 90, 6.1f, 'z'); if (top) top.transform.position += Vector3.up * 2.6f; }
                    blocked.Add(new Rect(x - 4, z - 4, 8, 8)); Physics.SyncTransforms();
                }
                // (warehouses you can enter are built in DressRoadsAndGaps – no solid hangars)
                var orak = KryvonLevant.RegionPos("Al-Orak");


                // ---- props in the countryside ----
                for (int k = 0; k < 40; k++) { float x = R(-W / 2, W / 2), z = R(-L / 2, L / 2); if (Land(x, z, 3)) { Put("Props/p_wreck", x, z, R(0, 360), 4.3f, 'x'); blocked.Add(new Rect(x - 3, z - 3, 6, 6)); } }
                for (int k = 0; k < 30; k++) { float x = R(-W / 2, W / 2), z = R(-L / 2, L / 2); if (Land(x, z, 2)) Put("Props/p_sandbags", x, z, R(0, 360), 3.2f, 'x'); }

                if (KryvonLevant.RoadPaths.Count > 0) DressRoadsAndGaps(world, (lx, lz, lr) => Land(lx, lz, lr), (yx, yz) => Y(yx, yz), blocked, (hx1, hz1, ht1) => House(hx1, hz1, ht1), (pm, px, pz, pyw, psz, pax, pcol) => Put(pm, px, pz, pyw, psz, pax, pcol), loot);

                // ---- fight spots: cover clusters around every town and village (sandbag lines, barriers, wrecks, barrels, crates),
                //      so a landing turns into a real fight instead of open ground ----
                int covers = 0;
                string[] coverAt = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(System.Linq.Enumerable.Select(KryvonLevant.TownPlan, t => t.region), System.Linq.Enumerable.Select(villages, v => v.name)));
                foreach (var name in coverAt)
                {
                    var c = KryvonLevant.RegionPos(name);
                    for (int g = 0; g < 7; g++)
                    {
                        float a2 = R(0, Mathf.PI * 2), rr = R(25, 140), gx = c.x + Mathf.Cos(a2) * rr, gz = c.y + Mathf.Sin(a2) * rr;
                        if (!Land(gx, gz, 4)) continue;
                        float yaw = R(0, 360); var fw = Quaternion.Euler(0, yaw, 0) * Vector3.forward; var rt = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                        int kind = rng.Next(4);
                        if (kind == 0) for (int i = -1; i <= 1; i++) Put("Props/p_sandbags", gx + rt.x * i * 3f, gz + rt.z * i * 3f, yaw, 3.2f, 'x');           // a sandbag line
                        else if (kind == 1) { Put("Props/p_wreck", gx, gz, yaw, 4.3f, 'x'); Put("Props/p_barrier", gx + fw.x * 5, gz + fw.z * 5, yaw + 90, 2.0f, 'x'); }
                        else if (kind == 2) { Put("Props/p_barrels", gx, gz, yaw, 1.6f, 'x'); Put("Props/p_sandbags", gx + fw.x * 3, gz + fw.z * 3, yaw, 3.2f, 'x'); Put("Props/p_barrier", gx - fw.x * 4, gz - fw.z * 4, yaw, 2.0f, 'x'); }
                        else { var ct = Put("Props/p_container", gx, gz, yaw, 6.1f, 'z'); Put("Props/p_sandbags", gx + rt.x * 5, gz + rt.z * 5, yaw + 90, 3.2f, 'x'); }
                        blocked.Add(new Rect(gx - 6, gz - 6, 12, 12)); covers++;
                    }
                }
                Physics.SyncTransforms();
                // ---- rock shelters in the hills: two boulders under a fallen slab = a short cave you can run through ----
                var rockM = PBRMat("M_RockShelter", "rock_face_03", new Color(0.8f, 0.72f, 0.62f), 3f, 1.4f);
                var shelters = new GameObject("RockShelters").transform; shelters.SetParent(world, false); int caves = 0;
                foreach (var hill in new[] { "Cedar Hills", "Wadi Araba", "Badia Desert", "Wadi Musa", "Dead Sea", "Kharbet Saba", "Tal Al-Rumman", "Joash & Alloun" })
                {
                    var c = KryvonLevant.RegionPos(hill);
                    for (int k = 0; k < 4; k++)
                    {
                        float x = c.x + R(-170, 170), z = c.y + R(-170, 170); if (!Land(x, z, 7)) continue;
                        var sh = new GameObject("Shelter").transform; sh.SetParent(shelters, false); sh.position = new Vector3(x, Y(x, z), z); sh.rotation = Quaternion.Euler(0, R(0, 360), 0);
                        P(PrimitiveType.Sphere, sh, new Vector3(-3.2f, 1.6f, 0), new Vector3(3.6f, 4.4f, 7.5f), rockM, new Vector3(0, 0, R(-8, 8)));
                        P(PrimitiveType.Sphere, sh, new Vector3(3.2f, 1.6f, 0), new Vector3(3.6f, 4.6f, 7.2f), rockM, new Vector3(0, 0, R(-8, 8)));
                        P(PrimitiveType.Cube, sh, new Vector3(0, 3.9f, 0), new Vector3(8.2f, 1.4f, 6.4f), rockM, new Vector3(R(-6, 6), R(-10, 10), R(-5, 5)));
                        P(PrimitiveType.Sphere, sh, new Vector3(R(-6, 6), 0.8f, R(5, 7)), new Vector3(2.4f, 1.8f, 2.2f), rockM);           // a loose boulder in front (cover)
                        lootTier = 2; for (int i = 0; i < 2; i++) DropLoot(loot, sh.TransformPoint(new Vector3(R(-1, 1), 0.15f, R(-2, 2))));
                        blocked.Add(new Rect(x - 7, z - 7, 14, 14)); caves++;
                    }
                }
                Physics.SyncTransforms();
                Debug.Log($"KRYVON: {covers} cover clusters, {caves} rock shelters");

                // ---- trees ----
                int trees = 0;
                void Tree(string m, float x, float z, float hgt)
                {
                    if (!Land(x, z, 1.2f)) return;
                    var t = Put(m, x, z, R(0, 360), hgt, 'y', false); if (t == null) return; t.transform.position -= Vector3.up * 0.15f;
                    var c = t.AddComponent<CapsuleCollider>(); c.radius = 0.35f; c.height = hgt * 0.5f; c.center = new Vector3(0, hgt * 0.25f, 0);
                    if (m.Contains("olive") && rng.NextDouble() < 0.75) Canopy(t, hgt);
                    trees++;
                }
                void Grove(string region, int groves, float radius, string model, float h0, float h1, int per)
                {
                    var c = KryvonLevant.RegionPos(region);
                    for (int g = 0; g < groves; g++)
                    {
                        float a = R(0, Mathf.PI * 2), rr = R(0, radius); float gx = c.x + Mathf.Cos(a) * rr, gz = c.y + Mathf.Sin(a) * rr;
                        for (int k = 0; k < per; k++) Tree(model, gx + (k % 4) * 7 + R(-1.5f, 1.5f), gz + (k / 4) * 7 + R(-1.5f, 1.5f), R(h0, h1));
                    }
                }
                Grove("Olive Plains", 14, 170, "Props/p_olive", 4.5f, 6.5f, 12);
                Grove("Joash & Alloun", 8, 120, "Props/p_olive", 4.5f, 6.5f, 9);
                Grove("Tal Al-Rumman", 6, 110, "Props/p_olive", 4.5f, 6.5f, 9);
                Grove("Cedar Hills", 26, 220, "Props/p_cypress", 9f, 15f, 10);
                Grove("Al Salt", 6, 160, "Props/p_cypress", 9f, 13f, 6);
                Grove("Beirut Coastal", 8, 180, "Props/p_palm", 8f, 12f, 4);
                Grove("Wadi Araba", 4, 160, "Props/p_palm", 7f, 10f, 4);
                Grove("Dead Sea", 3, 140, "Props/p_palm", 7f, 10f, 4);
                for (int k = 0; k < 120; k++) Tree(rng.NextDouble() < 0.5 ? "Props/p_cypress" : "Props/p_olive", R(-W / 2, W / 2), R(-L / 2, L / 2), R(5, 12));
                // street trees in every city: on the pavement at block corners (palms by the sea, cypress and olive inland),
                // only where nothing stands (no tree through a wall)
                int streetTrees = 0;
                for (int ti = 0; ti < Towns.Length && ti < KryvonLevant.TownPlan.Length; ti++)
                {
                    var tc = Towns[ti]; var (TC, TR, _, _, _) = Shape(ti); string reg = KryvonLevant.TownPlan[ti].region;
                    string model = reg.Contains("Beirut") ? "Props/p_palm" : rng.NextDouble() < 0.5 ? "Props/p_cypress" : "Props/p_olive";
                    for (int i = 0; i < TC; i++) for (int j = 0; j < TR; j++)
                    {
                        float bx = tc.x - TC * Cell / 2 + (i + 0.5f) * Cell, bz = tc.y - TR * Cell / 2 + (j + 0.5f) * Cell;
                        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                        {
                            if (rng.NextDouble() > 0.45) continue;
                            float x = bx + sx * (Cell / 2 - 4.6f), z = bz + sz * (Cell / 2 - 4.6f);
                            var gp = new Vector3(x, Y(x, z) + 0.3f, z);
                            if (Physics.CheckSphere(gp + Vector3.up * 2.2f, 1.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                            float hgt = model.Contains("palm") ? R(7, 10) : model.Contains("cypress") ? R(7, 10) : R(4, 5.5f);
                            var t = Put(model, x, z, R(0, 360), hgt, 'y', false); if (t == null) continue; t.transform.position = new Vector3(x, gp.y - 0.15f, z);
                            var c = t.AddComponent<CapsuleCollider>(); c.radius = 0.3f; c.height = hgt * 0.5f; c.center = new Vector3(0, hgt * 0.25f, 0);
                            streetTrees++;
                        }
                    }
                }
                Debug.Log($"KRYVON: {trees} trees on the Levant map, {streetTrees} street trees in the cities");

                PlaceFlareGuns();
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Physics.SyncTransforms();
            if (KryvonLevant.RoadPaths.Count > 0) RoadVehicles(world); else PlaceVehicles(world);
        }
    }
}

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        /// one generated prop scaled to size along an axis ('x' widest, 'y' height, 'z' length), with a box collider
        public static GameObject Prop(string rel, float size, char axis, Vector3 pos, float yaw, Transform parent)
        {
            var g = Spawn(rel, size, axis, parent); if (g == null) return null;
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); return g;
        }
    }
}

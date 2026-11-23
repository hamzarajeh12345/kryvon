// The forward operating base used behind the lobby (and later as the pre-match waiting area):
// apron with a helipad, control tower, hangar, comms masts with dishes, sandbags, crates, vehicles,
// and an old Levantine city climbing a hill behind, crowned by a citadel and minarets.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        public static void BuildMilitaryArea(Transform world, Terrain terrain, Vector2 centre)
        {
            rng = new System.Random(1946);
            float Y(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            var root = new GameObject("MilitaryCombatZone").transform;
            root.SetParent(world, false);
            root.position = new Vector3(centre.x, Y(centre.x, centre.y), centre.y);

            var baseRoot = BuildBase(root.position);
            baseRoot.SetParent(root, true);
            baseRoot.name = "MilitaryBaseCompound";

            var mid = 50000;
            var concrete = PBRMat("M_MilitaryConcrete", "concrete_floor_02", new Color(0.72f, 0.73f, 0.70f), 5f);
            var asphalt = PBRMat("M_MilitaryApron", "asphalt_02", new Color(0.35f, 0.37f, 0.38f), 7f);
            var yellow = PBRMat("M_MilitaryMark", "concrete_floor_02", new Color(0.95f, 0.68f, 0.12f), 1f);
            var plate = new Builder();
            plate.Box(asphalt, new Vector3(0, -0.18f, 0), new Vector3(210, 0.36f, 150));
            for (float x = -82; x <= 82; x += 20)
                plate.Box(yellow, new Vector3(x, 0.03f, -48), new Vector3(10, 0.03f, 0.5f), false);
            plate.Build("MilitaryApron", root, root.position, 0, ref mid);

            void PutVehicle(string model, float dx, float dz, float yaw)
            {
                var v = MakeVehicle(model, root.TransformPoint(new Vector3(dx, 0.2f, dz)), yaw, root);
                if (v) v.transform.position = new Vector3(v.transform.position.x, Y(v.transform.position.x, v.transform.position.z), v.transform.position.z);
            }

            PutVehicle("v_armored", -42, -22, 25);
            PutVehicle("v_armored", -34, -12, 25);
            PutVehicle("v_pickup", 38, -28, 210);
            PutVehicle("v_pickup", 46, -18, 210);
            PutVehicle("v_sedan", 55, 20, 160);
            PutVehicle("v_jeep", -52, 28, 70);
            PutVehicle("v_buggy", 18, 38, 130);
            PutVehicle("v_minibus", 4, 42, 180);

            // heavy combat tanks: two visible tracks, a hull, turret and cannon
            void PutTank(float dx, float dz, float yaw)
            {
                var tank = new GameObject("MilitaryTank");
                tank.transform.SetParent(root, false);
                tank.transform.position = root.TransformPoint(new Vector3(dx, 0.25f, dz));
                tank.transform.rotation = root.rotation * Quaternion.Euler(0, yaw, 0);
                var hullMat = PBRMat("M_TankOlive", "concrete_floor_02", new Color(0.34f, 0.38f, 0.27f), 2f);
                var trackMat = PBRMat("M_TankTracks", "concrete_floor_02", new Color(0.10f, 0.11f, 0.10f), 1f);
                var b = new Builder();
                b.Box(hullMat, new Vector3(0, 1.15f, 0), new Vector3(4.8f, 1.2f, 7.2f));
                b.Box(trackMat, new Vector3(-2.55f, 0.65f, 0), new Vector3(0.7f, 1.3f, 7.0f));
                b.Box(trackMat, new Vector3(2.55f, 0.65f, 0), new Vector3(0.7f, 1.3f, 7.0f));
                b.Box(hullMat, new Vector3(0, 2.25f, -0.2f), new Vector3(3.2f, 1.15f, 3.6f));
                b.Box(trackMat, new Vector3(0, 2.8f, -4.5f), new Vector3(0.45f, 0.45f, 5.2f));
                b.Build("TankGeometry", tank.transform, tank.transform.position, tank.transform.eulerAngles.y, ref mid);
            }
            PutTank(-18, 24, 35);
            PutTank(18, 24, 145);

            // two parked jet aircraft on opposite taxi pads
            foreach (var x in new[] { -78f, 78f })
            {
                var jet = Spawn("Vehicles/v_jet", 18f, 'z', root);
                if (jet)
                {
                    jet.transform.position = root.TransformPoint(new Vector3(x, 1.1f, 18));
                    jet.transform.rotation = root.rotation * Quaternion.Euler(0, x > 0 ? 20 : 200, 0);
                    Collide(jet);
                }
            }

            // compact housing row and separated houses around the apron
            int meshId = 50000;
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 24f - 25f, z = -62f;
                var b = new Builder();
                var spots = HouseShape(b, 10f, 8f, i % 2 == 0 ? 2 : 1, 1.2f);
                var house = b.Build("MilitaryHouse", root, new Vector3(root.position.x + x, Y(root.position.x + x, root.position.z + z) + 0.05f, root.position.z + z), i % 2 == 0 ? 180 : 0, ref meshId);
                foreach (var s in spots)
                {
                    var p = house.transform.TransformPoint(s);
                    DropLoot(root, p);
                }
            }

            // hangars and high radar/control structures
            foreach (var dx in new[] { -86f, -58f, 58f, 86f })
            {
                var hangar = Spawn("Props/p_hangar", 24f, 'x', root);
                if (hangar)
                {
                    hangar.transform.position = root.TransformPoint(new Vector3(dx, 0, -2));
                    hangar.transform.rotation = root.rotation * Quaternion.Euler(0, dx < 0 ? 180 : 0, 0);
                    Collide(hangar);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float dx = -20f + i * 20f;
                var radar = new Builder();
                radar.Box(concrete, new Vector3(dx, 4, 52), new Vector3(0.7f, 8, 0.7f));
                radar.Box(concrete, new Vector3(dx, 9, 52), new Vector3(12, 0.25f, 0.25f));
                radar.Build("RadarTower", root, root.position, 0, ref mid);
            }

            var fences = new GameObject("MilitaryFences").transform;
            fences.SetParent(root, false);
            for (float x = -100; x <= 100; x += 8)
            {
                Put("Props/p_barrier", root.position.x + x, root.position.z - 76, 0, 2f, 'x', false);
                Put("Props/p_sandbags", root.position.x + x, root.position.z + 68, 90, 3.2f, 'x', false);
            }
            blocked.Add(new Rect(centre.x - 112, centre.y - 82, 224, 164));
            Debug.Log("KRYVON: military combat zone ready — apron, hangars, radar towers, houses, aircraft and armoured vehicles");
        }

        static GameObject Put(string model, float x, float z, float yaw, float size, char axis, bool collide)
        {
            var g = Spawn(model, size, axis, null);
            if (!g) return null;
            g.transform.position = new Vector3(x, 0, z);
            g.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (collide) Collide(g);
            return g;
        }

        public static Transform BuildBase(Vector3 origin)
        {
            rng = new System.Random(77);
            var root = new GameObject("ForwardBase").transform; root.position = origin;
            int mid = 40000;
            var concrete = PBRMat("M_BaseConcrete", "concrete_floor_02", new Color(0.78f, 0.76f, 0.72f), 6f, 1.2f);
            var asphalt = PBRMat("M_BaseAsphalt", "asphalt_02", new Color(0.62f, 0.62f, 0.64f), 8f);
            var wall = PBRMat("M_BaseWall", "concrete_wall_008", new Color(0.86f, 0.82f, 0.74f), 3f);
            var stone = PBRMat("M_CityStone", "sandstone_blocks_08", new Color(0.98f, 0.9f, 0.76f), 3f);
            var steel = PBRMat("M_Steel", "concrete_floor_02", new Color(0.22f, 0.23f, 0.24f), 1f);
            var yellow = PBRMat("M_Paint", "concrete_floor_02", new Color(0.95f, 0.7f, 0.12f), 1f);
            var glass = PBRMat("M_TowerGlass", "concrete_floor_02", new Color(0.18f, 0.24f, 0.28f), 1f);
            glass.SetFloat("_Smoothness", 0.95f);

            // ---- apron + helipad ----
            var b = new Builder();
            b.Box(asphalt, new Vector3(0, -0.15f, 10), new Vector3(140, 0.3f, 110));
            b.Box(concrete, new Vector3(0, 0.01f, 2), new Vector3(30, 0.04f, 30));
            for (int i = 0; i < 40; i++) { float a = i / 40f * Mathf.PI * 2; b.Box(yellow, new Vector3(Mathf.Cos(a) * 11, 0.04f, 2 + Mathf.Sin(a) * 11), new Vector3(1.8f, 0.02f, 0.35f), false); }
            b.Box(yellow, new Vector3(-2.4f, 0.04f, 2), new Vector3(0.9f, 0.02f, 7), false); b.Box(yellow, new Vector3(2.4f, 0.04f, 2), new Vector3(0.9f, 0.02f, 7), false); b.Box(yellow, new Vector3(0, 0.04f, 2), new Vector3(4, 0.02f, 0.9f), false);
            for (int i = -3; i <= 3; i++) b.Box(yellow, new Vector3(i * 9f, 0.04f, -18), new Vector3(5, 0.02f, 0.3f), false);   // taxi line
            // perimeter wall with blast barriers
            b.Box(wall, new Vector3(-70, 1.6f, 10), new Vector3(0.6f, 3.2f, 110)); b.Box(wall, new Vector3(70, 1.6f, 10), new Vector3(0.6f, 3.2f, 110));
            b.Build("Apron", root, root.position, 0, ref mid);

            // ---- control tower (left) ----
            var tw = new Builder();
            tw.Box(wall, new Vector3(0, 7, 0), new Vector3(5, 14, 5));
            tw.Box(wall, new Vector3(0, 14.2f, 0), new Vector3(7.6f, 0.4f, 7.6f));
            foreach (var (x, z, w, d) in new[] { (0f, 3.6f, 7.6f, 0.15f), (0f, -3.6f, 7.6f, 0.15f), (3.6f, 0f, 0.15f, 7.6f), (-3.6f, 0f, 0.15f, 7.6f) }) tw.Box(glass, new Vector3(x, 15.9f, z), new Vector3(w, 3f, d));
            tw.Box(wall, new Vector3(0, 17.6f, 0), new Vector3(8.2f, 0.5f, 8.2f));
            foreach (var (x, z, w, d) in new[] { (0f, 4.1f, 8.2f, 0.06f), (0f, -4.1f, 8.2f, 0.06f), (4.1f, 0f, 0.06f, 8.2f), (-4.1f, 0f, 0.06f, 8.2f) }) tw.Box(steel, new Vector3(x, 18.4f, z), new Vector3(w, 0.06f, d), false);
            tw.Box(steel, new Vector3(0, 19.6f, 0), new Vector3(0.12f, 4f, 0.12f), false);
            var banner = PBRMat("M_Banner", "concrete_floor_02", new Color(0.12f, 0.13f, 0.14f), 1f);
            tw.Box(banner, new Vector3(0, 8.5f, -2.56f), new Vector3(3.6f, 8f, 0.05f), false);
            tw.Box(PBRMat("M_BannerGold", "concrete_floor_02", new Color(0.95f, 0.72f, 0.28f), 1f), new Vector3(0, 10.4f, -2.6f), new Vector3(2.4f, 0.3f, 0.03f), false);
            tw.Build("ControlTower", root, root.TransformPoint(new Vector3(-24, 0, 18)), 0, ref mid);

            // ---- comms masts with dishes ----
            void Mast(Vector3 p, float h)
            {
                var m = new Builder(); float s = 1.6f;
                for (int c = 0; c < 4; c++) { float cx = (c % 2 == 0 ? -1 : 1) * s / 2, cz = (c < 2 ? -1 : 1) * s / 2; m.Box(steel, new Vector3(cx, h / 2, cz), new Vector3(0.12f, h, 0.12f)); }
                for (float y = 1.5f; y < h; y += 2.5f) { m.Box(steel, new Vector3(0, y, -s / 2), new Vector3(s, 0.08f, 0.08f), false); m.Box(steel, new Vector3(0, y, s / 2), new Vector3(s, 0.08f, 0.08f), false); m.Box(steel, new Vector3(-s / 2, y, 0), new Vector3(0.08f, 0.08f, s), false); m.Box(steel, new Vector3(s / 2, y, 0), new Vector3(0.08f, 0.08f, s), false); }
                m.Box(steel, new Vector3(0, h + 2, 0), new Vector3(0.1f, 4, 0.1f), false);
                m.Build("Mast", root, root.TransformPoint(p), R(0, 90), ref mid);
            }
            Mast(new Vector3(-48, 0, 30), 24); Mast(new Vector3(-12, 0, 40), 20); Mast(new Vector3(46, 0, 34), 26);
            var dishMat = PBRMat("M_Dish", "concrete_floor_02", new Color(0.9f, 0.9f, 0.88f), 1f);
            foreach (var (p, yaw) in new[] { (new Vector3(30, 9.5f, 26), -30f), (new Vector3(38, 8f, 22), 20f) })
            {
                var d = new GameObject("Dish").transform; d.SetParent(root, false); d.localPosition = p; d.localRotation = Quaternion.Euler(-35, yaw, 0);
                Prim(PrimitiveType.Sphere, d, Vector3.zero, new Vector3(3.2f, 3.2f, 0.9f), dishMat);
                Prim(PrimitiveType.Cylinder, d, new Vector3(0, 0, -1.2f), new Vector3(0.12f, 1.2f, 0.12f), steel, new Vector3(90, 0, 0));
                Prim(PrimitiveType.Cylinder, root, new Vector3(p.x, p.y / 2, p.z), new Vector3(0.4f, p.y / 2, 0.4f), steel);
            }

            // ---- hangar (right) with a jeep inside, vehicles, crates, sandbags ----
            GameObject Put(string m, Vector3 lp, float yaw, float size, char axis)
            {
                var g = Spawn(m, size, axis, root); if (!g) return null;
                g.transform.position = root.TransformPoint(lp); g.transform.rotation = root.rotation * Quaternion.Euler(0, yaw, 0); return g;
            }
            Put("Props/p_hangar", new Vector3(30, 0, 30), 180, 30, 'x');
            Put("Vehicles/v_jeep", new Vector3(30, 0, 26), 160, 3.9f, 'z');
            Put("Vehicles/v_pickup", new Vector3(17, 0, 10), 200, 5f, 'z');
            Put("Vehicles/v_armored", new Vector3(-36, 0, 6), 70, 5.3f, 'z');
            Put("Vehicles/v_buggy", new Vector3(12, 0, -6), 230, 3.4f, 'z');
            foreach (var (p, yaw) in new[] { (new Vector3(-18, 0, -6), 90f), (new Vector3(-16, 0, -2.4f), 90f), (new Vector3(-30, 0, -12), 20f), (new Vector3(-12, 0, 14), 0f) })
                Put("Props/p_sandbags", p, yaw, 3.4f, 'x');
            foreach (var p in new[] { new Vector3(22, 0, -10), new Vector3(24.4f, 0, -10), new Vector3(23.2f, 1.25f, -10), new Vector3(-22, 0, 12), new Vector3(20, 0, -13) })
                Put("Loot/l_crate", p, R(-8, 8), 1.6f, 'x');
            Put("Props/p_container", new Vector3(40, 0, 6), 90, 6.1f, 'z'); Put("Props/p_container", new Vector3(-45, 0, 16), 0, 6.1f, 'z');
            for (int i = 0; i < 5; i++) Put("Props/p_barrels", new Vector3(26 + i * 1.6f, 0, 14), R(0, 360), 1.5f, 'x');
            for (int i = 0; i < 6; i++) Put("Props/p_cypress", new Vector3(-64 + i * 3, 0, 52), 0, R(9, 13), 'y');

            // ---- the old city on the hill behind ----
            var hill = new GameObject("CityHill").transform; hill.SetParent(root, false); hill.localPosition = new Vector3(0, 0, 105); hill.localRotation = Quaternion.Euler(0, 180, 0);
            var hb = new Builder();
            // six stepped terraces climbing away from the base; each step is 14 m deep and 7 m higher
            for (int k = 0; k < 6; k++) { float top = 8 + k * 7; hb.Box(stone, new Vector3(R(-6, 6), top / 2, -k * 14), new Vector3(240 - k * 30, top, 14.2f), true); }
            hb.Build("Terraces", hill, hill.position, hill.eulerAngles.y, ref mid);
            string[] cityKinds = { "h_oldstone", "h_villa", "h_shops", "h_apartment", "h_oldstone", "h_villa", "h_tower" };
            for (int k = 0; k < 6; k++)
            {
                float top = 8 + k * 7, width = 220 - k * 30; int n = 10 - k;
                for (int i = 0; i < n; i++)
                {
                    float x = -width / 2 + (i + 0.5f) * width / n + R(-3, 3);
                    var g = Spawn("Buildings/" + cityKinds[rng.Next(cityKinds.Length)], R(9, 14), 'y', hill); if (!g) continue;
                    g.transform.localPosition = new Vector3(x, top, -k * 14 + R(-2, 2)); g.transform.localRotation = Quaternion.Euler(0, R(-12, 12), 0);
                }
            }
            // citadel on the crest: curtain walls with towers, a mosque and minarets
            var cit = new Builder(); float cy = 43; var cz = -82f;
            cit.Box(stone, new Vector3(0, cy / 2, cz + 6), new Vector3(120, cy, 30)); cit.Box(stone, new Vector3(0, cy + 7, cz - 2), new Vector3(110, 14, 3)); cit.Box(stone, new Vector3(-55, cy + 7, cz + 8), new Vector3(3, 14, 20)); cit.Box(stone, new Vector3(55, cy + 7, cz + 8), new Vector3(3, 14, 20));
            for (int i = 0; i < 6; i++) cit.Box(stone, new Vector3(-55 + i * 22, cy + 9, cz - 2), new Vector3(8, 18, 8));
            for (int i = 0; i < 28; i++) cit.Box(stone, new Vector3(-54 + i * 4, cy + 14.6f, cz - 2), new Vector3(1.8f, 1.2f, 3.2f), false);   // crenellations
            cit.Box(stone, new Vector3(-14, cy + 12, cz + 10), new Vector3(30, 20, 16)); cit.Box(stone, new Vector3(18, cy + 10, cz + 12), new Vector3(22, 16, 14));
            cit.Build("Citadel", hill, hill.position, hill.eulerAngles.y, ref mid);
            var mosque = Spawn("Buildings/h_mosque", 22, 'y', hill); if (mosque) { mosque.transform.localPosition = new Vector3(40, cy, cz + 12); mosque.transform.localRotation = Quaternion.identity; }
            foreach (var x in new[] { -34f, 8f })
            {
                Prim(PrimitiveType.Cylinder, hill, new Vector3(x, cy + 18, cz + 4), new Vector3(2.2f, 18, 2.2f), stone);
                Prim(PrimitiveType.Cylinder, hill, new Vector3(x, cy + 30, cz + 4), new Vector3(3.2f, 0.4f, 3.2f), stone);
                Prim(PrimitiveType.Cylinder, hill, new Vector3(x, cy + 35, cz + 4), new Vector3(1.6f, 5, 1.6f), stone);
                Prim(PrimitiveType.Sphere, hill, new Vector3(x, cy + 40.6f, cz + 4), new Vector3(2.2f, 3.2f, 2.2f), stone);
            }
            return root;
        }
    }
}

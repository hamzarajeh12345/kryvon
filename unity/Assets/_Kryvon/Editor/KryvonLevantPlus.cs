// More of the Levant map, all of it ENTERABLE (no solid placeholder buildings):
//   · roadside shacks every ~170 m along the roads (some become checkpoints with sandbags and a barrier)
//   · warehouses (22 x 14 m hall, two big doorways, crate stacks inside for cover) instead of the solid hangars/garages
//   · points of interest in every empty 170 m cell (farm compound, checkpoint, warehouse, ruin), so no big dead zones
//   · vehicles parked along the roads on the shoulder, facing the road, at least 140 m apart (no car piles)
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        /// one-room shack, origin = centre of the floor, door in the -Z wall; returns loot spots
        static List<Vector3> ShackShape(Builder b, float w, float d)
        {
            var walls = new[] { ("M_ShackPlaster", "plastered_wall_02", new Color(0.92f, 0.86f, 0.74f)), ("M_ShackStone", "sandstone_blocks_08", new Color(1f, 0.95f, 0.86f)), ("M_ShackBlock", "concrete_wall_008", new Color(0.8f, 0.78f, 0.74f)) };
            var (mn, ph, tint) = walls[rng.Next(walls.Length)];
            var ext = PBRMat(mn, ph, tint, 2f);
            var floor = PBRMat("M_Concrete", "concrete_floor_02", new Color(0.85f, 0.84f, 0.82f), 3f);
            var roof = rng.NextDouble() < 0.5 ? PBRMat("M_ShackRoofTin", "concrete_floor_02", new Color(0.42f, 0.44f, 0.46f), 2f) : PBRMat("M_ShackRoofClay", "clay_roof_tiles", Color.white, 2f);
            float h = 2.9f, hx = w / 2, hz = d / 2;
            b.Box(floor, new Vector3(0, -0.3f, 0), new Vector3(w + 0.2f, 0.6f, d + 0.2f));
            WallLine(b, ext, 'x', -hz + Wall / 2, -hx, hx, 0, h, new List<(float, float, float, float)> { (rng.NextDouble() < 0.5 ? -w * 0.18f : w * 0.18f, 1.15f, 0f, 2.15f) });
            WallLine(b, ext, 'x', hz - Wall / 2, -hx, hx, 0, h, new List<(float, float, float, float)> { (0, 1.0f, 1.0f, 1.9f) });
            WallLine(b, ext, 'z', -hx + Wall / 2, -hz + Wall, hz - Wall, 0, h, new List<(float, float, float, float)> { (0, 0.9f, 1.0f, 1.8f) });
            WallLine(b, ext, 'z', hx - Wall / 2, -hz + Wall, hz - Wall, 0, h, new List<(float, float, float, float)>());
            b.Box(roof, new Vector3(0, h + 0.09f, 0), new Vector3(w + 0.6f, 0.18f, d + 0.6f));
            return new List<Vector3> { new(0, 0.1f, 0.4f) };
        }

        /// warehouse hall: big doorways front and back, small doors on the sides, high windows, crate stacks for cover
        static List<Vector3> WarehouseShape(Builder b, float w, float d)
        {
            var ext = PBRMat("M_WarehouseWall", "concrete_wall_008", new Color(0.78f, 0.77f, 0.74f), 3f);
            var floor = PBRMat("M_Concrete", "concrete_floor_02", new Color(0.85f, 0.84f, 0.82f), 3f);
            var roof = PBRMat("M_ShackRoofTin", "concrete_floor_02", new Color(0.42f, 0.44f, 0.46f), 2f);
            var crate = PBRMat("M_Crate", "laminate_floor_02", new Color(0.7f, 0.55f, 0.35f), 1f);
            float h = 6.2f, hx = w / 2, hz = d / 2;
            b.Box(floor, new Vector3(0, -0.3f, 0), new Vector3(w + 0.2f, 0.6f, d + 0.2f));
            var high = new List<(float, float, float, float)>(); for (float x = -hx + 3; x < hx - 2; x += 4) if (Mathf.Abs(x) > 3) high.Add((x, 1.6f, 4.2f, 5.4f));
            var front = new List<(float, float, float, float)>(high) { (0, 4.2f, 0f, 4.4f) };
            WallLine(b, ext, 'x', -hz + Wall / 2, -hx, hx, 0, h, front);
            WallLine(b, ext, 'x', hz - Wall / 2, -hx, hx, 0, h, new List<(float, float, float, float)>(front));
            WallLine(b, ext, 'z', -hx + Wall / 2, -hz + Wall, hz - Wall, 0, h, new List<(float, float, float, float)> { (0, 1.2f, 0f, 2.25f) });
            WallLine(b, ext, 'z', hx - Wall / 2, -hz + Wall, hz - Wall, 0, h, new List<(float, float, float, float)> { (-2.5f, 1.2f, 0f, 2.25f), (3f, 1.4f, 3.8f, 5f) });
            b.Box(roof, new Vector3(0, h + 0.1f, 0), new Vector3(w + 0.8f, 0.2f, d + 0.8f));
            var spots = new List<Vector3>();
            // crate stacks in two rows, gaps to run through
            foreach (var x in new[] { -hx + 3.2f, -3.5f, 3.5f, hx - 3.2f })
                foreach (var z in new[] { -2.6f, 2.8f })
                {
                    if (rng.NextDouble() < 0.25) { spots.Add(new Vector3(x, 0.1f, z)); continue; }
                    int n = 1 + rng.Next(3);
                    for (int k = 0; k < n; k++) b.Box(crate, new Vector3(x + R(-0.2f, 0.2f), 0.7f + k * 1.4f, z + R(-0.2f, 0.2f)), new Vector3(1.4f, 1.4f, 1.4f));
                    spots.Add(new Vector3(x + 1.3f, 0.1f, z));
                }
            return spots;
        }

        /// the parts of DressLevant that need the map's roads; called from inside DressLevant
        static void DressRoadsAndGaps(Transform world, System.Func<float, float, float, bool> land, System.Func<float, float, float> Y,
                                      List<Rect> blocked, System.Action<float, float, int> house, System.Func<string, float, float, float, float, char, bool, GameObject> put, Transform lootRoot)
        {
            var root = new GameObject("RoadsideAndGaps").transform; root.SetParent(world, false);
            int meshId = 40000, shacks = 0, whs = 0, pois = 0;
            bool Free(float x, float z, float r) { foreach (var b in blocked) if (x > b.xMin - r && x < b.xMax + r && z > b.yMin - r && z < b.yMax + r) return false; return true; }
            GameObject Shack(float x, float z, float yaw, int tier)
            {
                float w = R(3.6f, 5.2f), d = R(3.4f, 4.6f); if (!land(x, z, 4)) return null;
                var b = new Builder(); lootTier = tier; var spots = ShackShape(b, w, d);
                float hi = float.MinValue; for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) hi = Mathf.Max(hi, Y(x + i * w / 2, z + j * d / 2));
                var go = b.Build("Shack", root, new Vector3(x, hi + 0.05f, z), yaw, ref meshId);
                foreach (var s in spots) DropLoot(lootRoot, go.transform.TransformPoint(s));
                blocked.Add(new Rect(x - 4, z - 4, 8, 8)); Physics.SyncTransforms(); shacks++; return go;
            }
            GameObject Warehouse(float x, float z, float yaw, int tier)
            {
                float w = R(18f, 24f), d = R(12f, 15f); if (!land(x, z, 14)) return null;
                var b = new Builder(); lootTier = tier; var spots = WarehouseShape(b, w, d);
                float hi = float.MinValue; for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) hi = Mathf.Max(hi, Y(x + i * w / 2, z + j * d / 2));
                var go = b.Build("Warehouse", root, new Vector3(x, hi + 0.05f, z), yaw, ref meshId);
                foreach (var s in spots) DropLoot(lootRoot, go.transform.TransformPoint(s));
                blocked.Add(new Rect(x - 14, z - 14, 28, 28)); Physics.SyncTransforms(); whs++; return go;
            }

            // ---- warehouses at the port and in Al-Orak (replacing the old solid hangars) ----
            foreach (var (region, n) in new[] { ("Beirut Port", 5), ("Al-Orak", 4) })
            {
                var c = KryvonLevant.RegionPos(region);
                for (int k = 0, tries = 0; k < n && tries < 60; tries++) { float x = c.x + R(-150, 150), z = c.y + R(-130, 130); if (Free(x, z, 14) && Warehouse(x, z, rng.Next(4) * 90f, 2)) k++; }
            }

            // ---- roadside shacks, every ~170 m, alternating sides, facing the road ----
            foreach (var (pts, width, type) in KryvonLevant.RoadPaths)
            {
                float next = R(60, 160), acc = 0; int side = rng.Next(2) * 2 - 1;
                for (int i = 1; i < pts.Length; i++)
                {
                    acc += Vector3.Distance(pts[i - 1], pts[i]); if (acc < next) continue;
                    acc = 0; next = R(140, 210); side = -side;
                    var dir = (pts[i] - pts[i - 1]); dir.y = 0; dir.Normalize(); var nrm = new Vector3(dir.z, 0, -dir.x) * side;
                    var p = pts[i] + nrm * (width / 2 + 6.5f); if (!Free(p.x, p.z, 4)) continue;
                    var fwd = nrm;                                                // the door (local -Z) faces the road
                    var go = Shack(p.x, p.z, Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, 1);
                    if (go && rng.NextDouble() < 0.3)
                    {   // a checkpoint: sandbags on the road side, a barrier across the shoulder
                        var q = p - nrm * 3.5f + dir * 3f; put("Props/p_sandbags", q.x, q.z, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 3.2f, 'x', true);
                        var r2 = p - nrm * 3.5f - dir * 4f; put("Props/p_barrier", r2.x, r2.z, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90, 2.0f, 'x', true);
                    }
                }
            }

            // ---- fill the empty cells: something to fight over every ~170 m ----
            var (W, L, _) = KryvonLevant.Size(); const float cell = 170f;
            for (float cx = -W / 2 + cell / 2; cx < W / 2; cx += cell)
                for (float cz = -L / 2 + cell / 2; cz < L / 2; cz += cell)
                {
                    float x = cx + R(-50, 50), z = cz + R(-50, 50);
                    if (!land(x, z, 12) || !Free(x, z, 70)) continue;
                    double roll = rng.NextDouble();
                    if (roll < 0.4)
                    {   // farm compound: a one- or two-storey house, a shack, a low wall line
                        house(x, z, 1); Shack(x + R(12, 16), z + R(-6, 6), R(0, 360), 1);
                        put("Props/p_sandbags", x - 10, z + 8, R(0, 360), 3.2f, 'x', true);
                    }
                    else if (roll < 0.62)
                    {   // checkpoint: two shacks facing each other across a sandbag line, a wreck
                        float yaw = R(0, 360); var f = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        Shack(x + f.x * 7, z + f.z * 7, yaw + 180, 2); Shack(x - f.x * 7, z - f.z * 7, yaw, 2);
                        put("Props/p_sandbags", x, z, yaw + 90, 3.2f, 'x', true); put("Props/p_wreck", x + f.z * 9, z - f.x * 9, yaw, 4.3f, 'x', true);
                    }
                    else if (roll < 0.82) { Warehouse(x, z, rng.Next(4) * 90f, 2); put("Props/p_barrels", x + 13, z + 3, R(0, 360), 1.6f, 'x', true); }
                    else
                    {   // hamlet: three small houses close together
                        for (int k = 0; k < 3; k++) house(x + R(-22, 22), z + R(-22, 22), 1);
                    }
                    blocked.Add(new Rect(x - 30, z - 30, 60, 60)); pois++;
                }
            Debug.Log($"KRYVON: {shacks} shacks, {whs} warehouses, {pois} points of interest in empty cells");
        }

        /// vehicles parked on road shoulders, aligned with the road, ≥140 m apart; one or two at every town edge
        static void RoadVehicles(Transform parent)
        {
            Physics.SyncTransforms();
            var root = new GameObject("AreaVehicles").transform; root.SetParent(parent, false);
            var placed = new List<Vector3>(); int made = 0;
            bool Try(string m, Vector3 p, float yaw)
            {
                foreach (var q in placed) if ((q - p).sqrMagnitude < 140f * 140f) return false;
                if (!Physics.Raycast(new Vector3(p.x, 600, p.z), Vector3.down, out var hit, 1200, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (Vector3.Angle(hit.normal, Vector3.up) > 12 || hit.point.y < 9f) return false;
                var st = VehicleStats[m];
                if (Physics.CheckBox(hit.point + Vector3.up * 1.15f, new Vector3(st.wid / 2 + 0.4f, 0.8f, st.len / 2 + 0.4f), Quaternion.Euler(0, yaw, 0), ~0, QueryTriggerInteraction.Ignore)) return false;
                if (!MakeVehicle(m, hit.point, yaw, root)) return false;
                placed.Add(p); made++; Physics.SyncTransforms(); return true;
            }
            string[] highway = { "v_sedan", "v_pickup", "v_minibus", "v_sedan", "v_pickup", "v_armored" }, track = { "v_pickup", "v_jeep", "v_buggy", "v_moto", "v_jeep", "v_moto" };
            foreach (var (pts, width, type) in KryvonLevant.RoadPaths)
            {
                float acc = R(0, 120), next = R(180, 260);
                for (int i = 1; i < pts.Length; i++)
                {
                    acc += Vector3.Distance(pts[i - 1], pts[i]); if (acc < next) continue;
                    var dir = pts[i] - pts[i - 1]; dir.y = 0; dir.Normalize(); var nrm = new Vector3(dir.z, 0, -dir.x) * (rng.Next(2) * 2 - 1);
                    var p = pts[i] + nrm * (width / 2 + 2.4f); var set = type == "highway" ? highway : track;
                    if (Try(set[rng.Next(set.Length)], p, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (rng.NextDouble() < 0.5 ? 0 : 180))) { acc = 0; next = R(180, 260); }
                }
            }
            // town edges: a getaway car on two sides of every city
            foreach (var t in Towns)
                foreach (var dv in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
                {
                    var p = new Vector3(t.x + dv.x * 95, 0, t.y + dv.y * 75);
                    Try(rng.NextDouble() < 0.5 ? "v_pickup" : "v_jeep", p, Mathf.Atan2(dv.x, dv.y) * Mathf.Rad2Deg);
                }
            Debug.Log($"KRYVON: {made} vehicles parked along roads and at town edges (≥140 m apart)");
        }
    }
}

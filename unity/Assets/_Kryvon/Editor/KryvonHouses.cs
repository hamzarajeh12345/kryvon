// Enterable multi-storey houses: rooms, interior doors, windows, stairs, flat Levantine roofs, loot in every room.
// Geometry is merged per house per material (few draw calls) with world-scaled UVs; every wall box gets a collider.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        const float FloorH = 3.2f, Wall = 0.22f, Slab = 0.25f;

        // ---------- materials ----------
        static readonly Dictionary<string, Material> mats = new();
        static readonly Dictionary<Material, float> tiles = new();
        static Material PBRMat(string name, string ph, Color tint, float tile, float bump = 1f)
        {
            if (mats.TryGetValue(name, out var m) && m) { tiles[m] = tile; return m; }
            var path = $"{Root}/Materials/{name}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var d = PH(ph + "_diff.jpg", false); var n = PH(ph + "_nor.jpg", true);
            if (d) m.SetTexture("_BaseMap", d);
            if (n) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", 0.12f);
            tiles[m] = tile;                // world metres per texture repeat, read by the mesh builder
            EditorUtility.SetDirty(m); mats[name] = m; return m;
        }

        // ---------- merged box builder ----------
        class Builder
        {
            public readonly Dictionary<Material, (List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)> parts = new();
            public readonly List<(Vector3 c, Vector3 s)> colliders = new();
            public readonly List<(Vector3 c, char axis, float w, float h)> doors = new(), windows = new();
            public void Box(Material m, Vector3 c, Vector3 s, bool collide = true)
            {
                if (s.x <= 0.01f || s.y <= 0.01f || s.z <= 0.01f) return;
                if (!parts.TryGetValue(m, out var p)) parts[m] = p = (new(), new(), new(), new());
                float tile = tiles.TryGetValue(m, out var tl) ? tl : 2f;
                Vector3 h = s * 0.5f;
                void Face(Vector3 n, Vector3 u, Vector3 v, float su, float sv)
                {
                    int i0 = p.v.Count; Vector3 o = c + Vector3.Scale(n, h);
                    Vector3 du = u * su * 0.5f, dv = v * sv * 0.5f;
                    p.v.Add(o - du - dv); p.v.Add(o + du - dv); p.v.Add(o + du + dv); p.v.Add(o - du + dv);
                    for (int k = 0; k < 4; k++) p.n.Add(n);
                    // world-anchored UVs so neighbouring boxes line up
                    Vector2 B(Vector3 q) => new Vector2(Vector3.Dot(q, u), Vector3.Dot(q, v)) / tile;
                    p.uv.Add(B(o - du - dv)); p.uv.Add(B(o + du - dv)); p.uv.Add(B(o + du + dv)); p.uv.Add(B(o - du + dv));
                    // front face must wind so that cross(b-a, c-a) points along the outward normal
                    if (Vector3.Dot(Vector3.Cross(u, v), n) > 0) p.t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                    else p.t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
                }
                Face(Vector3.right, Vector3.forward * -1, Vector3.up, s.z, s.y); Face(Vector3.left, Vector3.forward, Vector3.up, s.z, s.y);
                Face(Vector3.forward, Vector3.right, Vector3.up, s.x, s.y); Face(Vector3.back, Vector3.left, Vector3.up, s.x, s.y);
                Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z); Face(Vector3.down, Vector3.right, Vector3.back, s.x, s.z);
                if (collide) colliders.Add((c, s));
            }
            public GameObject Build(string name, Transform parent, Vector3 pos, float yaw, ref int meshId)
            {
                var go = new GameObject(name); go.transform.SetParent(parent, false);
                go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                foreach (var kv in parts)
                {
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(kv.Value.v); mesh.SetNormals(kv.Value.n); mesh.SetUVs(0, kv.Value.uv); mesh.SetTriangles(kv.Value.t, 0);
                    mesh.RecalculateTangents(); mesh.RecalculateBounds();
                    var mp = $"{Root}/Settings/House_{meshId++}.asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
                    var part = new GameObject(kv.Key.name); part.transform.SetParent(go.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
                }
                var cols = new GameObject("Colliders"); cols.transform.SetParent(go.transform, false);
                foreach (var (c, s) in colliders) { var bc = cols.AddComponent<BoxCollider>(); bc.center = c; bc.size = s; }
                Specials(this, go.transform);
                return go;
            }
        }

        // wall along local X (axis 'x') or local Z, from a to b at fixed coordinate, with openings (centre, width, bottom, top)
        static void WallLine(Builder b, Material m, char axis, float fixedC, float a, float c, float y0, float y1, List<(float at, float w, float lo, float hi)> holes)
        {
            holes.Sort((p, q) => p.at.CompareTo(q.at));
            float cur = a;
            void Seg(float s0, float s1, float lo, float hi)
            {
                if (s1 - s0 < 0.02f || hi - lo < 0.02f) return;
                float mid = (s0 + s1) / 2, len = s1 - s0;
                if (axis == 'x') b.Box(m, new Vector3(mid, (lo + hi) / 2, fixedC), new Vector3(len, hi - lo, Wall));
                else b.Box(m, new Vector3(fixedC, (lo + hi) / 2, mid), new Vector3(Wall, hi - lo, len));
            }
            foreach (var h in holes)
            {
                float s0 = h.at - h.w / 2, s1 = h.at + h.w / 2;
                var ctr = axis == 'x' ? new Vector3(h.at, y0 + (h.lo + h.hi) / 2, fixedC) : new Vector3(fixedC, y0 + (h.lo + h.hi) / 2, h.at);
                if (h.lo <= 0.01f) b.doors.Add((ctr, axis, h.w, h.hi - h.lo)); else b.windows.Add((ctr, axis, h.w, h.hi - h.lo));
                Seg(cur, s0, y0, y1);
                Seg(s0, s1, y0, y0 + h.lo);          // under a window
                Seg(s0, s1, y0 + h.hi, y1);          // lintel
                cur = s1;
            }
            Seg(cur, c, y0, y1);
        }

        static readonly string[] Exteriors = { "beige_wall_001", "plastered_wall_02", "painted_plaster_wall", "concrete_wall_008" };
        static readonly Color[] Tints = { new(1f, 0.97f, 0.9f), new(0.96f, 0.93f, 0.86f), new(1f, 0.95f, 0.85f), new(0.92f, 0.9f, 0.86f), new(0.98f, 0.92f, 0.8f) };

        /// builds one house in local space (origin = centre of the ground floor), returns loot spots in local space
        static List<Vector3> HouseShape(Builder b, float w, float d, int floors, float foundation)
        {
            var ext = PBRMat("M_HouseExt" + rng.Next(Exteriors.Length), Exteriors[rng.Next(Exteriors.Length)], Tints[rng.Next(Tints.Length)], 2.5f);
            var inner = PBRMat("M_HouseInt", "plastered_wall_02", new Color(0.97f, 0.96f, 0.93f), 2.5f);
            var floorM = rng.NextDouble() < 0.6 ? PBRMat("M_FloorTiles", "floor_tiles_06", Color.white, 1.6f) : PBRMat("M_FloorStone", "stone_tiles_02", new Color(0.95f, 0.93f, 0.9f), 2f);
            var concrete = PBRMat("M_Concrete", "concrete_floor_02", new Color(0.85f, 0.84f, 0.82f), 3f);
            var stairsM = PBRMat("M_Stairs", "concrete_floor_02", new Color(0.78f, 0.77f, 0.75f), 1.5f);
            var spots = new List<Vector3>();
            float hx = w / 2, hz = d / 2;
            // foundation down to the terrain
            b.Box(concrete, new Vector3(0, -foundation / 2 - 0.05f, 0), new Vector3(w + 0.3f, foundation + 0.1f, d + 0.3f));
            // stairwell in the back-right corner
            float swW = 1.3f, swD = 4.2f, swX = hx - Wall - swW / 2, swZ = hz - Wall - swD / 2;
            float midX = (rng.NextDouble() < 0.5 ? -1 : 1) * w * 0.12f;     // interior wall positions
            float midZ = -d * 0.08f;
            for (int f = 0; f < floors; f++)
            {
                float y0 = f * FloorH, y1 = y0 + FloorH;
                // floor slab (with a stair hole on upper floors)
                if (f == 0) b.Box(floorM, new Vector3(0, -Slab / 2, 0), new Vector3(w, Slab, d));
                else
                {
                    float holeX0 = swX - swW / 2, holeZ0 = swZ - swD / 2;
                    b.Box(floorM, new Vector3((-hx + holeX0) / 2, y0 - Slab / 2, 0), new Vector3(holeX0 + hx, Slab, d));
                    b.Box(floorM, new Vector3((holeX0 + hx) / 2, y0 - Slab / 2, (-hz + holeZ0) / 2), new Vector3(hx - holeX0, Slab, holeZ0 + hz));
                }
                // exterior walls: windows every ~3 m, the front door on the ground floor
                foreach (var (axis, fc, len) in new[] { ('x', -hz + Wall / 2, w), ('x', hz - Wall / 2, w), ('z', -hx + Wall / 2, d - Wall * 2), ('z', hx - Wall / 2, d - Wall * 2) })
                {
                    var holes = new List<(float, float, float, float)>();
                    int nWin = Mathf.Max(1, Mathf.FloorToInt(len / 3.2f));
                    for (int k = 0; k < nWin; k++)
                    {
                        float at = -len / 2 + (k + 0.5f) * len / nWin;
                        bool door = f == 0 && axis == 'x' && fc < 0 && k == nWin / 2;
                        if (door) holes.Add((at, 1.2f, 0f, 2.25f)); else holes.Add((at, 1.15f, 0.95f, 2.25f));
                    }
                    WallLine(b, ext, axis, fc, -len / 2, len / 2, y0, y1, holes);
                }
                // window sills, balconies and AC units on the street side
                var trim = PBRMat("M_Trim", "concrete_floor_02", new Color(0.93f, 0.9f, 0.84f), 1.5f);
                var rail = PBRMat("M_Rail", "concrete_floor_02", new Color(0.18f, 0.18f, 0.19f), 1f);
                int nw = Mathf.Max(1, Mathf.FloorToInt(w / 3.2f));
                for (int k = 0; k < nw; k++)
                {
                    float at = -w / 2 + (k + 0.5f) * w / nw;
                    if (!(f == 0 && k == nw / 2)) b.Box(trim, new Vector3(at, y0 + 0.88f, -hz - 0.08f), new Vector3(1.45f, 0.08f, 0.28f), false);
                    if (f > 0 && k % 2 == 1 && rng.NextDouble() < 0.7)
                    {
                        b.Box(trim, new Vector3(at, y0 - 0.1f, -hz - 0.65f), new Vector3(2.6f, 0.18f, 1.3f));                     // balcony slab
                        b.Box(rail, new Vector3(at, y0 + 0.5f, -hz - 1.27f), new Vector3(2.6f, 1.0f, 0.06f));                    // railing
                        b.Box(rail, new Vector3(at - 1.27f, y0 + 0.5f, -hz - 0.65f), new Vector3(0.06f, 1.0f, 1.3f));
                        b.Box(rail, new Vector3(at + 1.27f, y0 + 0.5f, -hz - 0.65f), new Vector3(0.06f, 1.0f, 1.3f));
                    }
                }
                if (f > 0 && rng.NextDouble() < 0.6) b.Box(trim, new Vector3(hx + 0.3f, y0 + 1.9f, R(-hz + 1.5f, hz - 1.5f)), new Vector3(0.5f, 0.6f, 0.85f), false);  // AC unit
                if (f > 0) b.Box(trim, new Vector3(0, y0 - 0.05f, -hz - 0.1f), new Vector3(w + 0.2f, 0.16f, 0.22f), false);                          // floor band
                // interior walls with door gaps -> 3-4 rooms per floor
                WallLine(b, inner, 'z', midX, -hz + Wall, hz - Wall, y0, y1, new() { (midZ - d * 0.22f, 1.0f, 0f, 2.15f), (midZ + d * 0.25f, 1.0f, 0f, 2.15f) });
                WallLine(b, inner, 'x', midZ, -hx + Wall, midX - Wall / 2, y0, y1, new() { ((-hx + midX) / 2, 1.0f, 0f, 2.15f) });
                if (w > 11) WallLine(b, inner, 'x', midZ, midX + Wall / 2, swX - swW / 2 - 0.1f, y0, y1, new() { ((midX + swX - swW / 2) / 2, 1.0f, 0f, 2.15f) });
                // stairs up to the next floor (and to the roof from the top floor)
                int steps = 16; float rise = FloorH / steps, run = (swD - 0.4f) / steps;
                for (int s = 0; s < steps; s++)
                    b.Box(stairsM, new Vector3(swX, y0 + rise * (s + 1) / 2f, swZ - swD / 2 + 0.2f + run * (s + 0.5f)), new Vector3(swW - 0.05f, rise * (s + 1), run));   // solid steps
                // loot: one or two spots per room
                foreach (var (rx, rz) in new[] { ((-hx + midX) / 2, (-hz + midZ) / 2), ((-hx + midX) / 2, (midZ + hz) / 2), ((midX + hx) / 2 - 0.6f, (-hz + midZ) / 2), ((midX + swX - swW / 2) / 2, (midZ + hz) / 2) })
                {
                    int n = rng.NextDouble() < 0.65 ? 1 : 2;
                    for (int k = 0; k < n; k++) spots.Add(new Vector3(rx + R(-1f, 1f), y0 + 0.02f, rz + R(-0.8f, 0.8f)));
                }
            }
            // roof slab with parapet, water tanks, solar heater
            float top = floors * FloorH;
            float hX0 = swX - swW / 2, hZ0 = swZ - swD / 2;
            b.Box(concrete, new Vector3((-hx + hX0) / 2, top - Slab / 2, 0), new Vector3(hX0 + hx, Slab, d));
            b.Box(concrete, new Vector3((hX0 + hx) / 2, top - Slab / 2, (-hz + hZ0) / 2), new Vector3(hx - hX0, Slab, hZ0 + hz));
            // stair hut over the stairwell
            b.Box(ext, new Vector3(swX, top + 1.2f, swZ + swD / 2 - Wall / 2), new Vector3(swW + Wall * 2, 2.4f, Wall));
            b.Box(ext, new Vector3(swX - swW / 2 - Wall / 2, top + 1.2f, swZ), new Vector3(Wall, 2.4f, swD));
            b.Box(concrete, new Vector3(swX, top + 2.45f, swZ), new Vector3(swW + Wall * 2, 0.15f, swD));
            foreach (var (axis, fc, len) in new[] { ('x', -hz + 0.1f, w), ('x', hz - 0.1f, w), ('z', -hx + 0.1f, d), ('z', hx - 0.1f, d) })
                if (axis == 'x') b.Box(ext, new Vector3(0, top + 0.5f, fc), new Vector3(len, 1.0f, 0.2f)); else b.Box(ext, new Vector3(fc, top + 0.5f, 0), new Vector3(0.2f, 1.0f, len));
            var black = PBRMat("M_TankBlack", "concrete_floor_02", new Color(0.12f, 0.12f, 0.13f), 1f);
            for (int k = 0; k < 1 + rng.Next(2); k++) b.Box(black, new Vector3(-hx + 1.5f + k * 1.4f, top + 0.65f, -hz + 1.5f), new Vector3(1.1f, 1.1f, 1.1f), false);
            var steel = PBRMat("M_Solar", "concrete_floor_02", new Color(0.25f, 0.32f, 0.42f), 1f);
            b.Box(steel, new Vector3(-hx + 2f, top + 0.6f, hz - 2f), new Vector3(2f, 0.08f, 1.2f), false);
            // roof loot too
            spots.Add(new Vector3(-hx + 2.5f, top + 0.02f, 0));
            return spots;
        }

        // ---------- loot ----------
        public static readonly List<WeaponData> lootGuns = new();
        static WeaponData Gun(string file, string id, string en, string ar, WeaponClass cls, AmmoType ammo, float dmg, float rpm, int mag, float range, FireMode[] modes, ScopeType scope, string sound)
        {
            var path = $"{Root}/Data/{file}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null) { Directory.CreateDirectory(Root + "/Data"); w = ScriptableObject.CreateInstance<WeaponData>(); AssetDatabase.CreateAsset(w, path); }
            w.weaponId = id; w.displayName = en; w.arabicName = ar; w.weaponClass = cls; w.ammoType = ammo; w.damage = dmg; w.roundsPerMinute = rpm; w.magazineSize = mag; w.range = range; w.fireModes = modes; w.defaultScope = scope;
            w.shotSound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{sound}.wav");
            w.suppressedShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/gun_suppressed.wav");
            w.reloadSound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/reload.wav");
            w.drySound = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/ui_dry_fire.wav");
            EditorUtility.SetDirty(w); return w;
        }
        public static void EnsureLootGuns()
        {
            lootGuns.Clear();
            var auto = new[] { FireMode.Auto, FireMode.Single }; var single = new[] { FireMode.Single };
            lootGuns.Add(Gun("AlSaqr", "saqr", "Al-Saqr", "الصقر", WeaponClass.AssaultRifle, AmmoType.A556, 40, 660, 30, 350, auto, ScopeType.RedDot, "gun_ar"));
            lootGuns.Add(Gun("Zalzal", "zalzal", "Zalzal SMG", "زلزال", WeaponClass.SMG, AmmoType.A9mm, 25, 900, 33, 150, auto, ScopeType.None, "gun_smg"));
            lootGuns.Add(Gun("Raad", "raad", "Ra'd 12", "رعد", WeaponClass.Shotgun, AmmoType.Gauge12, 22, 70, 5, 40, single, ScopeType.None, "gun_shotgun"));
            lootGuns.Add(Gun("Saif", "saif", "Saif P9", "سيف", WeaponClass.Pistol, AmmoType.A9mm, 30, 400, 15, 100, single, ScopeType.None, "gun_pistol"));
            lootGuns.Add(Gun("AlQannas", "qannas", "Al-Qannas", "القنّاص", WeaponClass.Sniper, AmmoType.A300, 78, 48, 5, 750, single, ScopeType.X4, "gun_sniper"));
            lootGuns.Add(Gun("Jabal", "jabal", "Jabal LMG", "جبل", WeaponClass.LMG, AmmoType.A762, 42, 600, 75, 400, auto, ScopeType.None, "gun_lmg"));
        }
        static readonly Dictionary<WeaponClass, (string model, float len)> GunModel = new()
        {
            { WeaponClass.AssaultRifle, ("w_ar", 0.95f) }, { WeaponClass.SMG, ("w_smg", 0.65f) }, { WeaponClass.Shotgun, ("w_shotgun", 1.0f) },
            { WeaponClass.Pistol, ("w_pistol", 0.24f) }, { WeaponClass.Sniper, ("w_sniper", 1.25f) }, { WeaponClass.LMG, ("w_lmg", 1.1f) },
        };
        static Material glowMat;
        static void DropLoot(Transform parent, Vector3 p)
        {
            if (lootGuns.Count == 0) EnsureLootGuns();
            double r = rng.NextDouble();
            GameObject g; KryvonLoot l; Color glow;
            if (r < 0.32)
            {
                var wd = lootGuns[rng.Next(lootGuns.Count)];
                var (model, len) = GunModel[wd.weaponClass];
                g = Spawn("Weapons/" + model, len, 'z', parent) ?? new GameObject("Gun");
                g.transform.position = p + Vector3.up * 0.04f; g.transform.rotation = Quaternion.Euler(0, R(0, 360), 90);
                l = g.AddComponent<KryvonLoot>(); l.kind = LootKind.Weapon; l.weapon = wd;
                glow = wd.weaponClass == WeaponClass.Sniper || wd.weaponClass == WeaponClass.LMG ? new Color(0.7f, 0.4f, 1f) : new Color(0.3f, 0.65f, 1f);
            }
            else
            {
                LootKind k = r < 0.58 ? LootKind.Ammo : r < 0.72 ? LootKind.Bandage : r < 0.8 ? LootKind.Medkit : r < 0.9 ? LootKind.Vest : LootKind.Helmet;
                var (col, size) = k switch
                {
                    LootKind.Ammo => (new Color(0.3f, 0.36f, 0.22f), new Vector3(0.36f, 0.2f, 0.22f)),
                    LootKind.Bandage => (new Color(0.95f, 0.95f, 0.92f), new Vector3(0.18f, 0.08f, 0.12f)),
                    LootKind.Medkit => (new Color(0.92f, 0.9f, 0.86f), new Vector3(0.4f, 0.22f, 0.28f)),
                    LootKind.Vest => (new Color(0.22f, 0.25f, 0.2f), new Vector3(0.5f, 0.12f, 0.58f)),
                    _ => (new Color(0.24f, 0.26f, 0.22f), new Vector3(0.32f, 0.2f, 0.36f)),
                };
                if (k == LootKind.Vest || k == LootKind.Helmet) g = ArmorModel(k, parent, p);
                else
                {
                    g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(g.GetComponent<Collider>());
                    g.name = k.ToString(); g.transform.SetParent(parent, false);
                    g.transform.position = p + Vector3.up * size.y / 2; g.transform.localScale = size; g.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
                    g.GetComponent<Renderer>().sharedMaterial = PBRMat("M_Loot" + k, "concrete_floor_02", col, 0.6f);
                }
                if (k == LootKind.Medkit)
                {
                    var cross = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(cross.GetComponent<Collider>());
                    cross.transform.SetParent(g.transform, false); cross.transform.localPosition = new Vector3(0, 0.51f, 0); cross.transform.localScale = new Vector3(0.5f, 0.02f, 0.18f);
                    cross.GetComponent<Renderer>().sharedMaterial = PBRMat("M_LootCross", "concrete_floor_02", new Color(0.8f, 0.1f, 0.1f), 1f);
                }
                l = g.AddComponent<KryvonLoot>(); l.kind = k;
                l.ammoType = lootGuns[rng.Next(lootGuns.Count)].ammoType; l.amount = k == LootKind.Bandage ? 5 : 30; l.level = 1 + (rng.NextDouble() < 0.3 ? 1 : 0) + (rng.NextDouble() < 0.08 ? 1 : 0);
                glow = l.level >= 3 ? new Color(1f, 0.8f, 0.2f) : l.level == 2 ? new Color(0.3f, 0.65f, 1f) : new Color(0.85f, 0.85f, 0.85f);
            }
            // soft rarity glow under the item
            if (glowMat == null)
            {
                glowMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); glowMat.SetFloat("_Surface", 1); glowMat.SetFloat("_Blend", 2);
                glowMat.SetOverrideTag("RenderType", "Transparent"); glowMat.renderQueue = 3000; glowMat.SetFloat("_ZWrite", 0);
                glowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One); glowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                glowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glowMat.SetTexture("_BaseMap", Tex("T_LootGlow", 64, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 32f; float a = Mathf.Clamp01(1 - d); return new Color(a * a, a * a, a * a, 1); }));
                AssetDatabase.DeleteAsset(Root + "/Materials/M_LootGlow.mat"); AssetDatabase.CreateAsset(glowMat, Root + "/Materials/M_LootGlow.mat");
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Glow"; q.transform.SetParent(g.transform.parent, false); q.transform.position = p + Vector3.up * 0.015f; q.transform.rotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = Vector3.one * 1.0f;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", glow * 0.55f);
            var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = glowMat; qr.SetPropertyBlock(mpb); qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.transform.SetParent(g.transform, true);
        }

        public static void CaptureHouses()
        {
            var cam = Camera.main; var dir = Directory.GetCurrentDirectory(); float fov = cam.fieldOfView; cam.fieldOfView = 62;
            var hs = GameObject.Find("Houses").transform;
            int shot = 0;
            for (int i = 0; i < hs.childCount && shot < 4; i += 5)
            {
                var h = hs.GetChild(i); var col = h.GetComponentsInChildren<BoxCollider>(); var b = new Bounds(h.position, Vector3.zero);
                foreach (var r in h.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                var f = h.forward; var rgt = h.right;
                if (shot == 0 || shot == 2)   // outside, three-quarter view of the front
                { cam.transform.position = h.position - h.forward * (b.extents.magnitude * 1.4f) + h.right * b.extents.x * 0.8f + Vector3.up * 3f; cam.transform.LookAt(b.center); }
                else                          // inside the ground floor, looking across a room
                { cam.transform.position = h.position + Vector3.up * 1.6f - h.forward * 2.5f - h.right * 2.5f; cam.transform.rotation = Quaternion.LookRotation(h.forward * 0.6f + h.right * 0.8f - Vector3.up * 0.25f); }
                KryvonShowcase.Capture(Path.Combine(dir, $"House{shot++}.png"), 1280, 720);
            }
            cam.fieldOfView = fov;
        }

        // ---------- placement ----------
        static void ScatterHouses(Terrain terrain, Transform parent)
        {
            float Y(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            var root = new GameObject("Houses").transform; root.SetParent(parent, false);
            var lootRoot = new GameObject("Loot").transform; lootRoot.SetParent(parent, false);
            int meshId = 0, houses = 0, loot = 0;
            for (int cl = 0; cl < 22; cl++)
            {
                // cluster centre in the open, leaving plenty of empty fields between clusters
                float cx = 0, cz = 0; bool ok = false;
                for (int t = 0; t < 80 && !ok; t++) { cx = R(-260, 260); cz = R(-260, 260); ok = (Mathf.Abs(cx) > 90 || Mathf.Abs(cz) > 55) && Free(new Vector2(cx, cz), 22); }
                if (!ok) continue;
                int n = rng.NextDouble() < 0.35 ? 1 : rng.NextDouble() < 0.6 ? 2 : 3;
                for (int k = 0; k < n; k++)
                {
                    float w = R(9.5f, 14f), d = R(8.5f, 12f); int floors = rng.NextDouble() < 0.55 ? 2 : 3;
                    float x = cx + (k == 0 ? 0 : R(-18, 18)), z = cz + (k == 0 ? 0 : R(-18, 18));
                    float rad = Mathf.Max(w, d) * 0.75f;
                    if (!Free(new Vector2(x, z), rad)) continue;
                    float yaw = rng.Next(4) * 90f;
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) { float h = Y(x + i * w / 2, z + j * d / 2); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h); }
                    var b = new Builder();
                    var spots = HouseShape(b, w, d, floors, hi - lo + 0.6f);
                    var go = b.Build("House", root, new Vector3(x, hi + 0.05f, z), yaw, ref meshId);
                    foreach (var s in spots) { DropLoot(lootRoot, go.transform.TransformPoint(s)); loot++; }
                    blocked.Add(new Rect(x - rad, z - rad, rad * 2, rad * 2));
                    houses++;
                }
            }
            Debug.Log($"KRYVON: {houses} enterable houses, {loot} loot items");
        }
    }
}

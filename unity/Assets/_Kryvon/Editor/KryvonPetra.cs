// Archaeological sites: Petra (rose-sandstone canyon, the Siq, Al-Khazneh façade) and a Jerash-style Roman site
// (colonnaded Cardo with lintels, semicircular theatre with tiered seating). Procedural, with Poly Haven CC0 rock textures.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        public static readonly (Vector2 c, float r)[] Sites = { (new Vector2(-320, -300), 75f), (new Vector2(10, 310), 70f) };

        static Mesh SaveMesh(Mesh m, string name)
        {
            var p = $"{Root}/Settings/Site_{name}.asset"; AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(m, p); return m;
        }

        /// a noisy rock wall from a to b (world, at ground y), rising `height`, displaced along `normal`
        static void CliffWall(Transform parent, string name, Vector3 a, Vector3 b, float height, Vector3 normal, Material mat, float rough = 2.2f, int seed = 0)
        {
            float len = Vector3.Distance(a, b); int nu = Mathf.Max(8, (int)(len / 1.2f)), nv = Mathf.Max(8, (int)(height / 1.2f));
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int j = 0; j <= nv; j++) for (int i = 0; i <= nu; i++)
            {
                float u = i / (float)nu, v = j / (float)nv;
                var p = Vector3.Lerp(a, b, u);
                float topJag = 1f + 0.18f * (Mathf.PerlinNoise(u * 6 + seed, 3.7f) - 0.5f);
                float y = height * v * topJag;
                // horizontal sandstone strata + large lumps + fine grain
                float n = (Mathf.PerlinNoise(p.x * 0.05f + seed, y * 0.05f) - 0.5f) * rough * 2.2f
                        + (Mathf.PerlinNoise(p.z * 0.18f + y * 0.02f, y * 0.9f + seed) - 0.5f) * rough * 0.5f
                        + Mathf.Sin(y * 1.3f + Mathf.PerlinNoise(u * 4, seed) * 3f) * 0.18f;
                float lean = -v * v * height * 0.06f;      // slight overhang towards the top
                verts.Add(p + Vector3.up * y + normal * (n + lean));
                uvs.Add(new Vector2(Vector3.Dot(p, (b - a).normalized) / 6f, y / 6f));
            }
            for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++)
            {
                int k = j * (nu + 1) + i;
                bool face = Vector3.Dot(Vector3.Cross(b - a, Vector3.up), normal) > 0;
                if (face) tris.AddRange(new[] { k, k + nu + 1, k + 1, k + 1, k + nu + 1, k + nu + 2 });
                else tris.AddRange(new[] { k, k + 1, k + nu + 1, k + 1, k + nu + 2, k + nu + 1 });
            }
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            SaveMesh(m, name);
            var g = new GameObject(name); g.transform.SetParent(parent, true);
            g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            g.AddComponent<MeshCollider>().sharedMesh = m;
        }

        static GameObject P(PrimitiveType t, Transform parent, Vector3 lp, Vector3 s, Material m, Vector3 e = default, bool collide = true)
        {
            var g = GameObject.CreatePrimitive(t); if (!collide) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = s; g.transform.localRotation = Quaternion.Euler(e);
            g.GetComponent<Renderer>().sharedMaterial = m; return g;
        }

        // classical column: plinth, torus base, shaft, flared Corinthian capital, abacus
        static void Column(Transform parent, Vector3 lp, float h, float r, Material m)
        {
            P(PrimitiveType.Cube, parent, lp + Vector3.up * 0.2f, new Vector3(r * 2.8f, 0.4f, r * 2.8f), m);
            P(PrimitiveType.Cylinder, parent, lp + Vector3.up * 0.55f, new Vector3(r * 2.5f, 0.15f, r * 2.5f), m, default, false);
            float sh = h - 2.2f;
            P(PrimitiveType.Cylinder, parent, lp + Vector3.up * (0.7f + sh / 2), new Vector3(r * 2, sh / 2, r * 2), m);
            P(PrimitiveType.Cylinder, parent, lp + Vector3.up * (0.7f + sh + 0.35f), new Vector3(r * 2.3f, 0.35f, r * 2.3f), m, default, false);
            P(PrimitiveType.Cylinder, parent, lp + Vector3.up * (0.7f + sh + 0.85f), new Vector3(r * 2.9f, 0.2f, r * 2.9f), m, default, false);
            P(PrimitiveType.Cube, parent, lp + Vector3.up * (h - 0.2f), new Vector3(r * 3.2f, 0.4f, r * 3.2f), m, default, false);
        }

        // triangular pediment (gable) as a mesh, width w, height h, depth d
        static void Pediment(Transform parent, Vector3 lp, float w, float h, float d, Material m, string id, bool half = false, int side = 1)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            Vector3 L = new(-w / 2, 0, 0), R = new(w / 2, 0, 0), T = new(0, h, 0);
            if (half) { if (side > 0) { L = new(0, 0, 0); T = new(0, h, 0); } else { R = new(0, 0, 0); T = new(0, h, 0); } }
            foreach (var z in new[] { -d / 2, d / 2 }) { v.Add(L + Vector3.forward * z); v.Add(R + Vector3.forward * z); v.Add(T + Vector3.forward * z); }
            t.AddRange(new[] { 0, 2, 1, 3, 4, 5, 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4, 2, 0, 3, 2, 3, 5 });
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.SetUVs(0, v.ConvertAll(q => new Vector2(q.x / 3, q.y / 3)));
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); SaveMesh(mesh, "Ped_" + id);
            var g = new GameObject("Pediment"); g.transform.SetParent(parent, false); g.transform.localPosition = lp;
            g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = m;
            // the cornice under the gable
            P(PrimitiveType.Cube, parent, lp + Vector3.down * 0.25f, new Vector3(half ? w / 2 + 0.6f : w + 0.6f, 0.5f, d + 0.4f), m, default, false).transform.localPosition += half ? Vector3.right * side * w / 4 : Vector3.zero;
        }

        static void BuildPetra(Terrain terrain, Transform parent)
        {
            var (c, _) = Sites[0];
            float gy = terrain.SampleHeight(new Vector3(c.x, 0, c.y)) + terrain.transform.position.y;
            var rose = new Color(1f, 0.72f, 0.6f);
            var cliff = PBRMat("M_PetraCliff", "cliff_side", new Color(1f, 0.66f, 0.55f), 14f, 1.8f);
            cliff.SetFloat("_Cull", 0); cliff.doubleSidedGI = true;   // rock walls seen from both sides
            var carve = PBRMat("M_PetraCarve", "cliff_side", new Color(1f, 0.66f, 0.54f), 6f, 0.6f);
            var shade = PBRMat("M_PetraDoor", "rock_face_03", new Color(0.18f, 0.12f, 0.1f), 2f);
            var sand = PBRMat("M_PetraSand", "coast_sand_01", new Color(1f, 0.8f, 0.68f), 5f);
            var root = new GameObject("Petra").transform; root.SetParent(parent, false);
            // façade faces the map centre
            var toCentre = (-new Vector3(c.x, 0, c.y)).normalized; float yaw = Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg;
            root.position = new Vector3(c.x, gy, c.y); root.rotation = Quaternion.Euler(0, yaw + 180f, 0);   // local -Z = towards the centre
            Vector3 W(float x, float z) => root.TransformPoint(new Vector3(x, 0, z));
            Vector3 Nn(float x, float z) => root.TransformDirection(new Vector3(x, 0, z)).normalized;

            // plaza floor
            var floor = new Builder(); floor.Box(sand, new Vector3(0, 0.0f, -25), new Vector3(52, 0.2f, 56)); floor.Box(sand, new Vector3(0, 0.0f, -84), new Vector3(14, 0.2f, 66));
            int mid = 20000; floor.Build("PlazaFloor", root, root.position, root.eulerAngles.y, ref mid);

            // canyon: back wall (behind the façade), side walls, and the Siq leading in
            CliffWall(root, "BackL", W(-26, 2), W(-13.5f, 2), 48, Nn(0, -1), cliff, 2.0f, 1);
            CliffWall(root, "BackR", W(13.5f, 2), W(26, 2), 48, Nn(0, -1), cliff, 2.0f, 2);
            CliffWall(root, "BackTop", W(-13.5f, 3.5f), W(13.5f, 3.5f), 48, Nn(0, -1), cliff, 1.2f, 3);
            CliffWall(root, "SideL", W(-26, -52), W(-26, 2), 44, Nn(1, 0), cliff, 2.6f, 4);
            CliffWall(root, "SideR", W(26, 2), W(26, -52), 44, Nn(-1, 0), cliff, 2.6f, 5);
            CliffWall(root, "FrontL", W(-26, -52), W(-5, -52), 40, Nn(0, 1), cliff, 2.4f, 6);
            CliffWall(root, "FrontR", W(5, -52), W(26, -52), 40, Nn(0, 1), cliff, 2.4f, 7);
            // the Siq: a narrow winding gorge, 4–6 m wide, 60 m long
            float px = 0; for (int k = 0; k < 5; k++)
            {
                float z0 = -52 - k * 12, z1 = z0 - 12, nx = Mathf.Sin(k * 1.3f) * 2.5f;
                CliffWall(root, $"SiqL{k}", W(px - 2.6f, z1), W(px - 2.6f, z0), 36 - k * 2, Nn(1, 0), cliff, 1.4f, 10 + k);
                CliffWall(root, $"SiqR{k}", W(px + 2.6f, z0), W(px + 2.6f, z1), 36 - k * 2, Nn(-1, 0), cliff, 1.4f, 20 + k);
                px = nx;
            }

            // ---------- Al-Khazneh ----------
            var f = new GameObject("AlKhazneh").transform; f.SetParent(root, false); f.localPosition = new Vector3(0, 0, 0.6f);
            P(PrimitiveType.Cube, f, new Vector3(0, 0.6f, 0.4f), new Vector3(27, 1.2f, 4.5f), carve);                        // podium
            for (int s = 0; s < 3; s++) P(PrimitiveType.Cube, f, new Vector3(0, 0.2f + s * 0.4f, -2.2f - s * 0.45f), new Vector3(14, 0.4f, 0.5f), carve); // steps
            P(PrimitiveType.Cube, f, new Vector3(0, 9f, 2.6f), new Vector3(27, 18f, 1.2f), carve);                             // carved back plane, lower storey
            P(PrimitiveType.Cube, f, new Vector3(0, 6.2f, 1.95f), new Vector3(3.4f, 8.4f, 0.2f), shade, default, false);       // dark doorway
            P(PrimitiveType.Cube, f, new Vector3(0, 10.7f, 1.9f), new Vector3(4.6f, 0.6f, 0.3f), carve, default, false);       // door lintel
            foreach (var x in new[] { -10.5f, -6.3f, -2.1f, 2.1f, 6.3f, 10.5f }) Column(f, new Vector3(x, 1.2f, -0.6f), 13f, 0.62f, carve);
            P(PrimitiveType.Cube, f, new Vector3(0, 14.7f, 0.3f), new Vector3(26.5f, 1.0f, 2.6f), carve);                    // architrave
            P(PrimitiveType.Cube, f, new Vector3(0, 15.5f, 0.2f), new Vector3(27.2f, 0.6f, 2.9f), carve, default, false);    // frieze + cornice
            Pediment(f, new Vector3(0, 16.05f, 0.2f), 12.6f, 3.4f, 2.6f, carve, "main");
            // upper storey
            P(PrimitiveType.Cube, f, new Vector3(0, 20.3f, 2.4f), new Vector3(27, 9f, 1.6f), carve);                          // carved back plane, upper storey
            P(PrimitiveType.Cube, f, new Vector3(0, 19.9f, 0.9f), new Vector3(27, 0.8f, 3.4f), carve);                        // upper podium
            // tholos (round kiosk) in the middle
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; if (Mathf.Sin(a) > 0.45f) continue; Column(f, new Vector3(Mathf.Cos(a) * 3.1f, 20.3f, Mathf.Sin(a) * 3.1f + 0.4f), 8.6f, 0.38f, carve); }
            P(PrimitiveType.Cylinder, f, new Vector3(0, 24.6f, 1.1f), new Vector3(4.6f, 4.3f, 4.6f), carve, default, false);   // tholos drum
            P(PrimitiveType.Cylinder, f, new Vector3(0, 29.1f, 0.4f), new Vector3(7.2f, 0.25f, 7.2f), carve, default, false);   // entablature ring
            var cone = P(PrimitiveType.Sphere, f, new Vector3(0, 29.6f, 0.4f), new Vector3(6.6f, 2.6f, 6.6f), carve, default, false);   // shallow conical roof
            P(PrimitiveType.Cylinder, f, new Vector3(0, 31.1f, 0.4f), new Vector3(0.9f, 0.5f, 0.9f), carve, default, false);   // capital under the urn
            P(PrimitiveType.Sphere, f, new Vector3(0, 32.3f, 0.4f), new Vector3(1.5f, 1.9f, 1.5f), carve, default, false);     // the urn
            // side pavilions with broken (half) pediments
            foreach (var sd in new[] { -1, 1 })
            {
                foreach (var x in new[] { 6.6f, 9.4f, 12.2f }) Column(f, new Vector3(sd * x, 20.3f, -0.1f), 8.6f, 0.42f, carve);
                P(PrimitiveType.Cube, f, new Vector3(sd * 9.4f, 29.2f, 0.0f), new Vector3(7.4f, 0.6f, 2.4f), carve, default, false);
                Pediment(f, new Vector3(sd * 6.0f, 29.5f, 0.0f), 7.2f, 2.2f, 2.2f, carve, "half" + sd, true, sd);
            }
            // weathering: dark streaks down the cliff above the façade
            lootTier = 3; var pl = new GameObject("PetraLoot").transform; pl.SetParent(root, false);
            for (int k = 0; k < 22; k++) DropLoot(pl, root.TransformPoint(new Vector3(R(-20, 20), 0.12f, R(-48, -4))));
            Debug.Log("KRYVON: Petra built");
        }

        public static void CaptureSites()
        {
            var cam = Camera.main; var dir = System.IO.Directory.GetCurrentDirectory(); float fov = cam.fieldOfView; cam.fieldOfView = 55;
            var pet = GameObject.Find("Petra").transform; var jer = GameObject.Find("Jerash").transform;
            var shots = new (Vector3 p, Vector3 l)[] {
                (pet.TransformPoint(new Vector3(4, 1.7f, -30)), pet.TransformPoint(new Vector3(0, 17, 0))),
                (pet.TransformPoint(new Vector3(-1, 1.7f, -14)), pet.TransformPoint(new Vector3(0, 9, 0))),
                (pet.TransformPoint(new Vector3(0.5f, 1.7f, -88)), pet.TransformPoint(new Vector3(0, 6, -40))),
                (jer.TransformPoint(new Vector3(-3, 2, -50)), jer.TransformPoint(new Vector3(0, 5, 10))),
                (jer.TransformPoint(new Vector3(36, 9, 34)), jer.TransformPoint(new Vector3(36, 2, 6))),
                (jer.TransformPoint(new Vector3(-30, 28, -90)), jer.TransformPoint(new Vector3(10, 0, 0))) };
            for (int i = 0; i < shots.Length; i++) { cam.transform.position = shots[i].p; cam.transform.LookAt(shots[i].l); KryvonShowcase.Capture(System.IO.Path.Combine(dir, $"Site{i}.png"), 1280, 720); }
            cam.fieldOfView = fov;
        }

        static void BuildJerash(Terrain terrain, Transform parent)
        {
            var (c, _) = Sites[1];
            float gy = terrain.SampleHeight(new Vector3(c.x, 0, c.y)) + terrain.transform.position.y;
            var lime = PBRMat("M_RomanStone", "sandstone_blocks_08", new Color(0.98f, 0.9f, 0.76f), 2.5f);
            var pave = PBRMat("M_RomanPave", "cobblestone_floor_08", new Color(0.95f, 0.88f, 0.75f), 2f);
            var root = new GameObject("Jerash").transform; root.SetParent(parent, false); root.position = new Vector3(c.x, gy, c.y);
            // Cardo: paved street lined with columns carrying lintels (some fallen)
            var b = new Builder(); b.Box(pave, new Vector3(0, 0.05f, 0), new Vector3(10, 0.1f, 110)); int mid = 30000;
            b.Build("Cardo", root, root.position, 0, ref mid);
            for (int i = 0; i < 18; i++)
            {
                float z = -52 + i * 6.1f;
                foreach (var x in new[] { -5.2f, 5.2f })
                {
                    if (rng.NextDouble() < 0.12)   // a toppled column lying in the street
                    { P(PrimitiveType.Cylinder, root, new Vector3(x * 0.6f, 0.45f, z + 1.5f), new Vector3(0.9f, 3.2f, 0.9f), lime, new Vector3(90, R(-30, 30), 0)); continue; }
                    Column(root, new Vector3(x, 0.1f, z), R(8.6f, 9.4f), 0.42f, lime);
                    if (i < 17 && rng.NextDouble() < 0.7) P(PrimitiveType.Cube, root, new Vector3(x, 9.3f, z + 3.05f), new Vector3(1.1f, 0.7f, 6.2f), lime, default, false);  // lintel
                }
            }
            // theatre: semicircular cavea of stone tiers + stage building, beside the Cardo
            var th = new GameObject("Theatre").transform; th.SetParent(root, false); th.localPosition = new Vector3(36, 0, 10); th.localRotation = Quaternion.Euler(0, -90, 0);
            for (int row = 0; row < 16; row++)
            {
                float rad = 9 + row * 1.0f, y = 0.45f * row + 0.22f;
                int seg = 28 + row * 2;
                for (int k = 0; k < seg; k++)
                {
                    float a = Mathf.PI * (k + 0.5f) / seg; float len = Mathf.PI * rad / seg + 0.05f;
                    var g = P(PrimitiveType.Cube, th, new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad), new Vector3(len, 0.45f, 1.0f), lime, new Vector3(0, -a * Mathf.Rad2Deg + 90, 0));
                }
            }
            P(PrimitiveType.Cube, th, new Vector3(0, 0.05f, 4.5f), new Vector3(17, 0.1f, 9), pave);                         // orchestra
            P(PrimitiveType.Cube, th, new Vector3(0, 1.0f, -1.5f), new Vector3(26, 2, 4), lime);                             // stage
            P(PrimitiveType.Cube, th, new Vector3(0, 6.5f, -4.0f), new Vector3(30, 13, 1.6f), lime);                         // scaenae frons
            for (int k = 0; k < 7; k++) Column(th, new Vector3(-12 + k * 4, 2f, -2.6f), 7.5f, 0.32f, lime);
            foreach (var x in new[] { -6f, 0f, 6f }) P(PrimitiveType.Cube, th, new Vector3(x, 4.2f, -3.15f), new Vector3(2.4f, 4.4f, 0.2f), PBRMat("M_PetraDoor", "rock_face_03", new Color(0.18f, 0.12f, 0.1f), 2f), default, false);
            // Hadrian's-arch style gate at the south end
            var gate = new GameObject("Arch").transform; gate.SetParent(root, false); gate.localPosition = new Vector3(0, 0, -62);
            foreach (var x in new[] { -6.5f, 6.5f }) P(PrimitiveType.Cube, gate, new Vector3(x, 6, 0), new Vector3(4, 12, 4), lime);
            P(PrimitiveType.Cube, gate, new Vector3(0, 13, 0), new Vector3(17, 3, 4), lime);
            foreach (var x in new[] { -8.8f, -4.2f, 4.2f, 8.8f }) Column(gate, new Vector3(x, 0, -2.4f), 11.5f, 0.38f, lime);
            lootTier = 3; var jl = new GameObject("JerashLoot").transform; jl.SetParent(root, false);
            for (int k = 0; k < 18; k++) DropLoot(jl, root.TransformPoint(new Vector3(R(-4, 4), 0.12f, R(-50, 50))));
            for (int k = 0; k < 8; k++) DropLoot(jl, root.TransformPoint(new Vector3(36 + R(-6, 6), 0.12f, 10 + R(1, 8))));
            Debug.Log("KRYVON: Jerash built");
        }
    }
}

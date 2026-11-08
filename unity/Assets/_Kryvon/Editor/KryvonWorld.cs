// Dresses the play area: a real Terrain (gentle hills, flat under the town) with dirt/sand/grass layers,
// short + tall waving grass, trees, road barriers, sandbag posts, burned cars, hangars, garages, containers, barrels.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonWorld
    {
        const string Root = "Assets/_Kryvon";
        const float Size = 600f, HeightScale = 40f, Base = 6f;   // the town sits at y=0, which is Base metres above the terrain floor
        static System.Random rng;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // keep-out rectangles (town, street) so dressing never lands inside buildings
        static readonly List<Rect> blocked = new();
        static bool Free(Vector2 p, float r)
        {
            foreach (var b in blocked) if (p.x > b.xMin - r && p.x < b.xMax + r && p.y > b.yMin - r && p.y < b.yMax + r) return false;
            return true;
        }

        static Texture2D Tex(string name, int size, System.Func<int, int, Color> f, bool alpha = false)
        {
            var path = $"{Root}/Materials/{name}.png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, f(x, y));
                File.WriteAllBytes(path, t.EncodeToPNG()); AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.alphaIsTransparency = alpha; ti.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // clump of grass blades on transparent background
        static Texture2D GrassBlades(string name, bool tall)
        {
            var path = $"{Root}/Materials/{name}.png";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int S = 256; var px = new Color[S * S];
            var r = new System.Random(tall ? 7 : 3);
            int blades = tall ? 46 : 70;
            for (int b = 0; b < blades; b++)
            {
                float x0 = 20 + (float)r.NextDouble() * (S - 40), lean = ((float)r.NextDouble() - 0.5f) * (tall ? 70 : 50);
                float h = S * (0.45f + (float)r.NextDouble() * 0.53f), w = tall ? 5f : 6.5f;
                var c = Color.Lerp(new Color(0.42f, 0.5f, 0.22f), new Color(0.78f, 0.72f, 0.38f), (float)r.NextDouble() * (tall ? 0.9f : 0.6f));
                for (int y = 0; y < h; y++)
                {
                    float k = y / h, cx = x0 + lean * k * k, half = w * (1 - k);
                    for (int x = (int)(cx - half - 1); x <= cx + half + 1; x++)
                    {
                        if (x < 0 || x >= S) continue;
                        float a = Mathf.Clamp01(half + 0.5f - Mathf.Abs(x - cx));
                        var col = c * (0.7f + 0.3f * k); col.a = a;
                        int i = y * S + x; if (a > px[i].a) px[i] = col;
                    }
                }
            }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true); t.SetPixels(px); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG()); AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static TerrainLayer Layer(string name, Texture2D diff, Texture2D nrm, float tile)
        {
            var path = $"{Root}/Settings/TL_{name}.terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            l.diffuseTexture = diff; l.normalMapTexture = nrm; l.tileSize = new Vector2(tile, tile); l.smoothness = 0.05f;
            EditorUtility.SetDirty(l); return l;
        }
        static Texture2D PH(string f, bool normal)
        {
            var path = $"{Root}/ThirdParty/PolyHaven/{f}";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti == null) return null;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void Dress()
        {
            rng = new System.Random(1946);
            blocked.Clear();
            blocked.Add(new Rect(-75, -30, 160, 68));      // the town block and street
            blocked.Add(new Rect(-120, -6, 240, 12));      // the road itself, end to end
            var oldGround = GameObject.Find("Ground"); if (oldGround) Object.DestroyImmediate(oldGround);
            var world = new GameObject("Outskirts").transform;

            // ---------- terrain ----------
            var td = new TerrainData { heightmapResolution = 513, alphamapResolution = 512 };
            td.SetDetailResolution(512, 16);
            td.size = new Vector3(Size, HeightScale, Size);
            int N = td.heightmapResolution; var h = new float[N, N];
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            {
                float wx = i / (N - 1f) * Size - Size / 2, wz = j / (N - 1f) * Size - Size / 2;
                float hills = Mathf.PerlinNoise(wx * 0.006f + 11, wz * 0.006f + 7) * 7f + Mathf.PerlinNoise(wx * 0.025f + 3, wz * 0.025f) * 1.4f;
                float town = Mathf.Max(Mathf.Abs(wx) / 95f, Mathf.Abs(wz) / 48f);          // 0 in the middle, 1 at the town edge
                float road = Mathf.Clamp01((Mathf.Abs(wz) - 7f) / 25f);                      // the road keeps going flat along X
                float flat = Mathf.Min(Mathf.Clamp01((town - 1.3f) * 1.2f), road);
                h[j, i] = (Base + hills * flat) / HeightScale;
            }
            td.SetHeights(0, 0, h);

            var sandTex = PH("coast_sand_01_diff.jpg", false); var dirtTex = PH("dry_ground_01_diff.jpg", false);
            var grassTex = Tex("T_GrassGround", 512, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.21f + 9, y * 0.21f) * 0.4f;
                return Color.Lerp(new Color(0.33f, 0.36f, 0.18f), new Color(0.58f, 0.55f, 0.3f), n);
            });
            td.terrainLayers = new[]
            {
                Layer("Grass", grassTex, null, 5f),
                Layer("Dirt", dirtTex, PH("dry_ground_01_nor.jpg", true), 6f),
                Layer("Sand", sandTex, PH("coast_sand_01_nor.jpg", true), 8f),
            };
            int A = td.alphamapResolution; var am = new float[A, A, 3];
            for (int j = 0; j < A; j++) for (int i = 0; i < A; i++)
            {
                float wx = i / (A - 1f) * Size - Size / 2, wz = j / (A - 1f) * Size - Size / 2;
                float d = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.02f + 40, wz * 0.02f + 40) - 0.55f) * 3.5f);   // dirt patches
                float s = Mathf.Clamp01((Mathf.PerlinNoise(wx * 0.012f + 90, wz * 0.012f) - 0.7f) * 4f) * 0.6f;  // a little sand
                if (!Free(new Vector2(wx, wz), 2f)) d = Mathf.Max(d, 0.85f);                                       // worn ground in town
                float g = Mathf.Max(0.05f, 1f - d - s);
                float sum = d + g + s; am[j, i, 0] = g / sum; am[j, i, 1] = d / sum; am[j, i, 2] = s / sum;
            }
            td.SetAlphamaps(0, 0, am);

            // grass: short dense + tall sparse, waving
            td.detailPrototypes = new[]
            {
                new DetailPrototype { prototypeTexture = GrassBlades("T_GrassShort", false), renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.4f, maxWidth = 0.8f, minHeight = 0.25f, maxHeight = 0.55f, healthyColor = new Color(0.75f, 0.8f, 0.55f), dryColor = new Color(0.85f, 0.75f, 0.5f), noiseSpread = 0.4f, useDensityScaling = true },
                new DetailPrototype { prototypeTexture = GrassBlades("T_GrassTall", true), renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.6f, maxWidth = 1.1f, minHeight = 0.8f, maxHeight = 1.5f, healthyColor = new Color(0.8f, 0.78f, 0.5f), dryColor = new Color(0.9f, 0.8f, 0.55f), noiseSpread = 0.6f, useDensityScaling = true },
            };
            int D = td.detailResolution; var shortG = new int[D, D]; var tallG = new int[D, D];
            for (int j = 0; j < D; j++) for (int i = 0; i < D; i++)
            {
                float wx = i / (D - 1f) * Size - Size / 2, wz = j / (D - 1f) * Size - Size / 2;
                if (!Free(new Vector2(wx, wz), 1.5f)) continue;
                float gl = am[Mathf.Clamp(j * A / D, 0, A - 1), Mathf.Clamp(i * A / D, 0, A - 1), 0];
                float n = Mathf.PerlinNoise(wx * 0.08f, wz * 0.08f);
                shortG[j, i] = (int)(gl * 24 + n * 8);
                tallG[j, i] = n > 0.4f ? (int)(gl * 16 * (n - 0.3f) * 3) : 0;
            }
            td.SetDetailLayer(0, 0, 0, shortG); td.SetDetailLayer(0, 0, 1, tallG);
            td.wavingGrassStrength = 0.35f; td.wavingGrassAmount = 0.4f; td.wavingGrassSpeed = 0.5f; td.wavingGrassTint = new Color(0.9f, 0.88f, 0.7f);

            var tdPath = $"{Root}/Settings/Outskirts.asset";
            AssetDatabase.DeleteAsset(tdPath); AssetDatabase.CreateAsset(td, tdPath);
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-Size / 2, -Base, -Size / 2);
            var terrain = tgo.GetComponent<Terrain>();
            terrain.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            terrain.detailObjectDistance = 200f; terrain.detailObjectDensity = 1f; terrain.heightmapPixelError = 4; terrain.basemapDistance = 2000;
            terrain.drawInstanced = true;

            GrassField(terrain, am, A, world);

            // ---------- dressing ----------
            float Y(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) - Base;
            GameObject Put(string model, float x, float z, float yaw, float size, char axis, bool collide = true, float sink = 0.05f)
            {
                if (File.Exists($"{Root}/Models/{model}_b.glb") && rng.NextDouble() < 0.5) model += "_b";   // two generated variants of most props
                var g = Spawn(model, size, axis, world); if (g == null) return null;
                g.transform.position = new Vector3(x, Y(x, z) - sink, z); g.transform.rotation = Quaternion.Euler(0, yaw, 0);
                if (collide) Collide(g);
                return g;
            }
            bool Spot(out float x, out float z, float r, float minR = 0, float maxR = 280)
            {
                for (int t = 0; t < 60; t++)
                {
                    x = R(-maxR, maxR); z = R(-maxR, maxR);
                    if (Mathf.Abs(x) < minR && Mathf.Abs(z) < minR) continue;
                    if (Free(new Vector2(x, z), r)) { blocked.Add(new Rect(x - r, z - r, r * 2, r * 2)); return true; }
                }
                x = z = 0; return false;
            }

            // big structures on the outskirts
            foreach (var (m, n, sz) in new[] { ("Props/p_hangar", 2, 34f), ("Props/p_garage", 4, 14f), ("Props/p_container", 10, 6.1f) })
                for (int k = 0; k < n; k++) if (Spot(out var x, out var z, sz * 0.7f, 90)) Put(m, x, z, rng.Next(4) * 90, sz, m.Contains("hangar") || m.Contains("garage") ? 'x' : 'z');
            // road: concrete barriers in pairs (checkpoints) and burned cars on the shoulders
            for (float x = -250; x <= 250; x += R(18, 34))
            {
                if (Mathf.Abs(x) < 80) continue;
                float side = rng.NextDouble() < 0.5 ? -1 : 1;
                if (rng.NextDouble() < 0.45) { for (int k = 0; k < 3; k++) Put("Props/p_barrier", x + k * 2.1f, side * R(4.5f, 6f), 90 + R(-6, 6), 2.0f, 'x'); }
                else Put("Props/p_wreck", x, side * R(6.5f, 11f), R(0, 360), 4.3f, 'x');
            }
            // a checkpoint at each end of town: barriers across half the road + sandbag posts
            foreach (var ex in new[] { -92f, 92f })
            {
                for (int k = 0; k < 3; k++) Put("Props/p_barrier", ex, -4.5f + k * 2.1f, 0, 2.0f, 'x');
                Put("Props/p_sandbags", ex + Mathf.Sign(ex) * 4, 8f, ex > 0 ? 200 : 20, 3.2f, 'x');
                Put("Props/p_sandbags", ex + Mathf.Sign(ex) * 4, -9f, ex > 0 ? 340 : 160, 3.2f, 'x');
            }
            // scattered wrecks, barrels, sandbags in the fields
            for (int k = 0; k < 14; k++) if (Spot(out var x, out var z, 3, 60)) Put("Props/p_wreck", x, z, R(0, 360), 4.3f, 'x');
            for (int k = 0; k < 16; k++) if (Spot(out var x, out var z, 1.5f, 40)) Put("Props/p_barrels", x, z, R(0, 360), 1.6f, 'x');
            for (int k = 0; k < 10; k++) if (Spot(out var x, out var z, 2, 60)) Put("Props/p_sandbags", x, z, R(0, 360), 3.2f, 'x');
            // trees: olive groves, palms near the town, cypress lines
            for (int g = 0; g < 26; g++)
                if (Spot(out var gx, out var gz, 4, 70))
                    for (int k = 0; k < 9; k++) { float x = gx + (k % 3) * 7 + R(-1, 1), z = gz + (k / 3) * 7 + R(-1, 1); if (Free(new Vector2(x, z), 1)) Tree("Props/p_olive", x, z, R(4.5f, 6.5f)); }
            for (int k = 0; k < 45; k++) if (Spot(out var x, out var z, 1.5f, 0, 140)) Tree("Props/p_palm", x, z, R(8, 12));
            for (int k = 0; k < 60; k++) if (Spot(out var x, out var z, 1.5f, 50)) Tree("Props/p_cypress", x, z, R(9, 14));
            // cypress rows along the road out of town
            for (float x = 100; x < 280; x += 9) foreach (var sx in new[] { -1f, 1f }) { if (Free(new Vector2(sx * x, 13), 1)) Tree("Props/p_cypress", sx * x, 13 + R(-1, 1), R(10, 13)); if (Free(new Vector2(sx * x, -13), 1)) Tree("Props/p_cypress", sx * x, -13 + R(-1, 1), R(10, 13)); }
            void Tree(string m, float x, float z, float hgt)
            {
                var t = Put(m, x, z, R(0, 360), hgt, 'y', false, 0.2f); if (t == null) return;
                var c = t.AddComponent<CapsuleCollider>(); c.radius = 0.35f; c.height = hgt * 0.5f; c.center = new Vector3(0, hgt * 0.25f, 0);
                if (m.Contains("olive") && rng.NextDouble() < 0.75) Canopy(t, hgt);
            }
            Debug.Log("KRYVON: outskirts dressed");
        }

        // baked grass: crossed alpha-clipped quads in 25 m chunks (short everywhere grassy, tall in patches)
        static void GrassField(Terrain terrain, float[,,] am, int A, Transform parent)
        {
            var mats = new Material[2];
            for (int k = 0; k < 2; k++)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetTexture("_BaseMap", GrassBlades(k == 0 ? "T_GrassShort" : "T_GrassTall", k == 1));
                m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0); m.doubleSidedGI = true; m.SetFloat("_Smoothness", 0.1f);
                m.SetColor("_BaseColor", k == 0 ? new Color(0.95f, 0.98f, 0.8f) : new Color(1f, 0.95f, 0.78f));
                var path = $"{Root}/Materials/M_Grass{k}.mat"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path); mats[k] = m;
            }
            var root = new GameObject("Grass").transform; root.SetParent(parent, false);
            const float C = 25f, R0 = 170f; int made = 0;
            for (float cz = -R0; cz < R0; cz += C) for (float cx = -R0; cx < R0; cx += C)
            {
                var verts = new List<Vector3>[2] { new(), new() }; var uvs = new List<Vector2>[2] { new(), new() }; var cols = new List<Color>[2] { new(), new() };
                for (int t = 0; t < 1400; t++)
                {
                    float x = cx + R(0, C), z = cz + R(0, C);
                    if (!Free(new Vector2(x, z), 0.8f)) continue;
                    int ai = Mathf.Clamp((int)((x + Size / 2) / Size * (A - 1)), 0, A - 1), aj = Mathf.Clamp((int)((z + Size / 2) / Size * (A - 1)), 0, A - 1);
                    float g = am[aj, ai, 0]; if ((float)rng.NextDouble() > g * 1.1f) continue;
                    float patch = Mathf.PerlinNoise(x * 0.07f + 3, z * 0.07f + 8);
                    int k = patch > 0.58f && rng.NextDouble() < 0.55 ? 1 : 0;
                    float hgt = k == 1 ? R(0.8f, 1.35f) : R(0.25f, 0.55f), w = hgt * (k == 1 ? 0.75f : 1.3f);
                    float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y - 0.03f;
                    var tint = Color.Lerp(new Color(0.85f, 0.95f, 0.75f), new Color(1.05f, 0.95f, 0.75f), Mathf.PerlinNoise(x * 0.03f, z * 0.03f));
                    float a0 = R(0, Mathf.PI);
                    for (int q = 0; q < 2; q++)
                    {
                        float a = a0 + q * Mathf.PI / 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * w * 0.5f;
                        var lean = new Vector3(R(-0.1f, 0.1f), 0, R(-0.1f, 0.1f)) * hgt;
                        var vb = verts[k]; int i0 = vb.Count;
                        vb.Add(new Vector3(x, y, z) - d); vb.Add(new Vector3(x, y, z) + d); vb.Add(new Vector3(x, y + hgt, z) + d + lean); vb.Add(new Vector3(x, y + hgt, z) - d + lean);
                        uvs[k].AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                        for (int c = 0; c < 4; c++) cols[k].Add(tint);
                    }
                }
                for (int k = 0; k < 2; k++)
                {
                    if (verts[k].Count == 0) continue;
                    var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.SetVertices(verts[k]); mesh.SetUVs(0, uvs[k]); mesh.SetColors(cols[k]);
                    var tri = new int[verts[k].Count / 4 * 6];
                    for (int qd = 0, ti = 0; qd < verts[k].Count; qd += 4) { tri[ti++] = qd; tri[ti++] = qd + 2; tri[ti++] = qd + 1; tri[ti++] = qd; tri[ti++] = qd + 3; tri[ti++] = qd + 2; }
                    mesh.SetTriangles(tri, 0);
                    var n = new Vector3[verts[k].Count]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up; mesh.normals = n;   // up-normals: grass lit like the ground
                    mesh.RecalculateBounds();
                    var mp = $"{Root}/Settings/Grass_{made}.asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp); made++;
                    var go = new GameObject("GrassChunk"); go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[k]; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            // the terrain's own detail grass becomes a light filler
            terrain.detailObjectDensity = 0.35f;
        }

        // olive canopy: clustered, lumpy leaf blobs over the generated trunk (the generator leaves olives bare)
        static Material leafMat; static Mesh blob;
        static void Canopy(GameObject tree, float hgt)
        {
            if (leafMat == null)
            {
                var tex = Tex("T_OliveLeaves", 256, (x, y) =>
                {
                    float n = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.55f + Mathf.PerlinNoise(x * 0.6f + 5, y * 0.6f) * 0.45f;
                    return Color.Lerp(new Color(0.22f, 0.28f, 0.16f), new Color(0.55f, 0.6f, 0.42f), n);
                });
                leafMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); leafMat.SetTexture("_BaseMap", tex); leafMat.SetFloat("_Smoothness", 0.15f);
                leafMat.SetTextureScale("_BaseMap", new Vector2(3, 3));
                AssetDatabase.DeleteAsset(Root + "/Materials/M_OliveLeaves.mat"); AssetDatabase.CreateAsset(leafMat, Root + "/Materials/M_OliveLeaves.mat");
                // a lumpy sphere so blobs don't look like balls
                var src = GameObject.CreatePrimitive(PrimitiveType.Sphere); var sm = src.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(src);
                var v = sm.vertices; var nr = new System.Random(5);
                for (int i = 0; i < v.Length; i++) { var d = v[i].normalized; float k = 0.82f + 0.36f * Mathf.PerlinNoise(d.x * 3 + 10, d.y * 3 + d.z * 2); v[i] = d * 0.5f * k; }
                blob = new Mesh { vertices = v, triangles = sm.triangles, uv = sm.uv }; blob.RecalculateNormals(); blob.RecalculateBounds();
                AssetDatabase.DeleteAsset(Root + "/Materials/OliveBlob.asset"); AssetDatabase.CreateAsset(blob, Root + "/Materials/OliveBlob.asset");
            }
            int n = 7 + rng.Next(5); float w = hgt * 0.42f;
            for (int i = 0; i < n; i++)
            {
                var b = new GameObject("Leaves"); b.transform.SetParent(tree.transform, false);
                float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = (float)rng.NextDouble() * w * 0.75f;
                b.transform.localPosition = new Vector3(Mathf.Cos(a) * rr, hgt * R(0.62f, 0.9f), Mathf.Sin(a) * rr);
                b.transform.localScale = new Vector3(R(0.9f, 1.4f), R(0.6f, 0.9f), R(0.9f, 1.4f)) * w * 0.9f;
                b.transform.localRotation = Quaternion.Euler(0, R(0, 360), 0);
                b.AddComponent<MeshFilter>().sharedMesh = blob;
                var mr = b.AddComponent<MeshRenderer>(); mr.sharedMaterial = leafMat;
            }
        }

        static GameObject Spawn(string rel, float size, char axis, Transform parent)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/{rel}.glb");
            if (src == null) { Debug.LogWarning("KRYVON: missing " + rel); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return go;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float dim = axis == 'y' ? b.size.y : axis == 'z' ? b.size.z : Mathf.Max(b.size.x, b.size.z);
            go.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, dim));
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            var pivot = new GameObject(Path.GetFileName(rel)); pivot.transform.SetParent(parent, false);
            go.transform.SetParent(pivot.transform, true);
            foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            return pivot;
        }
        static void Collide(GameObject pivot)
        {
            var rs = pivot.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var bc = pivot.AddComponent<BoxCollider>();
            bc.center = pivot.transform.InverseTransformPoint(b.center);
            var s = pivot.transform.InverseTransformVector(b.size); bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 0.92f;
        }
    }
}

// The Levant map (2560 x 1396 m), built from world/levant_*.{r16,json,png} made by tools/make_levant.py from the
// owner's layout image. Step 1: terrain, ground colour, sea + lakes, roads (asphalt laid on levelled ground),
// markers for every region and tunnel gate. Saved as Scenes/Levant.unity (the current Play scene is untouched).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kryvon.EditorTools
{
    public static class KryvonLevant
    {
        const string Root = "Assets/_Kryvon";
        static string WorldDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "world"));

        [System.Serializable] class Region { public string name, arabic, kind; public float x, z, radius; public int tier; }
        [System.Serializable] class Meta { public float width, length, hmax, sea; public int res; public Region[] regions; }
        [System.Serializable] class Road { public string from, to, type; public float[][] points; }

        // the cities built on the map: region name, grid columns x rows (26 m blocks), loot tier core/edge, share of towers
        public static readonly (string region, int cols, int rows, int core, int edge, float towers)[] TownPlan =
        {
            ("Old Damascus", 6, 5, 3, 3, 0.10f), ("Damascus Quarter", 4, 3, 3, 2, 0.05f), ("Amman Downtown", 6, 5, 3, 2, 0.45f),
            ("Beirut Coastal", 5, 4, 3, 2, 0.35f), ("Old Jerusalem", 5, 4, 3, 2, 0.0f), ("Al Salt", 4, 3, 2, 2, 0.05f),
            ("Aleppo Citadel", 4, 3, 3, 2, 0.0f), ("Al-Orak", 3, 3, 2, 2, 0.2f), ("The Outskirts", 3, 2, 2, 1, 0.0f), ("Kharbet Saba", 2, 2, 2, 1, 0f),
            ("QAIA", 8, 5, 4, 3, 0.10f),
        };
        public static List<(Vector3[] pts, float width, string type)> RoadPaths = new();   // the last built map's roads (for dressing)
        /// every named place on the map (for the in-match map labels)
        public static (string name, float x, float z, string kind)[] Regions()
        {
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(Path.Combine(WorldDir, "levant_regions.json")));
            return System.Array.ConvertAll(meta.regions, r => (r.name, r.x, r.z, r.kind));
        }
        public static Vector2 RegionPos(string name)
        {
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(Path.Combine(WorldDir, "levant_regions.json")));
            var r = System.Array.Find(meta.regions, x => x.name == name); return r != null ? new Vector2(r.x, r.z) : Vector2.zero;
        }
        public static (float w, float l, float sea) Size()
        {
            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(Path.Combine(WorldDir, "levant_regions.json"))); return (meta.width, meta.length, meta.sea);
        }

        [MenuItem("KRYVON/Build Levant map (step 1)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var metaText = File.ReadAllText(Path.Combine(WorldDir, "levant_regions.json"));
            var meta = JsonUtility.FromJson<Meta>(metaText);
            var gates = ParseGates(metaText);
            var roads = ParseRoads(File.ReadAllText(Path.Combine(WorldDir, "levant_roads.json")));
            float W = meta.width, L = meta.length, HM = meta.hmax, SEA = meta.sea; int N = meta.res;

            // ---------- heights (r16, row 0 = north) ----------
            var raw = File.ReadAllBytes(Path.Combine(WorldDir, "levant_height.r16"));
            var hts = new float[N, N];
            for (int r = 0; r < N; r++) for (int c = 0; c < N; c++)
            {
                int i = (r * N + c) * 2; float v = (raw[i] | (raw[i + 1] << 8)) / 65535f;
                hts[N - 1 - r, c] = v;                                // Unity: [z, x], z = 0 at the south edge
            }
            // ---------- an island in the open sea: no square map border ----------
            // the land sinks under the sea along every edge; the coast follows a rounded square (superellipse) wobbled by
            // noise, so it reads as a natural shoreline from the jet and on the map
            {
                float seabed = Mathf.Max(0f, SEA - 9f) / HM;
                for (int r = 0; r < N; r++) for (int c = 0; c < N; c++)
                {
                    float u = c / (N - 1f) * 2f - 1f, v = r / (N - 1f) * 2f - 1f;
                    float sq = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 6f) + Mathf.Pow(Mathf.Abs(v), 6f), 1f / 6f);   // 1 at the edge, 0 in the middle
                    float n = Mathf.PerlinNoise(u * 3.1f + 11.3f, v * 3.1f + 5.7f) * 0.65f + Mathf.PerlinNoise(u * 9.7f + 3.1f, v * 9.7f + 9.4f) * 0.35f;
                    // the shore sits in the outermost strip (every region, even the Waiting Area at 0.91, stays on land);
                    // the superellipse rounds the four corners off into the sea
                    float coast = 0.97f + 0.02f * (n - 0.5f) * 2f;                             // 0.95 … 0.99
                    if (sq <= coast - 0.035f) continue;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sq - (coast - 0.035f)) / 0.05f));   // 0 inland → 1 out at sea
                    hts[r, c] = Mathf.Lerp(hts[r, c], Mathf.Min(hts[r, c], seabed), k);
                }
            }
            float H(float x, float z) { int c = Mathf.Clamp(Mathf.RoundToInt((x / W + 0.5f) * (N - 1)), 0, N - 1), r = Mathf.Clamp(Mathf.RoundToInt((z / L + 0.5f) * (N - 1)), 0, N - 1); return hts[r, c] * HM; }

            // ---------- flat plates for the cities and the waiting area ----------
            var plates = new List<(Vector2 c, Vector2 half)>();
            foreach (var tp in TownPlan) { var rg = System.Array.Find(meta.regions, r => r.name == tp.region); if (rg != null) plates.Add((new Vector2(rg.x, rg.z), new Vector2(tp.cols * 13f + 14, tp.rows * 13f + 14))); }
            { var rg = System.Array.Find(meta.regions, r => r.name == "Waiting Area"); if (rg != null) plates.Add((new Vector2(rg.x, rg.z), new Vector2(110, 90))); }
            foreach (var (pc, ph) in plates)
            {
                float sum = 0; int cnt = 0;
                for (float x = -ph.x; x <= ph.x; x += 10) for (float z = -ph.y; z <= ph.y; z += 10) { sum += H(pc.x + x, pc.y + z); cnt++; }
                float plate = Mathf.Max(sum / cnt, SEA + 2.5f) / HM;
                float cell = W / (N - 1), cellZ = L / (N - 1), margin = 45f;
                int c0 = Mathf.FloorToInt(((pc.x - ph.x - margin) / W + 0.5f) * (N - 1)), c1 = Mathf.CeilToInt(((pc.x + ph.x + margin) / W + 0.5f) * (N - 1));
                int r0 = Mathf.FloorToInt(((pc.y - ph.y - margin) / L + 0.5f) * (N - 1)), r1 = Mathf.CeilToInt(((pc.y + ph.y + margin) / L + 0.5f) * (N - 1));
                for (int r = Mathf.Max(0, r0); r <= Mathf.Min(N - 1, r1); r++) for (int c = Mathf.Max(0, c0); c <= Mathf.Min(N - 1, c1); c++)
                {
                    float wx = (c / (N - 1f) - 0.5f) * W, wz = (r / (N - 1f) - 0.5f) * L;
                    float dx = Mathf.Max(0, Mathf.Abs(wx - pc.x) - ph.x), dz = Mathf.Max(0, Mathf.Abs(wz - pc.y) - ph.y);
                    float d = Mathf.Sqrt(dx * dx + dz * dz); if (d > margin) continue;
                    hts[r, c] = Mathf.Lerp(hts[r, c], plate, Mathf.SmoothStep(1f, 0f, d / margin));
                }
            }

            // ---------- roads: level the ground along each road ----------
            var roadPaths = new List<(Vector3[] pts, float width, string type)>(); RoadPaths = roadPaths;
            foreach (var rd in roads)
            {
                if (rd.type == "tunnel") continue;
                var pts = Resample(rd.points, 6f); if (pts.Count < 2) continue;
                var ys = new float[pts.Count]; for (int i = 0; i < pts.Count; i++) ys[i] = Mathf.Max(SEA + 0.6f, H(pts[i].x, pts[i].y));
                for (int pass = 0; pass < 6; pass++) { var nys = (float[])ys.Clone(); for (int i = 1; i < ys.Length - 1; i++) nys[i] = (ys[i - 1] + ys[i] * 2 + ys[i + 1]) / 4; ys = nys; }
                float wdt = rd.type == "highway" ? 11f : 7f;
                var p3 = new Vector3[pts.Count]; for (int i = 0; i < pts.Count; i++) p3[i] = new Vector3(pts[i].x, ys[i], pts[i].y);
                roadPaths.Add((p3, wdt, rd.type));
                float cell = W / (N - 1), rad = wdt / 2 + 4f;
                foreach (var p in p3)
                {
                    int c0 = Mathf.RoundToInt((p.x / W + 0.5f) * (N - 1)), r0 = Mathf.RoundToInt((p.z / L + 0.5f) * (N - 1));
                    int rc = Mathf.CeilToInt(rad / cell) + 1;
                    for (int dr = -rc; dr <= rc; dr++) for (int dc = -rc; dc <= rc; dc++)
                    {
                        int r = r0 + dr, c = c0 + dc; if (r < 0 || c < 0 || r >= N || c >= N) continue;
                        float d = Mathf.Sqrt(dc * dc * cell * cell + dr * dr * (L / (N - 1)) * (L / (N - 1)));
                        if (d > rad) continue;
                        float k = d < wdt / 2 + 0.5f ? 1f : Mathf.SmoothStep(1f, 0f, (d - wdt / 2 - 0.5f) / 3.5f);
                        hts[r, c] = Mathf.Lerp(hts[r, c], (p.y - 0.12f) / HM, k);
                    }
                }
            }

            // ---------- terrain ----------
            var td = new TerrainData { heightmapResolution = N, alphamapResolution = 1024 };
            td.size = new Vector3(W, HM, L);
            // save the asset FIRST: the painted splat textures are created as sub-assets of it (painting before saving loses them)
            var tdPath = $"{Root}/Settings/LevantTerrain.asset"; AssetDatabase.DeleteAsset(tdPath); AssetDatabase.CreateAsset(td, tdPath);
            td.SetHeights(0, 0, hts);
            Texture2D T(string f, bool nrm) { var p = $"{Root}/ThirdParty/PolyHaven/{f}"; var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti && nrm && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); } return AssetDatabase.LoadAssetAtPath<Texture2D>(p); }
            TerrainLayer Lay(string n, string diff, string nrm, float tile, Color tint)
            {
                var path = $"{Root}/Settings/TL_Levant_{n}.terrainlayer";
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path); if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
                l.diffuseTexture = T(diff, false); l.normalMapTexture = nrm != null ? T(nrm, true) : null; l.tileSize = new Vector2(tile, tile); l.diffuseRemapMax = tint; l.smoothness = 0.04f;
                EditorUtility.SetDirty(l); return l;
            }
            var grassL = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{Root}/Settings/TL_Grass.terrainlayer");
            td.terrainLayers = new[]
            {
                Lay("Grass", "dry_ground_01_diff.jpg", "dry_ground_01_nor.jpg", 6f, new Color(0.42f, 0.62f, 0.26f, 1)),
                Lay("Dirt", "dry_ground_01_diff.jpg", "dry_ground_01_nor.jpg", 7f, new Color(0.82f, 0.68f, 0.5f, 1)),
                Lay("Sand", "coast_sand_01_diff.jpg", "coast_sand_01_nor.jpg", 9f, new Color(1f, 0.93f, 0.8f, 1)),
                Lay("Rock", "rock_face_03_diff.jpg", "rock_face_03_nor.jpg", 14f, new Color(0.78f, 0.72f, 0.66f, 1)),
                Lay("Asphalt", "asphalt_02_diff.jpg", "asphalt_02_nor.jpg", 6f, new Color(0.8f, 0.8f, 0.82f, 1)),
                Lay("RoseRock", "red_sandstone_wall_diff.jpg", "red_sandstone_wall_nor.jpg", 10f, new Color(1f, 0.85f, 0.8f, 1)),
            };
            // splat: height, slope, masks (forest / olives / desert), roads, the pink sandstone of Petra & Wadi Musa
            var maskTex = new Texture2D(2, 2); maskTex.LoadImage(File.ReadAllBytes(Path.Combine(WorldDir, "levant_masks.png")));
            int A = td.alphamapResolution; var al = new float[A, A, 6];
            var petra = System.Array.Find(meta.regions, r => r.name == "Petra"); var musa = System.Array.Find(meta.regions, r => r.name == "Wadi Musa");
            for (int y = 0; y < A; y++) for (int x = 0; x < A; x++)
            {
                float u = x / (A - 1f), v = y / (A - 1f);
                float wx = (u - 0.5f) * W, wz = (v - 0.5f) * L;
                float hgt = td.GetInterpolatedHeight(u, v), steep = td.GetSteepness(u, v);
                var m = maskTex.GetPixelBilinear(u, v);            // png row 0 = north; GetPixelBilinear v=0 = bottom = south  ✓
                float forest = m.r, olive = m.g, desert = m.b;
                // patches: two noise scales break the ground into grass, dry earth and stony areas
                float n1 = Mathf.PerlinNoise(wx * 0.006f + 31f, wz * 0.006f + 7f), n2 = Mathf.PerlinNoise(wx * 0.03f + 3f, wz * 0.03f + 11f);
                float north = Mathf.Clamp01((wz + L * 0.1f) / (L * 0.6f));                                  // the north is greener
                float sand = Mathf.Clamp01(desert * 1.6f) * (0.7f + 0.3f * n2) + Mathf.Clamp01(1f - (hgt - SEA) / 3f) * 2f;
                float rock = Mathf.Clamp01((steep - 16f) / 12f) * 1.6f + Mathf.Clamp01((hgt - 150f) / 60f) * 0.6f;
                float grass = (Mathf.Clamp01(forest * 1.6f + olive * 0.9f) + Mathf.Clamp01(n1 * 1.3f - 0.35f) * (0.4f + north)) * (1 - Mathf.Clamp01(desert * 1.4f));
                float dirt = 0.25f + Mathf.Clamp01(0.9f - n1) * 0.6f + n2 * 0.2f;
                float pink = 0f;
                foreach (var rg in new[] { petra, musa }) if (rg != null) { float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(rg.x, rg.z)); pink = Mathf.Max(pink, Mathf.Clamp01(1f - d / (rg.radius * 2.2f))); }
                rock *= 1 - pink; float rose = pink * Mathf.Clamp01(0.4f + steep / 30f);
                float tot = grass + dirt + sand + rock + rose;
                al[y, x, 0] = grass / tot; al[y, x, 1] = dirt / tot; al[y, x, 2] = sand / tot; al[y, x, 3] = rock / tot; al[y, x, 4] = 0; al[y, x, 5] = rose / tot;
            }
            // paint the asphalt
            foreach (var (pts, wdt, type) in roadPaths)
                foreach (var p in pts)
                {
                    int cx = Mathf.RoundToInt((p.x / W + 0.5f) * (A - 1)), cy = Mathf.RoundToInt((p.z / L + 0.5f) * (A - 1));
                    float px = W / (A - 1); int rr = Mathf.CeilToInt(wdt / 2 / px) + 1;
                    for (int dy = -rr; dy <= rr; dy++) for (int dx = -rr; dx <= rr; dx++)
                    {
                        int x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= A || y >= A) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * px; if (d > wdt / 2) continue;
                        float k = Mathf.Clamp01((wdt / 2 - d) / 1.2f);
                        for (int l = 0; l < 6; l++) al[y, x, l] *= 1 - k;
                        al[y, x, 4] += k;
                    }
                }
            td.SetAlphamaps(0, 0, al);
            EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
            var tgo = Terrain.CreateTerrainGameObject(td); tgo.name = "Terrain";
            tgo.transform.position = new Vector3(-W / 2, 0, -L / 2);
            var terrain = tgo.GetComponent<Terrain>(); terrain.heightmapPixelError = 6;
            var tmat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.DeleteAsset($"{Root}/Materials/M_LevantTerrain.mat"); AssetDatabase.CreateAsset(tmat, $"{Root}/Materials/M_LevantTerrain.mat");
            terrain.materialTemplate = tmat; terrain.basemapDistance = 300; terrain.drawInstanced = true;

            // ---------- water: the sea + lakes ----------
            var water = new Material(Shader.Find("Universal Render Pipeline/Lit")); water.SetColor("_BaseColor", new Color(0.09f, 0.27f, 0.4f)); water.SetFloat("_Smoothness", 0.93f); water.SetFloat("_Metallic", 0.1f);
            AssetDatabase.DeleteAsset($"{Root}/Materials/M_Sea.mat"); AssetDatabase.CreateAsset(water, $"{Root}/Materials/M_Sea.mat");
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); sea.name = "Sea"; Object.DestroyImmediate(sea.GetComponent<Collider>());
            // open sea to the horizon all around the island (the jet and the sky view see far)
            sea.transform.position = new Vector3(0, SEA, 0); sea.transform.localScale = new Vector3(W / 10f * 6f, 1, L / 10f * 6f); sea.GetComponent<Renderer>().sharedMaterial = water;
            foreach (var rg in meta.regions)
                if (rg.kind == "dead_sea")
                {   // the Dead Sea sits lower than the sea: its own water surface just under the lowest rim
                    float rim = float.MaxValue; for (int a = 0; a < 48; a++) { var o = new Vector2(Mathf.Cos(a * Mathf.PI / 24), Mathf.Sin(a * Mathf.PI / 24)); rim = Mathf.Min(rim, terrain.SampleHeight(new Vector3(rg.x + o.x * rg.radius * 0.8f, 0, rg.z + o.y * rg.radius * 2.0f))); }
                    var ds = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ds.name = "DeadSea"; Object.DestroyImmediate(ds.GetComponent<Collider>());
                    ds.transform.position = new Vector3(rg.x, rim - 1.5f, rg.z); ds.transform.localScale = new Vector3(rg.radius * 1.5f, 0.05f, rg.radius * 4.2f);
                    var dm = new Material(water); dm.SetColor("_BaseColor", new Color(0.18f, 0.45f, 0.5f)); ds.GetComponent<Renderer>().sharedMaterial = dm;
                }

            // Jordan River / Wadi Araba: a narrow winding water channel feeding the Dead Sea.
            var riverRoot = new GameObject("JordanRiver").transform;
            var riverPoints = new List<Vector3>
            {
                new(-540, SEA - 1.0f, 230), new(-560, SEA - 1.2f, 100),
                new(-590, SEA - 1.4f, -20), new(-610, SEA - 1.6f, -160),
                new(-620, SEA - 1.8f, -300), new(-590, SEA - 2.0f, -430),
                new(-550, SEA - 2.2f, -520),
            };
            var riverMesh = Ribbon(riverPoints.ToArray(), 14f, terrain);
            var river = new GameObject("JordanRiverSurface", typeof(MeshFilter), typeof(MeshRenderer));
            river.transform.SetParent(riverRoot, false);
            river.GetComponent<MeshFilter>().sharedMesh = riverMesh;
            river.GetComponent<MeshRenderer>().sharedMaterial = dm = new Material(water) { name = "M_JordanRiver" };
            dm.SetColor("_BaseColor", new Color(0.12f, 0.38f, 0.46f));
            var riverAsset = $"{Root}/Settings/JordanRiver.asset";
            AssetDatabase.DeleteAsset(riverAsset); AssetDatabase.CreateAsset(riverMesh, riverAsset);

            // ---------- road meshes (a clean asphalt strip with lane marks over the painted ground) ----------
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/M_BaseAsphalt.mat");
            var roadsRoot = new GameObject("Roads").transform;
            foreach (var (pts, wdt, type) in roadPaths)
            {
                var mesh = Ribbon(pts, wdt, terrain); var g = new GameObject(type == "highway" ? "Highway" : "Road", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                g.transform.SetParent(roadsRoot, false); g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = asphalt; g.GetComponent<MeshCollider>().sharedMesh = mesh;
                var mp = $"{Root}/Settings/LevantRoad_{roadsRoot.childCount}.asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
            }

            // ---------- markers for the next steps (towns, port, citadel, tunnel...) ----------
            var marks = new GameObject("Regions").transform;
            foreach (var rg in meta.regions)
            {
                var m = new GameObject("Region_" + rg.name); m.transform.SetParent(marks, false);
                m.transform.position = new Vector3(rg.x, terrain.SampleHeight(new Vector3(rg.x, 0, rg.z)), rg.z);
            }
            foreach (var kv in gates) { var g = new GameObject("Gate_" + kv.Key); g.transform.SetParent(marks, false); g.transform.position = new Vector3(kv.Value.x, terrain.SampleHeight(new Vector3(kv.Value.x, 0, kv.Value.y)), kv.Value.y); }

            // ---------- sky + sun + post ----------
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.6f; sun.color = new Color(1f, 0.93f, 0.82f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38, -35, 0); sun.gameObject.AddComponent<UniversalAdditionalLightData>(); RenderSettings.sun = sun;
            var hdr = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/ThirdParty/PolyHaven/sky_partly_cloudy_2k.hdr");
            if (hdr) { var sky = new Material(Shader.Find("Skybox/Cubemap")); sky.SetTexture("_Tex", hdr); sky.SetFloat("_Exposure", 1f); AssetDatabase.DeleteAsset($"{Root}/Materials/M_LevantSky.mat"); AssetDatabase.CreateAsset(sky, $"{Root}/Materials/M_LevantSky.mat"); RenderSettings.skybox = sky; }
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.00032f; RenderSettings.fogColor = new Color(0.78f, 0.76f, 0.72f);
            var post = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Root}/Settings/PostFX.asset");
            if (post) { var v = new GameObject("Post").AddComponent<Volume>(); v.isGlobal = true; v.sharedProfile = post; }
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.farClipPlane = 4000; cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cam.transform.SetPositionAndRotation(new Vector3(0, 900, -900), Quaternion.Euler(50, 0, 0));
            DynamicGI.UpdateEnvironment();

            var path = $"{Root}/Scenes/Levant.unity";
            EditorSceneManager.SaveScene(scene, path);
            // in the build (after Lobby/Play) so the self-test can fly over it; the match still uses Play.unity
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!list.Exists(s => s.path == path)) { list.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = list.ToArray(); }
            AssetDatabase.SaveAssets();
            Debug.Log($"KRYVON: Levant map step 1 ready – {W}x{L} m, {roadPaths.Count} roads, {meta.regions.Length} regions");
        }

        static List<Vector2> Resample(float[][] pts, float step)
        {
            var o = new List<Vector2>(); if (pts == null || pts.Length < 2) return o;
            o.Add(new Vector2(pts[0][0], pts[0][1])); float carry = 0;
            for (int i = 1; i < pts.Length; i++)
            {
                var a = new Vector2(pts[i - 1][0], pts[i - 1][1]); var b = new Vector2(pts[i][0], pts[i][1]); float seg = Vector2.Distance(a, b);
                for (float t = step - carry; t <= seg; t += step) o.Add(Vector2.Lerp(a, b, t / seg));
                carry = (carry + seg) % step;
            }
            o.Add(new Vector2(pts[pts.Length - 1][0], pts[pts.Length - 1][1]));
            return o;
        }

        static Mesh Ribbon(Vector3[] pts, float w, Terrain terrain)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); float run = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                var fwd = (pts[Mathf.Min(i + 1, pts.Length - 1)] - pts[Mathf.Max(i - 1, 0)]); fwd.y = 0; fwd.Normalize();
                var side = Vector3.Cross(Vector3.up, fwd) * (w / 2);
                var l = pts[i] - side; var r = pts[i] + side;
                l.y = Mathf.Max(pts[i].y, terrain.SampleHeight(l)) + 0.06f; r.y = Mathf.Max(pts[i].y, terrain.SampleHeight(r)) + 0.06f;
                if (i > 0) run += Vector3.Distance(pts[i], pts[i - 1]);
                v.Add(l); v.Add(r); uv.Add(new Vector2(0, run / w)); uv.Add(new Vector2(1, run / w));
                if (i > 0) { int b = v.Count - 4; tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
            }
            var m = new Mesh { indexFormat = IndexFormat.UInt32 }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        // JsonUtility can't read jagged arrays / dictionaries: tiny parsers for the two shapes we write
        static List<Road> ParseRoads(string json)
        {
            var list = new List<Road>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"from\":\\s*\"([^\"]*)\",\\s*\"to\":\\s*\"([^\"]*)\",\\s*\"type\":\\s*\"([^\"]*)\",\\s*\"points\":\\s*\\[(.*?)\\]\\s*\\}", System.Text.RegularExpressions.RegexOptions.Singleline))
            {
                var pts = new List<float[]>();
                foreach (System.Text.RegularExpressions.Match p in System.Text.RegularExpressions.Regex.Matches(m.Groups[4].Value, "\\[\\s*(-?[0-9.]+),\\s*(-?[0-9.]+)\\s*\\]"))
                    pts.Add(new[] { float.Parse(p.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(p.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) });
                list.Add(new Road { from = m.Groups[1].Value, to = m.Groups[2].Value, type = m.Groups[3].Value, points = pts.ToArray() });
            }
            return list;
        }
        static Dictionary<string, Vector2> ParseGates(string json)
        {
            var d = new Dictionary<string, Vector2>();
            var g = System.Text.RegularExpressions.Regex.Match(json, "\"gates\":\\s*\\{(.*?)\\}", System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(g.Groups[1].Value, "\"(B\\d)\":\\s*\\[\\s*(-?[0-9.]+),\\s*(-?[0-9.]+)\\s*\\]"))
                d[m.Groups[1].Value] = new Vector2(float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
            return d;
        }
    }
}

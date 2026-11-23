// Real buildings from OpenStreetMap: KRYVON/World/Real Buildings (OpenStreetMap)...
// Pick a place (lat/lon, radius) and a spot in the scene; the tool asks the public Overpass API for every building
// footprint there (no key needed), extrudes each outline to its real height (building:levels × 3.2 m, or height=),
// sits it on the terrain, and merges them into a few static meshes per 64 m cell so a phone draws them in a few batches.
// The result goes under "OSM_<name>" and is saved with the scene; the raw answer is cached in world/osm_<name>.json.
// Map data © OpenStreetMap contributors (ODbL).
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Kryvon.EditorTools
{
    public class KryvonOsmBuildings : EditorWindow
    {
        string place = "amman_downtown"; double lat = 31.9516, lon = 35.9239; float radius = 350f; Vector3 at = Vector3.zero;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        static readonly Preset[] Presets =
        {
            new Preset("amman_downtown", "Amman Downtown", "Amman Downtown", 31.9516, 35.9239, 350),
            new Preset("amman_citadel", "Amman Citadel", "Amman Downtown", 31.9540, 35.9345, 220),
            new Preset("al_salt", "As-Salt", "Al Salt", 32.0392, 35.7272, 350),
            new Preset("jerash", "Jerash", "Joash & Alloun", 32.2803, 35.8916, 450),
            new Preset("petra", "Petra", "Petra", 30.3285, 35.4444, 450),
            new Preset("beirut", "Beirut", "Beirut Coastal", 33.8886, 35.4950, 600),
            new Preset("beirut_port", "Beirut Port", "Beirut Port", 33.9018, 35.5164, 420),
            new Preset("damascus_old", "Old Damascus", "Old Damascus", 33.5138, 36.2926, 500),
            new Preset("damascus_quarter", "Damascus Quarter", "Damascus Quarter", 33.5100, 36.3040, 380),
            new Preset("aleppo", "Aleppo Citadel", "Aleppo Citadel", 36.1997, 37.1625, 550),
            new Preset("old_jerusalem", "Old Jerusalem", "Old Jerusalem", 31.7781, 35.2352, 480),
            new Preset("heliopolis", "Heliopolis / Baalbek", "Heliopolis", 33.9977, 36.2031, 450),
            new Preset("wadi_musa", "Wadi Musa", "Wadi Musa", 30.3218, 35.4793, 420),
            new Preset("qaia", "Queen Alia International Airport", "QAIA", 31.7225, 35.9932, 650),
        };

        [MenuItem("KRYVON/World/Real Buildings (OpenStreetMap)...")]
        static void Open() => GetWindow<KryvonOsmBuildings>("Real Buildings");

        [MenuItem("KRYVON/World/Build Amman real buildings")]
        static void BuildAmman() => BuildPreset("amman_downtown");

        [MenuItem("KRYVON/World/Build Petra real buildings")]
        static void BuildPetra() => BuildPreset("petra");

        [MenuItem("KRYVON/World/Build As-Salt real buildings")]
        static void BuildSalt() => BuildPreset("al_salt");

        [MenuItem("KRYVON/World/Build Jerash real buildings")]
        static void BuildJerash() => BuildPreset("jerash");

        [MenuItem("KRYVON/World/Build Beirut and Damascus real buildings")]
        static void BuildBeirutDamascus()
        {
            foreach (var presetName in new[] { "beirut", "beirut_port", "damascus_old", "damascus_quarter" })
                BuildPreset(presetName);
        }

        [MenuItem("KRYVON/World/Build Jerusalem and Aleppo real buildings")]
        static void BuildJerusalemAleppo()
        {
            foreach (var presetName in new[] { "old_jerusalem", "aleppo", "heliopolis" })
                BuildPreset(presetName);
        }

        [MenuItem("KRYVON/World/Build Queen Alia International Airport")]
        static void BuildQaia() => BuildPreset("qaia");

        [MenuItem("KRYVON/World/Build Levant + real building presets")]
        static void BuildLevantWithRealBuildings()
        {
            KryvonLevant.Build();
            foreach (var preset in Presets)
                BuildPreset(preset.name);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        [MenuItem("KRYVON/World/Build current-priority real cities")]
        static void BuildPriorityCities()
        {
            foreach (var presetName in new[] { "amman_downtown", "al_salt", "jerash", "petra", "beirut", "damascus_old", "old_jerusalem", "aleppo", "heliopolis", "wadi_musa" })
                BuildPreset(presetName);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Building footprints from OpenStreetMap", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Map data © OpenStreetMap contributors — ODbL. بلاط وخامات المباني منفصل ويجب التحقق من ترخيصها.", MessageType.Info);
            foreach (var preset in Presets)
            {
                if (GUILayout.Button(preset.label)) BuildPreset(preset.name);
            }
            place = EditorGUILayout.TextField("Name", place);
            lat = EditorGUILayout.DoubleField("Latitude", lat); lon = EditorGUILayout.DoubleField("Longitude", lon);
            radius = EditorGUILayout.Slider("Radius (m)", radius, 100f, 1000f);
            at = EditorGUILayout.Vector3Field("Place centre at (world)", at);
            if (GUILayout.Button("Use scene view centre")) { var sv = SceneView.lastActiveSceneView; if (sv) at = sv.pivot; }
            EditorGUILayout.HelpBox("Petra 30.3285, 35.4444 · Amman citadel 31.9540, 35.9345 · Jerash 32.2803, 35.8916 · As-Salt 32.0392, 35.7272", MessageType.Info);
            if (GUILayout.Button("Download and build")) Build(place, lat, lon, radius, at);
        }

        static void BuildPreset(string presetName)
        {
            var preset = System.Array.Find(Presets, p => p.name == presetName);
            if (preset.name == null) { Debug.LogError("KRYVON OSM: unknown preset " + presetName); return; }
            var pos = KryvonLevant.RegionPos(preset.region);
            Build(preset.name, preset.lat, preset.lon, preset.radius, new Vector3(pos.x, 0, pos.y));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        readonly struct Preset
        {
            public readonly string name, label, region;
            public readonly double lat, lon;
            public readonly float radius;
            public Preset(string name, string label, string region, double lat, double lon, float radius)
            { this.name = name; this.label = label; this.region = region; this.lat = lat; this.lon = lon; this.radius = radius; }
        }

        /// remote: -executeMethod Kryvon.EditorTools.KryvonOsmBuildings.BuildFromArgs -osm name lat lon radius x z
        public static void BuildFromArgs()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-osm");
            if (i < 0 || i + 6 >= a.Length) { Debug.LogError("KRYVON OSM: -osm name lat lon radius x z"); return; }
            Build(a[i + 1], double.Parse(a[i + 2], IC), double.Parse(a[i + 3], IC), float.Parse(a[i + 4], IC), new Vector3(float.Parse(a[i + 5], IC), 0, float.Parse(a[i + 6], IC)));
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        public static int Build(string name, double lat, double lon, float radius, Vector3 centre)
        {
            var cache = Path.Combine(Application.dataPath, "../../world/osm_" + name + ".json");
            string json;
            if (File.Exists(cache)) json = File.ReadAllText(cache);
            else
            {
                string q = $"[out:json][timeout:60];way[\"building\"](around:{radius.ToString(IC)},{lat.ToString(IC)},{lon.ToString(IC)});out tags geom;";
                using var req = UnityWebRequest.Post("https://overpass-api.de/api/interpreter", new Dictionary<string, string> { { "data", q } });
                req.SetRequestHeader("User-Agent", "KryvonEditor/1.0");
                var op = req.SendWebRequest();
                while (!op.isDone) { if (EditorUtility.DisplayCancelableProgressBar("OpenStreetMap", "Downloading building footprints…", req.downloadProgress)) { req.Abort(); EditorUtility.ClearProgressBar(); return 0; } System.Threading.Thread.Sleep(50); }
                EditorUtility.ClearProgressBar();
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("KRYVON OSM: " + req.error); return 0; }
                json = req.downloadHandler.text; Directory.CreateDirectory(Path.GetDirectoryName(cache)); File.WriteAllText(cache, json);
            }
            var data = JsonUtility.FromJson<OsmResult>(json.Replace("\"building:levels\"", "\"levels\""));
            if (data?.elements == null || data.elements.Length == 0) { Debug.LogWarning("KRYVON OSM: no buildings there"); return 0; }

            var old = GameObject.Find("OSM_" + name); if (old) DestroyImmediate(old);
            var root = new GameObject("OSM_" + name); root.transform.position = centre; Undo.RegisterCreatedObjectUndo(root, "OSM buildings");
            var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Kryvon/Materials/M_CityStone.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/_Kryvon/Materials/M_Crate.mat");
            if (!wall) wall = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.86f, 0.8f, 0.68f) };
            var terrain = Terrain.activeTerrain;
            double mPerLat = 111320.0, mPerLon = 111320.0 * System.Math.Cos(lat * System.Math.PI / 180.0);
            var cells = new Dictionary<Vector2Int, List<CombineInstance>>(); int made = 0;
            foreach (var e in data.elements)
            {
                if (e.geometry == null || e.geometry.Length < 4) continue;
                var pts = new List<Vector2>();
                for (int i = 0; i < e.geometry.Length - 1; i++) pts.Add(new Vector2((float)((e.geometry[i].lon - lon) * mPerLon), (float)((e.geometry[i].lat - lat) * mPerLat)));
                if (Area(pts) < 0) pts.Reverse(); if (Mathf.Abs(Area(pts)) < 12f) continue;
                float h = Height(e.tags);
                Vector2 c2 = Vector2.zero; foreach (var p in pts) c2 += p; c2 /= pts.Count;
                float ground = centre.y; if (terrain) { float lo = float.MaxValue; foreach (var p in pts) lo = Mathf.Min(lo, terrain.SampleHeight(centre + new Vector3(p.x, 0, p.y)) + terrain.transform.position.y); ground = lo; }
                var mesh = Extrude(pts, h, ground - centre.y - 0.5f);
                if (mesh == null) continue;
                var key = new Vector2Int(Mathf.FloorToInt(c2.x / 64f), Mathf.FloorToInt(c2.y / 64f));
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity }); made++;
            }
            var meshDir = "Assets/_Kryvon/Models/OSM"; if (!AssetDatabase.IsValidFolder(meshDir)) AssetDatabase.CreateFolder("Assets/_Kryvon/Models", "OSM");
            foreach (var kv in cells)
            {
                var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, name = $"osm_{name}_{kv.Key.x}_{kv.Key.y}" };
                m.CombineMeshes(kv.Value.ToArray(), true, true); m.RecalculateBounds();
                AssetDatabase.CreateAsset(m, $"{meshDir}/{m.name}.asset");
                var g = new GameObject(m.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); g.transform.SetParent(root.transform, false);
                g.GetComponent<MeshFilter>().sharedMesh = m; g.GetComponent<MeshRenderer>().sharedMaterial = wall; g.GetComponent<MeshCollider>().sharedMesh = m;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"KRYVON OSM: {made} buildings from OpenStreetMap at {lat},{lon} in {cells.Count} merged meshes under {root.name}");
            return made;
        }

        static float Height(OsmTags t)
        {
            if (t != null && !string.IsNullOrEmpty(t.height) && float.TryParse(t.height.Replace("m", "").Trim(), NumberStyles.Float, IC, out var hh)) return Mathf.Clamp(hh, 3f, 80f);
            if (t != null && !string.IsNullOrEmpty(t.levels) && int.TryParse(t.levels, out var lv)) return Mathf.Clamp(lv * 3.2f, 3f, 80f);
            return 3.2f * (1 + (Mathf.Abs((t?.building ?? "").GetHashCode()) % 3));   // Levant default: 1–3 storeys
        }

        static float Area(List<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; a += p[i].x * q.y - q.x * p[i].y; } return a * 0.5f; }

        // walls + flat roof (ear-clipped), UVs in metres so the stone texture keeps its scale
        static Mesh Extrude(List<Vector2> p, float h, float y0)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); float run = 0;
            for (int i = 0; i < p.Count; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Count]; float len = (b - a).magnitude; int s = v.Count;
                v.Add(new Vector3(a.x, y0, a.y)); v.Add(new Vector3(b.x, y0, b.y)); v.Add(new Vector3(b.x, y0 + h, b.y)); v.Add(new Vector3(a.x, y0 + h, a.y));
                uv.Add(new Vector2(run, 0) / 3f); uv.Add(new Vector2(run + len, 0) / 3f); uv.Add(new Vector2(run + len, h) / 3f); uv.Add(new Vector2(run, h) / 3f); run += len;
                tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var roof = EarClip(p); if (roof == null) return null; int r0 = v.Count;
            foreach (var q in p) { v.Add(new Vector3(q.x, y0 + h, q.y)); uv.Add(q / 3f); }
            for (int i = 0; i < roof.Count; i += 3) tri.AddRange(new[] { r0 + roof[i], r0 + roof[i + 2], r0 + roof[i + 1] });
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); return m;
        }

        static List<int> EarClip(List<Vector2> p)
        {
            var idx = new List<int>(); for (int i = 0; i < p.Count; i++) idx.Add(i);
            var res = new List<int>(); int guard = p.Count * p.Count;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool cut = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                    if (Cross(p[b] - p[a], p[c] - p[b]) <= 0) continue;
                    bool inside = false; foreach (var j in idx) if (j != a && j != b && j != c && In(p[j], p[a], p[b], p[c])) { inside = true; break; }
                    if (inside) continue;
                    res.AddRange(new[] { a, b, c }); idx.RemoveAt(i); cut = true; break;
                }
                if (!cut) return null;
            }
            if (idx.Count == 3) res.AddRange(idx);
            return res;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool In(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0;

        [System.Serializable] class OsmResult { public OsmWay[] elements; }
        [System.Serializable] class OsmWay { public long id; public OsmPt[] geometry; public OsmTags tags; }
        [System.Serializable] class OsmPt { public double lat, lon; }
        [System.Serializable] class OsmTags { public string building, height; public string levels; }
    }
}

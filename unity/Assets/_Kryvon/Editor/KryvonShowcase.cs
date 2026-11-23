// Builds a URP showcase street with the Hyper3D models and renders a screenshot.
// Menu: KRYVON/Build Showcase Scene   |   batch: -executeMethod Kryvon.EditorTools.KryvonShowcase.BuildAndCapture
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kryvon.EditorTools
{
    public static class KryvonShowcase
    {
        const string Root = "Assets/_Kryvon";
        const string Models = Root + "/Models";

        [MenuItem("KRYVON/Setup URP Graphics")]
        public static void SetupURP()
        {
            Directory.CreateDirectory(Root + "/Settings");
            var rdPath = Root + "/Settings/KRYVON_Renderer.asset";
            var assetPath = Root + "/Settings/KRYVON_URP.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rdPath);
                // screen-space ambient occlusion
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, rd);
                rd.rendererFeatures.Add(ssao);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long id);
                var so = new SerializedObject(rd);
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
                var sso = new SerializedObject(ssao);
                SetFloat(sso, "m_Settings.Intensity", 1.6f);
                SetFloat(sso, "m_Settings.Radius", 0.6f);
                sso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rd);
            }
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.shadowDistance = 220f;
            asset.shadowCascadeCount = 4;
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            var a = new SerializedObject(asset);
            SetBool(a, "m_SoftShadowsSupported", true);
            SetInt(a, "m_MainLightShadowmapResolution", 4096);
            SetBool(a, "m_AdditionalLightShadowsSupported", true);
            a.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            AssetDatabase.SaveAssets();
        }

        /// a second renderer without SSAO, at index 1 of the pipeline asset (used by the low graphics levels)
        public static void EnsureLiteRenderer()
        {
            var assetPath = Root + "/Settings/KRYVON_URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath); if (asset == null) { SetupURP(); asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath); }
            var litePath = Root + "/Settings/KRYVON_Renderer_Lite.asset";
            var lite = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(litePath);
            if (lite == null) { lite = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(lite, litePath); }
            foreach (var path in new[] { assetPath, Root + "/Settings/KRYVON_URP_Mobile.asset" })
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path); if (a == null) continue;
                var so = new SerializedObject(a); var list = so.FindProperty("m_RendererDataList");
                if (list.arraySize < 2) list.arraySize = 2;
                list.GetArrayElementAtIndex(1).objectReferenceValue = lite;
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(a);
            }
            AssetDatabase.SaveAssets();
        }

        static void SetFloat(SerializedObject o, string p, float v) { var s = o.FindProperty(p); if (s != null) s.floatValue = v; }
        static void SetInt(SerializedObject o, string p, int v) { var s = o.FindProperty(p); if (s != null) s.intValue = v; }
        static void SetBool(SerializedObject o, string p, bool v) { var s = o.FindProperty(p); if (s != null) s.boolValue = v; }

        // ---------- procedural textures (saved as PNG assets) ----------
        static Texture2D SaveTex(string name, int size, System.Func<float, float, Color> f, bool linear = false)
        {
            var path = Root + "/Materials/" + name + ".png";
            if (!File.Exists(path))
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, f(x / (float)size, y / (float)size));
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.sRGBTexture = !linear;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static float Fbm(float x, float y, int oct = 5)
        {
            float v = 0, a = 0.5f, f = 1;
            for (int i = 0; i < oct; i++) { v += a * Mathf.PerlinNoise(x * f + i * 17.3f, y * f + i * 9.1f); f *= 2; a *= 0.5f; }
            return v;
        }
        // tileable noise: sample on a torus
        static float TFbm(float u, float v, float scale)
        {
            float a = u * Mathf.PI * 2, b = v * Mathf.PI * 2;
            return Fbm(Mathf.Cos(a) * scale + 50, Mathf.Sin(a) * scale + Mathf.Cos(b) * scale + 50, 5) * 0.6f + Fbm(Mathf.Sin(b) * scale * 2 + 9, Mathf.Cos(a) * scale * 2 + 3, 3) * 0.4f;
        }

        const string PH = Root + "/ThirdParty/PolyHaven/";
        static Texture2D PHTex(string file, bool normal)
        {
            var path = PH + file;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != want || ti.anisoLevel != 8) { ti.textureType = want; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Repeat; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material PBR(string name, string ph, Texture2D fallback, Color tint, float smooth, Vector2 tiling, float bump = 1f)
        {
            var m = Lit(name, PHTex(ph + "_diff.jpg", false) ?? fallback, tint, smooth, tiling);
            var n = PHTex(ph + "_nor.jpg", true);
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); m.SetTextureScale("_BumpMap", tiling); }
            return m;
        }

        static Material Lit(string name, Texture2D tex, Color tint, float smooth, Vector2 tiling)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth);
            m.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Place(string rel, Vector3 pos, float yaw, float size, char axis, Transform parent)
        {
            var path = Models + "/" + rel + ".glb";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogWarning("KRYVON: missing model " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.rotation = Quaternion.identity; go.transform.position = Vector3.zero; go.transform.localScale = Vector3.one;
            var b = Bounds(go);
            float dim = axis == 'y' ? b.size.y : axis == 'z' ? b.size.z : Mathf.Max(b.size.x, b.size.z);
            float s = dim > 0.0001f ? size / dim : 1;
            go.transform.localScale = Vector3.one * s;
            b = Bounds(go);
            go.transform.position = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            var pivot = new GameObject(Path.GetFileName(rel)); pivot.transform.SetParent(parent, false);
            go.transform.SetParent(pivot.transform, true);
            pivot.transform.position = pos; pivot.transform.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.On; r.receiveShadows = true; }
            return pivot;
        }
        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        static GameObject Box(string n, Vector3 c, Vector3 s, Material m, Transform parent)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; g.transform.SetParent(parent, false);
            g.transform.position = c; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = m; return g;
        }

        [MenuItem("KRYVON/Build Showcase Scene")]
        public static void Build()
        {
            SetupURP();
            ShaderUtil.allowAsyncCompilation = false;
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ----- textures & materials -----
            var sand = SaveTex("T_Sand", 512, (u, v) => { float n = TFbm(u, v, 3f), g = TFbm(u + .37f, v + .11f, 14f); var c = Color.Lerp(new Color(.62f, .52f, .38f), new Color(.80f, .70f, .53f), n); return c * (0.86f + g * 0.24f); });
            var asphalt = SaveTex("T_Asphalt", 512, (u, v) => { float n = TFbm(u, v, 20f), s = Random.value; float k = .17f + n * .09f + (s > .985f ? .12f : 0); return new Color(k, k, k * 1.02f); });
            var pave = SaveTex("T_Pavement", 512, (u, v) => { float n = TFbm(u, v, 9f); bool joint = (u * 4 % 1) < .03f || (v * 4 % 1) < .03f; float k = joint ? .42f : .66f + n * .12f; return new Color(k, k * .97f, k * .92f); });
            var line = SaveTex("T_RoadLine", 128, (u, v) => { bool dash = v % 1 < .55f; float k = dash ? .92f : .2f; return new Color(k, k, k * .9f); });
            var mGround = PBR("M_Sand", "dry_ground_01", sand, new Color(1f, .93f, .82f), .05f, new Vector2(150, 150), 1.2f);
            var mRoad = PBR("M_Asphalt", "asphalt_02", asphalt, new Color(.85f, .85f, .85f), .18f, new Vector2(60, 2.5f));
            var mPave = PBR("M_Pavement", "cobblestone_floor_08", pave, new Color(1f, .96f, .9f), .1f, new Vector2(80, 1), 1.4f);
            var mLine = Lit("M_RoadLine", line, Color.white, .3f, new Vector2(1, 60));
            var mCurb = Lit("M_Curb", pave, new Color(.8f, .78f, .74f), .1f, new Vector2(1, 60));

            var world = new GameObject("World").transform;
            // ground + street running along X
            Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(600, 1, 600), mGround, world);
            Box("Road", new Vector3(0, 0.01f, 0), new Vector3(240, 0.02f, 10), mRoad, world).transform.rotation = Quaternion.identity;
            var rl = Box("RoadLine", new Vector3(0, 0.025f, 0), new Vector3(240, 0.01f, 0.18f), mLine, world);
            foreach (var z in new[] { 6.5f, -6.5f })
            {
                Box("Sidewalk", new Vector3(0, 0.09f, z), new Vector3(240, 0.18f, 3f), mPave, world);
                Box("Curb", new Vector3(0, 0.1f, z - Mathf.Sign(z) * 1.55f), new Vector3(240, 0.2f, 0.25f), mCurb, world);
            }
            // fix tiling direction for long boxes (UVs follow the cube faces)
            mLine.SetTextureScale("_BaseMap", new Vector2(40, 1));

            // ----- buildings (front faces +Z in the source, so turn 180° to face the street) -----
            var bld = new GameObject("Buildings").transform;
            string[] north = { "h_tower", "h_apartment", "h_shops", "h_oldstone", "h_apartment", "h_mosque", "h_villa", "h_apartment" };
            float[] hts = { 20, 13, 8, 8, 13, 17, 9, 13 };
            float x = -62;
            for (int i = 0; i < north.Length; i++)
            {
                var g = Place("Buildings/" + north[i], new Vector3(x, 0, 16), 180, hts[i], 'y', bld);
                if (g == null) { g = Place("Buildings/h_apartment", new Vector3(x, 0, 16), 180, 13, 'y', bld); }
                float w = g ? Bounds(g).size.x : 12;
                if (g) g.transform.position = new Vector3(x + w / 2, 0, 9 + Bounds(g).size.z / 2);
                x += w + 1.5f;
            }
            x = -60;
            string[] south = { "h_modern", "h_villa", "h_apartment", "h_shops", "h_villa" };
            foreach (var n in south)
            {
                var g = Place("Buildings/" + n, new Vector3(x, 0, -16), 0, n == "h_apartment" ? 13 : 8.5f, 'y', bld);
                if (g == null) g = Place("Buildings/h_villa", new Vector3(x, 0, -16), 0, 9, 'y', bld);
                float w = g ? Bounds(g).size.x : 12;
                if (g) g.transform.position = new Vector3(x + w / 2, 0, -9 - Bounds(g).size.z / 2);
                x += w + 3;
            }

            // ----- parked cars -----
            var cars = new GameObject("Vehicles").transform;
            Place("Vehicles/v_pickup", new Vector3(-14, 0, 3.4f), 90, 5.0f, 'z', cars);
            Place("Vehicles/v_sedan", new Vector3(-6.5f, 0, -3.4f), -90, 4.5f, 'z', cars);
            Place("Vehicles/v_armored", new Vector3(13, 0, 3.2f), 70, 5.3f, 'z', cars);
            Place("Vehicles/v_minibus", new Vector3(22, 0, -3.3f), -90, 5.6f, 'z', cars);
            Place("Vehicles/v_jeep", new Vector3(-22, 0, -3.2f), -100, 3.9f, 'z', cars);
            Place("Vehicles/v_moto", new Vector3(6.5f, 0, 5.2f), 120, 2.1f, 'z', cars);
            Place("Vehicles/v_trike", new Vector3(30, 0, 5.4f), 90, 2.7f, 'z', cars);

            // ----- the squad, facing the camera -----
            var squad = new GameObject("Squad").transform;
            string[] crew = { "recon", "medic", "sara", "hamza", "engineer", "heavy" };
            for (int i = 0; i < crew.Length; i++)
            {
                float cx = (i - 2.5f) * 1.25f, cz = 0.6f + Mathf.Abs(i - 2.5f) * 0.35f;
                Place("Characters/" + crew[i], new Vector3(cx, 0.02f, cz), 180 + (i - 2.5f) * 6, 1.8f, 'y', squad);
            }
            // a couple of weapons on the pavement
            Place("Weapons/w_sniper", new Vector3(-4.4f, 0.22f, -1.4f), 30, 1.25f, 'z', squad);

            // ----- lighting -----
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, 0.86f, 0.68f); sun.intensity = 2.1f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.92f;
            sunGo.transform.rotation = Quaternion.Euler(22, -48, 0);
            sunGo.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = sun;
            Material sky;
            var hdrPath = PH + "sky_partly_cloudy_2k.hdr";
            var hti = AssetImporter.GetAtPath(hdrPath) as TextureImporter;
            if (hti != null)
            {
                if (hti.textureShape != TextureImporterShape.TextureCube) { hti.textureShape = TextureImporterShape.TextureCube; hti.SaveAndReimport(); }
                sky = new Material(Shader.Find("Skybox/Cubemap"));
                sky.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Cubemap>(hdrPath));
                sky.SetFloat("_Exposure", 1.0f); sky.SetFloat("_Rotation", 200f); sky.SetColor("_Tint", new Color(0.52f, 0.5f, 0.48f));
            }
            else
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 1.35f); sky.SetFloat("_Exposure", 1.15f);
            }
            AssetDatabase.DeleteAsset(Root + "/Materials/M_Sky.mat");
            AssetDatabase.CreateAsset(sky, Root + "/Materials/M_Sky.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.004f; RenderSettings.fogColor = new Color(0.82f, 0.74f, 0.62f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();

            // ----- post-processing -----
            var profPath = Root + "/Settings/PostFX.asset";
            AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, profPath);
            T Add<T>() where T : VolumeComponent { var c = prof.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, prof); return c; }
            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>(); bloom.intensity.Override(0.55f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.7f);
            var ca = Add<ColorAdjustments>(); ca.postExposure.Override(-0.15f); ca.contrast.Override(18f); ca.saturation.Override(4f);
            var wb = Add<WhiteBalance>(); wb.temperature.Override(10f);
            var smh = Add<ShadowsMidtonesHighlights>(); smh.shadows.Override(new Vector4(0.95f, 1f, 1.08f, 0f)); smh.highlights.Override(new Vector4(1.06f, 1f, 0.92f, 0f));
            var vig = Add<Vignette>(); vig.intensity.Override(0.24f); vig.smoothness.Override(0.45f);
            var dof = Add<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focusDistance.Override(9.5f); dof.aperture.Override(5.6f); dof.focalLength.Override(55f);
            Add<FilmGrain>().intensity.Override(0.12f);
            EditorUtility.SetDirty(prof);
            var vol = new GameObject("PostFX").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof;

            // ----- camera -----
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 38; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500;
            var cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cd.antialiasingQuality = AntialiasingQuality.High;
            camGo.transform.position = new Vector3(0.6f, 1.25f, -8.6f);
            camGo.transform.LookAt(new Vector3(0, 2.6f, 6));
            camGo.AddComponent<AudioListener>();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Showcase.unity");
            Debug.Log("KRYVON: showcase scene built");
        }

        public static void Capture(string file, int w = 1920, int h = 1080)
        {
            if (Application.isBatchMode) return;   // GPU readback can lose the device in batch builds
            var cam = Camera.main; if (cam == null) { Debug.LogError("KRYVON: no camera"); return; }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            cam.targetTexture = rt;
            for (int i = 0; i < 4; i++) cam.Render(); // warm up (shaders, TAA history, auto exposure)
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt);
            Debug.Log("KRYVON: captured " + file);
        }

        // glTFast picks shaders for the active pipeline at import time: reimport after switching to URP
        public static void ReimportModels()
        {
            SetupURP();
            AssetDatabase.ImportAsset(Models, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        public static void BuildAndCapture()
        {
            Build();
            Capture(Path.Combine(Directory.GetCurrentDirectory(), "Showcase.png"));
            var cam = Camera.main;
            cam.transform.position = new Vector3(-30, 14, -30); cam.transform.LookAt(new Vector3(0, 3, 10)); cam.fieldOfView = 50;
            Capture(Path.Combine(Directory.GetCurrentDirectory(), "Showcase_wide.png"));
            cam.transform.position = new Vector3(0.6f, 1.25f, -8.6f); cam.transform.LookAt(new Vector3(0, 2.6f, 6)); cam.fieldOfView = 38;
        }
    }
}

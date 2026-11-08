// Builds the Lobby scene: a lit stage with the heroes on a turntable, a dusk city backdrop, post-processing,
// and the KryvonLobby UI. Lobby is scene 0 in Build Settings, Play is scene 1.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kryvon.EditorTools
{
    public static class KryvonLobbyBuild
    {
        const string Root = "Assets/_Kryvon";

        [MenuItem("KRYVON/Build Lobby Scene")]
        public static void Build()
        {
            KryvonShowcase.SetupURP();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material M(string n) => AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/{n}.mat");

            // ---- stage ----
            var stageMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            stageMat.SetColor("_BaseColor", new Color(0.09f, 0.09f, 0.1f)); stageMat.SetFloat("_Smoothness", 0.75f); stageMat.SetFloat("_Metallic", 0.6f);
            AssetDatabase.DeleteAsset(Root + "/Materials/M_Stage.mat"); AssetDatabase.CreateAsset(stageMat, Root + "/Materials/M_Stage.mat");
            var ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ringMat.SetColor("_BaseColor", new Color(0.2f, 0.12f, 0.02f)); ringMat.EnableKeyword("_EMISSION"); ringMat.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.18f) * 3.2f);
            AssetDatabase.DeleteAsset(Root + "/Materials/M_StageRing.mat"); AssetDatabase.CreateAsset(ringMat, Root + "/Materials/M_StageRing.mat");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Floor"; floor.transform.localScale = Vector3.one * 30;
            floor.GetComponent<Renderer>().sharedMaterial = M("M_Asphalt") ?? stageMat;
            var podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder); podium.name = "Podium"; podium.transform.position = new Vector3(0, 0.08f, 0); podium.transform.localScale = new Vector3(2.6f, 0.08f, 2.6f);
            podium.GetComponent<Renderer>().sharedMaterial = stageMat;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "Ring"; ring.transform.position = new Vector3(0, 0.07f, 0); ring.transform.localScale = new Vector3(2.75f, 0.06f, 2.75f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;

            // ---- backdrop: the street and buildings, far and soft ----
            var back = new GameObject("Backdrop").transform;
            string[] bld = { "h_tower", "h_apartment", "h_oldstone", "h_mosque", "h_shops", "h_apartment", "h_villa", "h_tower" };
            for (int i = 0; i < bld.Length; i++)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Buildings/{bld[i]}.glb"); if (!src) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, back);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float h = bld[i] == "h_tower" ? 22 : bld[i] == "h_mosque" ? 17 : 12; g.transform.localScale = Vector3.one * (h / b.size.y);
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float x = -46 + i * 13f, z = 26 + (i % 2) * 6;
                g.transform.position += new Vector3(x - b.center.x, -b.min.y, z - b.center.z); g.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            foreach (var (m, p, y) in new[] { ("v_pickup", new Vector3(-6.5f, 0, 7), 35f), ("v_armored", new Vector3(7.5f, 0, 9), -30f) })
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Vehicles/{m}.glb"); if (!src) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, back);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (5f / b.size.z); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += p - new Vector3(b.center.x, b.min.y, b.center.z); g.transform.RotateAround(p, Vector3.up, y);
            }

            // ---- heroes on the turntable ----
            var turn = new GameObject("Turntable").transform; turn.position = new Vector3(0, 0.16f, 0);
            var info = new (string id, string name, string role, string bio)[] {
                ("sara", "SARA", "SNIPER  ·  JORDANIAN SPECIAL FORCES", "Patient, precise – she never needs a second shot."),
                ("hamza", "HAMZA", "ASSAULT  ·  BREACHER", "First through the door, last to leave the fight."),
                ("recon", "RASIDA", "RECON  ·  SCOUT", "Sees you before you see her."),
                ("medic", "HAKEEM", "FIELD MEDIC", "Keeps the squad standing when it matters."),
                ("engineer", "MUHANDISA", "ENGINEER  ·  DRONES", "Turns any building into a fortress."),
                ("heavy", "DIR'", "HEAVY  ·  SUPPORT", "A wall of armour with a machine gun.") };
            var heroes = new System.Collections.Generic.List<KryvonLobby.Hero>();
            foreach (var h in info)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{h.id}.glb"); if (!src) continue;
                var pivot = new GameObject(h.id); pivot.transform.SetParent(turn, false);
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, pivot.transform);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (1.85f / b.size.y); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += new Vector3(turn.position.x - b.center.x, turn.position.y - b.min.y, turn.position.z - b.center.z);
                g.transform.rotation = Quaternion.identity;
                var ctrl = KryvonPlay.BuildController(h.id);
                if (ctrl)
                {
                    var an = g.GetComponentInChildren<Animator>() ?? g.AddComponent<Animator>();
                    an.runtimeAnimatorController = ctrl; an.applyRootMotion = false;
                }
                foreach (var r in rs) { r.shadowCastingMode = ShadowCastingMode.On; }
                heroes.Add(new KryvonLobby.Hero { id = h.id, name = h.name, role = h.role, bio = h.bio, model = pivot });
            }

            // ---- light: warm key, cool rim, soft fill, sunset sky ----
            var sun = new GameObject("Key").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.74f, 0.5f); sun.intensity = 2.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(18, -35, 0); sun.gameObject.AddComponent<UniversalAdditionalLightData>(); RenderSettings.sun = sun;
            var rim = new GameObject("Rim").AddComponent<Light>(); rim.type = LightType.Spot; rim.color = new Color(0.45f, 0.65f, 1f); rim.intensity = 60; rim.range = 14; rim.spotAngle = 40;
            rim.transform.position = new Vector3(1.8f, 3.4f, 3.2f); rim.transform.LookAt(new Vector3(0, 1.2f, 0)); rim.gameObject.AddComponent<UniversalAdditionalLightData>();
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Point; fill.color = new Color(1f, 0.85f, 0.7f); fill.intensity = 4; fill.range = 8;
            fill.transform.position = new Vector3(-2.2f, 1.6f, -3f); fill.gameObject.AddComponent<UniversalAdditionalLightData>();
            var hdr = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/ThirdParty/PolyHaven/sky_partly_cloudy_2k.hdr");
            var sky = new Material(Shader.Find(hdr ? "Skybox/Cubemap" : "Skybox/Procedural"));
            if (hdr) { sky.SetTexture("_Tex", hdr); sky.SetFloat("_Exposure", 0.55f); sky.SetFloat("_Rotation", 120); sky.SetColor("_Tint", new Color(0.62f, 0.45f, 0.36f)); }
            AssetDatabase.DeleteAsset(Root + "/Materials/M_LobbySky.mat"); AssetDatabase.CreateAsset(sky, Root + "/Materials/M_LobbySky.mat");
            RenderSettings.skybox = sky; RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.018f; RenderSettings.fogColor = new Color(0.55f, 0.38f, 0.28f);
            DynamicGI.UpdateEnvironment();

            // ---- post ----
            var profPath = Root + "/Settings/LobbyPost.asset"; AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, profPath);
            T Add<T>() where T : VolumeComponent { var c = prof.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, prof); return c; }
            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bl = Add<Bloom>(); bl.intensity.Override(1.1f); bl.threshold.Override(0.9f); bl.scatter.Override(0.75f);
            var dof = Add<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focusDistance.Override(5.4f); dof.aperture.Override(2.2f); dof.focalLength.Override(70f);
            var ca = Add<ColorAdjustments>(); ca.contrast.Override(20f); ca.saturation.Override(8f); ca.postExposure.Override(0.1f);
            var vg = Add<Vignette>(); vg.intensity.Override(0.35f); vg.smoothness.Override(0.5f);
            Add<FilmGrain>().intensity.Override(0.15f);
            var vol = new GameObject("Post").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof;

            // ---- camera ----
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.fieldOfView = 30;
            cam.transform.position = new Vector3(0, 1.3f, -5.4f); cam.transform.LookAt(new Vector3(0, 1.05f, 0));
            var cd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.gameObject.AddComponent<AudioListener>();

            // ---- UI ----
            var lobby = new GameObject("Lobby").AddComponent<KryvonLobby>();
            var so = new SerializedObject(lobby);
            var hp = so.FindProperty("heroes"); hp.arraySize = heroes.Count;
            for (int i = 0; i < heroes.Count; i++)
            {
                var e = hp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = heroes[i].id; e.FindPropertyRelative("name").stringValue = heroes[i].name;
                e.FindPropertyRelative("role").stringValue = heroes[i].role; e.FindPropertyRelative("bio").stringValue = heroes[i].bio;
                e.FindPropertyRelative("model").objectReferenceValue = heroes[i].model;
            }
            Font F(string f) => AssetDatabase.LoadAssetAtPath<Font>($"{Root}/ThirdParty/Fonts/{f}");
            so.FindProperty("display").objectReferenceValue = F("BebasNeue-Regular.ttf");
            so.FindProperty("body").objectReferenceValue = F("Rajdhani-SemiBold.ttf");
            so.FindProperty("bodyBold").objectReferenceValue = F("Rajdhani-SemiBold.ttf");
            so.FindProperty("turntable").objectReferenceValue = turn;
            so.FindProperty("clickSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactGeneric_light_000.ogg");
            so.FindProperty("ambience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/amb_wind.wav");
            so.ApplyModifiedPropertiesWithoutUndo();

            var path = Root + "/Scenes/Lobby.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true), new EditorBuildSettingsScene(Root + "/Scenes/Play.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KRYVON: lobby scene ready");
        }

        /// enter play mode on the lobby, wait for the splash to finish and screenshot the Game view
        public static void PlayAndShoot()
        {
            EditorSceneManager.OpenScene(Root + "/Scenes/Lobby.unity");
            SessionState.SetBool("kryvon.shoot", true);
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("kryvon.shoot", false)) return;
                SessionState.SetBool("kryvon.shoot", false);
                double t0 = EditorApplication.timeSinceStartup; int n = 0;
                EditorApplication.CallbackFunction tick = null;
                tick = () =>
                {
                    double t = EditorApplication.timeSinceStartup - t0;
                    if (n == 0 && t > 1.2) { ScreenCapture.CaptureScreenshot(Path.Combine(Directory.GetCurrentDirectory(), "Lobby_splash.png")); n++; }
                    if (n == 1 && t > 6.0) { ScreenCapture.CaptureScreenshot(Path.Combine(Directory.GetCurrentDirectory(), "Lobby.png")); n++; }
                    if (n == 2 && t > 7.5) { EditorApplication.update -= tick; EditorApplication.isPlaying = false; }
                };
                EditorApplication.update += tick;
            };
        }
    }
}

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

            // ---- the forward operating base and the old city behind it ----
            KryvonWorld.BuildBase(Vector3.zero);
            var floorShadow = GameObject.CreatePrimitive(PrimitiveType.Plane); floorShadow.name = "Ground"; floorShadow.transform.position = new Vector3(0, -0.3f, 60); floorShadow.transform.localScale = Vector3.one * 60;
            floorShadow.GetComponent<Renderer>().sharedMaterial = M("M_Sand") ?? M("M_BaseConcrete");

            // ---- heroes on the turntable ----
            var turn = new GameObject("Turntable").transform; turn.position = new Vector3(0, 0.02f, -4f);
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

            // ---- the squad: three friends beside the player, with name tags ----
            var anchors = new System.Collections.Generic.List<Transform>();
            var headP = new GameObject("PlayerHead").transform; headP.SetParent(turn, false); headP.localPosition = Vector3.up * 1.95f; anchors.Add(headP);
            foreach (var (id, pos, yaw) in new[] { ("heavy", new Vector3(-2.3f, 0.02f, -2.6f), 165f), ("medic", new Vector3(2.4f, 0.02f, -2.5f), 195f), ("recon", new Vector3(-4.4f, 0.02f, -0.9f), 160f) })
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{id}.glb"); if (!src) continue;
                var pivot = new GameObject("Friend_" + id); pivot.transform.position = pos; pivot.transform.rotation = Quaternion.Euler(0, yaw, 0);
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, pivot.transform);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (1.85f / b.size.y); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
                var ctrl = KryvonPlay.BuildController(id);
                if (ctrl) { var an = g.GetComponentInChildren<Animator>() ?? g.AddComponent<Animator>(); an.runtimeAnimatorController = ctrl; an.applyRootMotion = false; }
                var head = new GameObject("Head").transform; head.SetParent(pivot.transform, false); head.localPosition = Vector3.up * 1.95f; anchors.Add(head);
            }

            // ---- light: warm key, cool rim, soft fill, sunset sky ----
            var sun = new GameObject("Key").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.88f, 0.72f); sun.intensity = 2.6f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32, -40, 0); sun.gameObject.AddComponent<UniversalAdditionalLightData>(); RenderSettings.sun = sun;
            var rim = new GameObject("Rim").AddComponent<Light>(); rim.type = LightType.Spot; rim.color = new Color(0.45f, 0.65f, 1f); rim.intensity = 60; rim.range = 14; rim.spotAngle = 40;
            rim.transform.position = new Vector3(1.8f, 3.4f, 3.2f); rim.transform.LookAt(new Vector3(0, 1.2f, 0)); rim.gameObject.AddComponent<UniversalAdditionalLightData>();
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Point; fill.color = new Color(1f, 0.85f, 0.7f); fill.intensity = 4; fill.range = 8;
            fill.transform.position = new Vector3(-2.2f, 1.6f, -3f); fill.gameObject.AddComponent<UniversalAdditionalLightData>();
            var hdr = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/ThirdParty/PolyHaven/sky_partly_cloudy_2k.hdr");
            var sky = new Material(Shader.Find(hdr ? "Skybox/Cubemap" : "Skybox/Procedural"));
            if (hdr) { sky.SetTexture("_Tex", hdr); sky.SetFloat("_Exposure", 1.0f); sky.SetFloat("_Rotation", 120); sky.SetColor("_Tint", new Color(0.55f, 0.52f, 0.5f)); }
            AssetDatabase.DeleteAsset(Root + "/Materials/M_LobbySky.mat"); AssetDatabase.CreateAsset(sky, Root + "/Materials/M_LobbySky.mat");
            RenderSettings.skybox = sky; RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.0035f; RenderSettings.fogColor = new Color(0.78f, 0.74f, 0.66f);
            DynamicGI.UpdateEnvironment();

            // ---- post ----
            var profPath = Root + "/Settings/LobbyPost.asset"; AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, profPath);
            T Add<T>() where T : VolumeComponent { var c = prof.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, prof); return c; }
            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bl = Add<Bloom>(); bl.intensity.Override(1.1f); bl.threshold.Override(0.9f); bl.scatter.Override(0.75f);
            var dof = Add<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Bokeh); dof.focusDistance.Override(8.5f); dof.aperture.Override(5.6f); dof.focalLength.Override(40f);
            var ca = Add<ColorAdjustments>(); ca.contrast.Override(20f); ca.saturation.Override(8f); ca.postExposure.Override(0.1f);
            var vg = Add<Vignette>(); vg.intensity.Override(0.35f); vg.smoothness.Override(0.5f);
            Add<FilmGrain>().intensity.Override(0.15f);
            var vol = new GameObject("Post").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof;

            // ---- camera ----
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.fieldOfView = 50; cam.farClipPlane = 2000;
            cam.transform.position = new Vector3(-0.6f, 1.75f, -12.5f); cam.transform.LookAt(new Vector3(1.2f, 5.5f, 30f));
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
            var ta = so.FindProperty("tagAnchors"); ta.arraySize = anchors.Count; for (int i = 0; i < anchors.Count; i++) ta.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];
            var tn = so.FindProperty("tagNames"); string[] names = { "HAMZA RAJEH", "ZAIN", "OMAR", "LAYLA" }; tn.arraySize = anchors.Count; for (int i = 0; i < anchors.Count; i++) tn.GetArrayElementAtIndex(i).stringValue = names[i];
            string[] mapFiles = { "m0_damascus", "m1_petra", "m2_jerash", "m3_amman", "m4_outskirts" }, mapTitles = { "Damascus Quarter", "Petra", "Jerash", "Amman Towns", "The Outskirts" };
            var mt = so.FindProperty("mapThumbs"); mt.arraySize = mapFiles.Length; var mn = so.FindProperty("mapNames"); mn.arraySize = mapFiles.Length;
            for (int i = 0; i < mapFiles.Length; i++) { mt.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/UI/Maps/{mapFiles[i]}.png"); mn.GetArrayElementAtIndex(i).stringValue = mapTitles[i]; }
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

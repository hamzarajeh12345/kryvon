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

            // ---- the stage: a dark polished floor, a lit round platform, a camp at dusk behind (soft in the depth of field) ----
            var stageM = new Material(Shader.Find("Universal Render Pipeline/Lit")); stageM.SetColor("_BaseColor", new Color(0.09f, 0.085f, 0.08f)); stageM.SetFloat("_Smoothness", 0.72f); stageM.SetFloat("_Metallic", 0.2f);
            AssetDatabase.DeleteAsset(Root + "/Materials/M_StageFloor.mat"); AssetDatabase.CreateAsset(stageM, Root + "/Materials/M_StageFloor.mat");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Floor"; floor.transform.position = new Vector3(0, 0, 20); floor.transform.localScale = new Vector3(14, 1, 8);
            floor.GetComponent<Renderer>().sharedMaterial = M("M_Sand") ?? stageM;
            var plat = GameObject.CreatePrimitive(PrimitiveType.Cylinder); plat.name = "Platform"; plat.transform.position = new Vector3(0, 0.04f, -4f); plat.transform.localScale = new Vector3(3.4f, 0.04f, 3.4f);
            plat.GetComponent<Renderer>().sharedMaterial = stageM;
            var rimM = new Material(Shader.Find("Universal Render Pipeline/Lit")); rimM.SetColor("_BaseColor", new Color(1f, 0.75f, 0.3f)); rimM.EnableKeyword("_EMISSION"); rimM.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.2f) * 2.2f);
            AssetDatabase.DeleteAsset(Root + "/Materials/M_StageRim.mat"); AssetDatabase.CreateAsset(rimM, Root + "/Materials/M_StageRim.mat");
            var rimRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rimRing.name = "PlatformRim"; Object.DestroyImmediate(rimRing.GetComponent<Collider>());
            rimRing.transform.position = new Vector3(0, 0.02f, -4f); rimRing.transform.localScale = new Vector3(3.55f, 0.02f, 3.55f); rimRing.GetComponent<Renderer>().sharedMaterial = rimM;
            // ---- store showcase, far below the stage: every gun, the parachute, backpack, helmet and a car, each fitted to ~1 m,
            //      seen by a disabled camera the store turns on and renders into its preview (KryvonStorePreview) ----
            var show = new GameObject("Showcase"); show.transform.position = new Vector3(0, -300, 0);
            var showItems = new System.Collections.Generic.List<string>();
            foreach (var g in KryvonWorld.Arsenal) if (!showItems.Contains("Weapons/" + g.model)) showItems.Add("Weapons/" + g.model);
            showItems.AddRange(new[] { "Props/p_chute", "Props/p_bag", "Loot/l_backpack", "Props/p_helmet", "Vehicles/v_pickup" });
            foreach (var rel in showItems)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/{rel}.glb"); if (!src) continue;
                var gi = (GameObject)PrefabUtility.InstantiatePrefab(src); var holder = new GameObject(System.IO.Path.GetFileName(rel)); holder.transform.SetParent(show.transform, false);
                var rs = gi.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) { Object.DestroyImmediate(gi); continue; }
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                gi.transform.localScale = Vector3.one * (1.1f / Mathf.Max(bb.size.x, bb.size.y, bb.size.z));
                bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                gi.transform.SetParent(holder.transform, true); gi.transform.position += show.transform.position - bb.center;
                foreach (var c in gi.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                holder.SetActive(false);
            }
            var scam = new GameObject("ShowCam").AddComponent<Camera>(); scam.transform.SetParent(show.transform, false);
            scam.transform.localPosition = new Vector3(0, 0.25f, -1.75f); scam.transform.LookAt(show.transform.position);
            scam.clearFlags = CameraClearFlags.SolidColor; scam.backgroundColor = new Color(0.07f, 0.075f, 0.09f, 0f); scam.nearClipPlane = 0.05f; scam.farClipPlane = 6f; scam.fieldOfView = 34; scam.enabled = false;
            var sl = new GameObject("ShowKey").AddComponent<Light>(); sl.transform.SetParent(show.transform, false); sl.type = LightType.Point; sl.range = 8; sl.intensity = 6; sl.color = new Color(1f, 0.95f, 0.88f); sl.transform.localPosition = new Vector3(-1.2f, 1.4f, -1.4f);
            var sr = new GameObject("ShowRim").AddComponent<Light>(); sr.transform.SetParent(show.transform, false); sr.type = LightType.Point; sr.range = 8; sr.intensity = 4; sr.color = new Color(1f, 0.75f, 0.4f); sr.transform.localPosition = new Vector3(1.4f, 0.8f, 1.2f);

            var camp = new GameObject("Camp").transform;
            void P(string m, float size, char ax, Vector3 pos, float yaw) => KryvonWorld.Prop("Props/" + m, size, ax, pos, yaw, camp);
            P("p_container", 6.1f, 'z', new Vector3(-12, 0, 16), 80); P("p_container", 6.1f, 'z', new Vector3(-12.3f, 2.6f, 16.2f), 84); P("p_container", 6.1f, 'z', new Vector3(-17, 0, 22), 10);
            P("p_sandbags", 3.2f, 'x', new Vector3(-4.5f, 0, 3.5f), 10); P("p_sandbags", 3.2f, 'x', new Vector3(5, 0, 4), -20);
            P("p_barrels", 1.6f, 'x', new Vector3(6.5f, 0, 1.5f), 30); P("p_barrier", 2.0f, 'x', new Vector3(-6.5f, 0, 0.5f), 70);
            var car = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Vehicles/v_pickup.glb"); if (car) KryvonWorld.Prop("Vehicles/v_pickup", 5.2f, 'z', new Vector3(11, 0, 14), -35, camp);
            foreach (var t in new[] { new Vector3(-16, 0, 22), new Vector3(15, 0, 20), new Vector3(-22, 0, 30), new Vector3(20, 0, 34) }) P("p_cypress", 12f, 'y', t, 0);

            // ---- heroes on the turntable ----
            var turn = new GameObject("Turntable").transform; turn.position = new Vector3(0, 0.085f, -4f);
            var info = new (string id, string name, string role, string bio)[] {
                ("sara", "SARA", "SNIPER  ·  JORDANIAN SPECIAL FORCES", "Patient, precise – she never needs a second shot."),
                ("hamza", "HAMZA", "ASSAULT  ·  BREACHER", "First through the door, last to leave the fight."),
                ("mythic", "MYTHIC RUNE", "OUTFIT  ·  LEGENDARY", "Runes of the old citadel, worn into battle."),
                ("recon", "RASIDA", "RECON  ·  SCOUT", "Sees you before you see her."),
                ("medic", "HAKEEM", "FIELD MEDIC", "Keeps the squad standing when it matters."),
                ("engineer", "MUHANDISA", "ENGINEER  ·  DRONES", "Turns any building into a fortress."),
                ("heavy", "DIR'", "HEAVY  ·  SUPPORT", "A wall of armour with a machine gun.") };
            var heroes = new System.Collections.Generic.List<KryvonLobby.Hero>(); RuntimeAnimatorController lobbyCtrl = null;
            foreach (var h in info)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{h.id}.glb"); if (!src) continue;
                var pivot = new GameObject(h.id); pivot.transform.SetParent(turn, false);
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, pivot.transform);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (1.85f / b.size.y); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += new Vector3(turn.position.x - b.center.x, turn.position.y - b.min.y, turn.position.z - b.center.z);
                g.transform.rotation = Quaternion.identity;
                // humanoid heroes can stand, get ready and dance in the lobby (emote panel); others keep their own clips
                var hav = KryvonHumanoid.Available ? KryvonHumanoid.BuildAvatar(h.id) : null;
                if (hav != null && hav.isValid && hav.isHuman)
                {
                    lobbyCtrl ??= KryvonHumanoid.BuildLobbyController();
                    var an = g.GetComponentInChildren<Animator>() ?? g.AddComponent<Animator>();
                    an.avatar = hav; an.runtimeAnimatorController = lobbyCtrl; an.applyRootMotion = false;
                    KryvonHumanoid.AttachGunAimed(g, an, "w_ar", 0.95f);       // heroes are modelled empty-handed; in the lobby they hold a rifle
                    foreach (var r in rs) { r.shadowCastingMode = ShadowCastingMode.On; }
                    heroes.Add(new KryvonLobby.Hero { id = h.id, name = h.name, role = h.role, bio = h.bio, model = pivot });
                    continue;
                }
                var ctrl = KryvonPlay.BuildController(h.id);
                if (ctrl)
                {
                    var an = g.GetComponentInChildren<Animator>() ?? g.AddComponent<Animator>();
                    an.runtimeAnimatorController = ctrl; an.applyRootMotion = false;
                }
                foreach (var r in rs) { r.shadowCastingMode = ShadowCastingMode.On; }
                heroes.Add(new KryvonLobby.Hero { id = h.id, name = h.name, role = h.role, bio = h.bio, model = pivot });
            }

            // ---- teammates: three stands beside the player, hidden until the lobby turns on DUO / SQUAD ----
            var anchors = new System.Collections.Generic.List<Transform>(); var mates = new System.Collections.Generic.List<GameObject>();
            var headP = new GameObject("PlayerHead").transform; headP.SetParent(turn, false); headP.localPosition = Vector3.up * 1.75f; anchors.Add(headP);
            foreach (var (id, alt, pos, yaw) in new[] { ("recon", "heavy", new Vector3(-1.75f, 0.02f, -2.7f), 172f), ("medic", "sara", new Vector3(1.75f, 0.02f, -2.7f), 188f), ("engineer", "mythic", new Vector3(3.0f, 0.02f, -1.2f), 195f) })
            {
                string use = id; var hav = KryvonHumanoid.Available ? KryvonHumanoid.BuildAvatar(id) : null;
                if (hav == null || !hav.isValid || !hav.isHuman) { use = alt; hav = KryvonHumanoid.Available ? KryvonHumanoid.BuildAvatar(alt) : null; }
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{use}.glb"); if (!src) continue;
                var pivot = new GameObject("Mate_" + use); pivot.transform.position = pos; pivot.transform.rotation = Quaternion.Euler(0, yaw, 0);
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, pivot.transform);
                var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (1.8f / b.size.y); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
                var an = g.GetComponentInChildren<Animator>() ?? g.AddComponent<Animator>(); an.applyRootMotion = false;
                if (hav != null && hav.isValid && hav.isHuman) { lobbyCtrl ??= KryvonHumanoid.BuildLobbyController(); an.avatar = hav; an.runtimeAnimatorController = lobbyCtrl; KryvonHumanoid.AttachGunAimed(g, an, "w_ar", 0.95f); }
                else { var ctrl = KryvonPlay.BuildController(use); if (ctrl) an.runtimeAnimatorController = ctrl; }
                var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ped.name = "Pad"; Object.DestroyImmediate(ped.GetComponent<Collider>()); ped.transform.SetParent(pivot.transform, false);
                ped.transform.localPosition = new Vector3(0, 0.01f, 0); ped.transform.localScale = new Vector3(1.3f, 0.01f, 1.3f); ped.GetComponent<Renderer>().sharedMaterial = rimM;
                var head = new GameObject("Head").transform; head.SetParent(pivot.transform, false); head.localPosition = Vector3.up * 1.75f; anchors.Add(head);
                pivot.SetActive(false); mates.Add(pivot);
            }

            // ---- light: warm key, cool rim, soft fill, sunset sky ----
            var sun = new GameObject("Key").AddComponent<Light>(); sun.type = LightType.Directional; sun.color = new Color(1f, 0.66f, 0.42f); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(14, -48, 0); sun.gameObject.AddComponent<UniversalAdditionalLightData>(); RenderSettings.sun = sun;
            var rim = new GameObject("Rim").AddComponent<Light>(); rim.type = LightType.Spot; rim.color = new Color(1f, 0.72f, 0.4f); rim.intensity = 90; rim.range = 14; rim.spotAngle = 40;
            rim.transform.position = new Vector3(1.8f, 3.6f, -0.5f); rim.transform.LookAt(new Vector3(0, 1.2f, -4f)); rim.gameObject.AddComponent<UniversalAdditionalLightData>();
            var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Point; fill.color = new Color(1f, 0.85f, 0.7f); fill.intensity = 4; fill.range = 8;
            fill.transform.position = new Vector3(-1.5f, 1.9f, -7f); fill.intensity = 6; fill.gameObject.AddComponent<UniversalAdditionalLightData>();
            var hdr = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/ThirdParty/PolyHaven/sky_partly_cloudy_2k.hdr");
            var sky = new Material(Shader.Find(hdr ? "Skybox/Cubemap" : "Skybox/Procedural"));
            if (hdr) { sky.SetTexture("_Tex", hdr); sky.SetFloat("_Exposure", 0.8f); sky.SetFloat("_Rotation", 120); sky.SetColor("_Tint", new Color(0.62f, 0.5f, 0.45f)); }
            AssetDatabase.DeleteAsset(Root + "/Materials/M_LobbySky.mat"); AssetDatabase.CreateAsset(sky, Root + "/Materials/M_LobbySky.mat");
            RenderSettings.skybox = sky; RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientIntensity = 0.65f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.012f; RenderSettings.fogColor = new Color(0.55f, 0.42f, 0.36f);
            DynamicGI.UpdateEnvironment();

            // ---- post ----
            var profPath = Root + "/Settings/LobbyPost.asset"; AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, profPath);
            T Add<T>() where T : VolumeComponent { var c = prof.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, prof); return c; }
            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bl = Add<Bloom>(); bl.intensity.Override(1.7f); bl.threshold.Override(0.85f); bl.scatter.Override(0.8f); bl.tint.Override(new Color(0.85f, 0.7f, 1f));
            var dof = Add<DepthOfField>(); dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(9f); dof.gaussianEnd.Override(28f); dof.gaussianMaxRadius.Override(1f);   // bokeh DOF is too heavy for integrated GPUs
            var ca = Add<ColorAdjustments>(); ca.contrast.Override(24f); ca.saturation.Override(10f); ca.postExposure.Override(0.05f);
            var vg = Add<Vignette>(); vg.intensity.Override(0.42f); vg.smoothness.Override(0.5f);
            Add<FilmGrain>().intensity.Override(0.15f);
            var vol = new GameObject("Post").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = prof; vol.priority = 60;   // the lobby keeps its own look

            // ---- camera ----
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.fieldOfView = 44; cam.farClipPlane = 2000;
            cam.transform.position = new Vector3(0f, 1.45f, -8.3f); cam.transform.LookAt(new Vector3(0f, 1.25f, -4f));
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
            var mp = so.FindProperty("mates"); mp.arraySize = mates.Count; for (int i = 0; i < mates.Count; i++) mp.GetArrayElementAtIndex(i).objectReferenceValue = mates[i];
            var tn = so.FindProperty("tagNames"); string[] names = { "HAMZA RAJEH", "ZAIN", "LAYLA", "OMAR" }; tn.arraySize = anchors.Count; for (int i = 0; i < anchors.Count; i++) tn.GetArrayElementAtIndex(i).stringValue = names[i];
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

        // ---- dark-neon dressing: purple light strips round the helipad, neon masts, light shafts through dust ----
        static void Neon()
        {
            var root = new GameObject("Neon").transform;
            var neonCol = new Color(0.62f, 0.28f, 1f);
            var mPath = Root + "/Materials/M_Neon.mat"; AssetDatabase.DeleteAsset(mPath);
            var neon = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            neon.SetColor("_BaseColor", neonCol * 0.4f); neon.EnableKeyword("_EMISSION"); neon.SetColor("_EmissionColor", neonCol * 6f);
            neon.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; AssetDatabase.CreateAsset(neon, mPath);
            GameObject Bar(Vector3 p, Vector3 size, float yaw)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(root, false); g.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); g.transform.localScale = size;
                var r = g.GetComponent<Renderer>(); r.sharedMaterial = neon; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
            }
            // ring of light strips just outside the yellow helipad circle (centre 0,0,2 radius 11)
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2f; if (Mathf.Sin(a) < -0.55f) continue;          // leave the camera side open
                Bar(new Vector3(Mathf.Cos(a) * 12.4f, 0.05f, 2 + Mathf.Sin(a) * 12.4f), new Vector3(0.14f, 0.05f, 1.7f), -a * Mathf.Rad2Deg);
            }
            // neon masts with real light
            foreach (var p in new[] { new Vector3(-8.5f, 0, 11), new Vector3(8.5f, 0, 11), new Vector3(-12.5f, 0, 2.5f), new Vector3(12.5f, 0, 2.5f) })
            {
                Bar(p + Vector3.up * 1.9f, new Vector3(0.12f, 3.8f, 0.12f), 0);
                Bar(p + Vector3.up * 3.85f, new Vector3(0.6f, 0.08f, 0.08f), 0);
                var l = new GameObject("NeonLight").AddComponent<Light>(); l.transform.SetParent(root, false); l.transform.position = p + Vector3.up * 2.4f;
                l.type = LightType.Point; l.color = neonCol; l.intensity = 7f; l.range = 9f; l.shadows = LightShadows.None;
                l.gameObject.AddComponent<UniversalAdditionalLightData>();
            }
            // purple wash on the ground behind the squad
            var wash = new GameObject("NeonWash").AddComponent<Light>(); wash.transform.SetParent(root, false); wash.transform.position = new Vector3(0, 6, 7);
            wash.transform.LookAt(new Vector3(0, 0, 0)); wash.type = LightType.Spot; wash.spotAngle = 70; wash.range = 16; wash.intensity = 40; wash.color = new Color(0.5f, 0.22f, 1f); wash.shadows = LightShadows.None;
            wash.gameObject.AddComponent<UniversalAdditionalLightData>();

            // light shafts: crossed soft quads, brightest at the top, fading down and to the edges
            var tex = new Texture2D(64, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Shaft" };
            for (int y = 0; y < 256; y++) for (int x = 0; x < 64; x++)
            {
                float u = x / 63f, v = y / 255f; float edge = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2.2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, edge * Mathf.Pow(v, 1.4f) * 0.55f));
            }
            tex.Apply(); var tPath = Root + "/Materials/T_Shaft.asset"; AssetDatabase.DeleteAsset(tPath); AssetDatabase.CreateAsset(tex, tPath);
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Resources/KryvonParticle.mat");
            if (baseMat)
            {
                var sPath = Root + "/Materials/M_Shaft.mat"; AssetDatabase.DeleteAsset(sPath);
                var shaft = new Material(baseMat); shaft.SetTexture("_BaseMap", tex); shaft.SetColor("_BaseColor", new Color(1f, 0.82f, 0.62f, 0.35f)); AssetDatabase.CreateAsset(shaft, sPath);
                var purple = new Material(shaft); purple.SetColor("_BaseColor", new Color(0.66f, 0.4f, 1f, 0.3f)); AssetDatabase.CreateAsset(purple, Root + "/Materials/M_ShaftPurple.mat");
                void Shaft(Vector3 top, Vector3 bottom, float width, Material m)
                {
                    var c = (top + bottom) / 2; var len = Vector3.Distance(top, bottom); var up = (top - bottom).normalized;
                    for (int k = 0; k < 2; k++)
                    {
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
                        q.name = "Shaft"; q.transform.SetParent(root, false); q.transform.position = c;
                        q.transform.rotation = Quaternion.LookRotation(Vector3.Cross(up, k == 0 ? Vector3.right : Vector3.forward).normalized, up) * Quaternion.Euler(0, k * 90, 0);
                        q.transform.localScale = new Vector3(width, len, 1);
                        var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                Shaft(new Vector3(-9, 14, 10), new Vector3(-2.5f, 0, -1), 4.5f, shaft);      // sun beam over the squad (left)
                Shaft(new Vector3(-14, 13, 14), new Vector3(-6, 0, 4), 3.5f, shaft);
                Shaft(new Vector3(7, 12, 12), new Vector3(2.5f, 0, -2.5f), 3f, purple);       // purple beam from the right
            }

            // (a dust particle system here froze Intel GPUs after a few seconds – removed; the light shafts carry the look)
        }
    }
}

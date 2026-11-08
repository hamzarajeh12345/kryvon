// Builds the playable street: the Hyper3D character as the player with skeletal animation, sounds, targets.
// Menu: KRYVON/Build Play Scene   |   remote: Kryvon.EditorTools.KryvonPlay.BuildAndCapture
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kryvon.EditorTools
{
    public static class KryvonPlay
    {
        const string Root = "Assets/_Kryvon";
        const string AnimDir = Root + "/Animations";
        public static string PlayerCharacter = "sara";

        // ---------- animation ----------
        static AnimationClip InPlace(string ch, AnimationClip src, bool loop)
        {
            Directory.CreateDirectory(AnimDir + "/" + ch);
            var path = $"{AnimDir}/{ch}/{src.name}.anim";
            var clip = new AnimationClip { name = src.name, frameRate = src.frameRate };
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, b);
                bool hipsXZ = b.path.EndsWith("Hips") && (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.z");
                if (hipsXZ && curve.length > 0)
                {
                    float v = curve.keys[0].value; curve = AnimationCurve.Constant(curve.keys[0].time, curve.keys[curve.length - 1].time, v);
                }
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, s);
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        public static RuntimeAnimatorController BuildController(string ch)
        {
            var glb = $"{Root}/Models/Characters/{ch}.glb";
            var clips = AssetDatabase.LoadAllAssetsAtPath(glb).OfType<AnimationClip>().ToDictionary(c => c.name);
            if (clips.Count == 0) { Debug.LogWarning("KRYVON: no animations in " + glb); return null; }
            AnimationClip C(string n, bool loop = true) => clips.TryGetValue(n, out var c) ? InPlace(ch, c, loop) : null;
            var idle = C("HumanSpIdle"); var walk = C("HumanSpWalking"); var run = C("HumanSpRun"); var sprint = C("HumanSpTwoCycleSprint") ?? run;
            var crouch = C("HumanSpCrouchedWalking"); var crawl = C("HumanSpLowCrawl"); var jump = C("HumanSpJumping", false); var die = C("HumanSpDying", false);
            var sit = C("HumanSpSitting"); var salute = C("HumanSpSalute");

            var path = $"{AnimDir}/{ch}.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Stance", AnimatorControllerParameterType.Int);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Knocked", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;

            AnimatorState Tree(string name, params (Motion m, float at, float ts)[] kids)
            {
                var st = ctrl.CreateBlendTreeInController(name, out var tree, 0);
                tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
                foreach (var k in kids) if (k.m != null) tree.AddChild(k.m, k.at);
                var ch2 = tree.children;
                for (int i = 0, j = 0; i < kids.Length; i++) if (kids[i].m != null) { ch2[j].timeScale = kids[i].ts; j++; }
                tree.children = ch2;
                return st;
            }
            var loco = Tree("Locomotion", (idle, 0f, 1f), (walk, 1.9f, 1f), (run, 4.8f, 1f), (sprint, 6.4f, 1f));
            var crouchS = Tree("Crouch", (crouch, 0f, 0.02f), (crouch, 2.4f, 1f));
            var proneS = Tree("Prone", (crawl, 0f, 0.02f), (crawl, 1.1f, 1f));
            var knock = Tree("Knocked", (crawl, 0f, 0.02f), (crawl, 0.9f, 0.8f));
            var air = sm.AddState("Air"); air.motion = jump;
            var dead = sm.AddState("Dead"); dead.motion = die;
            var sitS = sm.AddState("Sit"); sitS.motion = sit;
            var sal = sm.AddState("Salute"); sal.motion = salute;
            sm.defaultState = loco;

            AnimatorStateTransition T(AnimatorState a, AnimatorState b, float dur = 0.18f)
            { var t = a.AddTransition(b); t.hasExitTime = false; t.duration = dur; return t; }
            void Stance(AnimatorState from, AnimatorState to, int s) => T(from, to, 0.25f).AddCondition(AnimatorConditionMode.Equals, s, "Stance");
            foreach (var (a, b, s) in new[] { (loco, crouchS, 1), (loco, proneS, 2), (crouchS, loco, 0), (crouchS, proneS, 2), (proneS, loco, 0), (proneS, crouchS, 1) }) Stance(a, b, s);
            foreach (var st in new[] { loco, crouchS }) T(st, air, 0.12f).AddCondition(AnimatorConditionMode.If, 0, "Jump");
            var land = T(air, loco, 0.15f); land.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); land.hasExitTime = true; land.exitTime = 0.35f;
            var d1 = sm.AddAnyStateTransition(dead); d1.AddCondition(AnimatorConditionMode.If, 0, "Dead"); d1.duration = 0.2f; d1.canTransitionToSelf = false;
            var k1 = sm.AddAnyStateTransition(knock); k1.AddCondition(AnimatorConditionMode.If, 0, "Knocked"); k1.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); k1.canTransitionToSelf = false; k1.duration = 0.25f;
            T(knock, loco, 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0, "Knocked");
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static void Animate(GameObject modelRoot, RuntimeAnimatorController rc)
        {
            var an = modelRoot.GetComponentInChildren<Animator>();
            if (an == null) an = modelRoot.transform.GetChild(0).gameObject.AddComponent<Animator>();
            an.runtimeAnimatorController = rc; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        // ---------- scene ----------
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"KRYVON: field '{field}' not on {target.GetType().Name}"); return; }
            switch (value)
            {
                case Object[] arr: p.arraySize = arr.Length; for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i]; break;
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i2: p.intValue = i2; break;
                case bool b: p.boolValue = b; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static AudioClip A(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Audio/{n}.wav");

        static WeaponData Weapon(string file, System.Action<WeaponData> setup)
        {
            var path = $"{Root}/Data/{file}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null) { Directory.CreateDirectory(Root + "/Data"); w = ScriptableObject.CreateInstance<WeaponData>(); setup(w); AssetDatabase.CreateAsset(w, path); }
            return w;
        }

        static void BoxCollide(Transform pivot)
        {
            var rs = pivot.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var bc = pivot.gameObject.AddComponent<BoxCollider>();
            bc.center = pivot.InverseTransformPoint(b.center);
            var s = pivot.InverseTransformVector(b.size); bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 0.96f;
        }

        [MenuItem("KRYVON/Build Play Scene")]
        public static void Build()
        {
            if (A("gun_ar") == null) KryvonAudioGen.GenerateAll();
            KryvonShowcase.Build();                                   // street, lights, sky, post-fx, squad, camera
            KryvonWorld.Dress();                                      // terrain, grass, trees, barriers, wrecks, hangars
            var scene = EditorSceneManager.GetActiveScene();

            foreach (var g in new[] { "Buildings", "Vehicles" })
            {
                var root = GameObject.Find(g); if (root == null) continue;
                foreach (Transform t in root.transform) BoxCollide(t);
            }
            var street = GameObject.Find("Vehicles");
            if (street) foreach (Transform t in street.transform.Cast<Transform>().ToArray())
            {
                var key = t.name; if (!key.StartsWith("v_")) continue;
                var pos = t.position; var yaw = t.eulerAngles.y; Object.DestroyImmediate(t.gameObject);
                KryvonWorld.MakeVehicle(key == "v_trike" ? "v_moto" : key, pos, yaw, street.transform);
            }

            // ---------- sounds on the weapons ----------
            var rifle = Weapon("AlSaqr", w => { w.weaponId = "saqr"; w.displayName = "Al-Saqr"; w.arabicName = "الصقر"; w.weaponClass = WeaponClass.AssaultRifle; w.ammoType = AmmoType.A556; });
            var sniper = Weapon("AlQannas", w =>
            {
                w.weaponId = "qannas"; w.displayName = "Al-Qannas"; w.arabicName = "القنّاص"; w.weaponClass = WeaponClass.Sniper; w.ammoType = AmmoType.A300;
                w.damage = 78; w.headshotMultiplier = 2.5f; w.roundsPerMinute = 48; w.magazineSize = 5; w.reloadSeconds = 3.2f; w.fireModes = new[] { FireMode.Single }; w.range = 750; w.defaultScope = ScopeType.X4;
            });
            rifle.shotSound = A("gun_ar"); rifle.suppressedShotSound = A("gun_suppressed"); rifle.reloadSound = A("reload"); rifle.drySound = A("ui_dry_fire");
            sniper.shotSound = A("gun_sniper"); sniper.suppressedShotSound = A("gun_suppressed"); sniper.reloadSound = A("reload"); sniper.drySound = A("ui_dry_fire");
            EditorUtility.SetDirty(rifle); EditorUtility.SetDirty(sniper);

            // ---------- player ----------
            var spawn = new Vector3(0, 0.05f, -3.5f);
            var player = new GameObject("Player"); player.transform.position = spawn;
            var cc = player.AddComponent<CharacterController>(); cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.35f; cc.slopeLimit = 45f;
            player.AddComponent<PlayerInputReader>();
            player.AddComponent<Health>().teamId = 0;
            var src = player.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0f;
            var weapons = player.AddComponent<WeaponController>();
            var body = player.AddComponent<PlayerController>();
            var wso = new SerializedObject(weapons);
            var slots = wso.FindProperty("slots"); slots.arraySize = 2;
            void Slot(int i, WeaponData d, ScopeType sc, int n) { var e = slots.GetArrayElementAtIndex(i); e.FindPropertyRelative("data").objectReferenceValue = d; e.FindPropertyRelative("scope").enumValueIndex = (int)sc; e.FindPropertyRelative("ammo").intValue = n; }
            Slot(0, rifle, ScopeType.RedDot, 30); Slot(1, sniper, ScopeType.X4, 5);
            var ammo = wso.FindProperty("ammo"); ammo.arraySize = 2;
            ammo.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex = (int)AmmoType.A556; ammo.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 600;
            ammo.GetArrayElementAtIndex(1).FindPropertyRelative("type").enumValueIndex = (int)AmmoType.A300; ammo.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = 60;
            wso.FindProperty("audioSource").objectReferenceValue = src;
            wso.ApplyModifiedPropertiesWithoutUndo();

            // the character model, animated
            var squad = GameObject.Find("Squad");
            // every playable hero is built under the player; KryvonHeroSwitch keeps the one picked in the lobby
            var heroRoots = new System.Collections.Generic.List<GameObject>(); var humanCtrl = KryvonHumanoid.Available ? KryvonHumanoid.BuildController() : null;
            foreach (var hero in KryvonHeroSwitch.Heroes)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{hero}.glb"); if (asset == null) continue;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, player.transform); model.name = hero;
                FitHeight(model, 1.8f);
                var hav = humanCtrl != null ? KryvonHumanoid.BuildAvatar(hero) : null;
                bool bakedGun = hero == "hamza";                       // generated holding his rifle
                if (hav != null)
                {
                    var an = model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();
                    an.avatar = hav; an.runtimeAnimatorController = humanCtrl; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    if (!bakedGun) KryvonHumanoid.AttachGunAimed(model, an, "w_ar", 0.95f);
                }
                else { Animate(model, BuildController(hero)); if (!bakedGun) AttachGun(model, "w_ar", 0.95f); }
                heroRoots.Add(model);
            }
            var sw = player.AddComponent<KryvonHeroSwitch>(); Set(sw, "models", heroRoots.Select(h => (Object)h).ToArray());
            player.AddComponent<PlayerAnimatorBridge>();

            player.tag = "Player";
            KryvonWorld.MakeGrave(player, 8f);
            new GameObject("KillFeed").AddComponent<KryvonKillFeed>();
            var gearC = player.AddComponent<KryvonGear>(); player.AddComponent<KryvonLootLabels>();
            KryvonWorld.SetupTactical(player, A);
            Set(gearC, "useSound", AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactSoft_heavy_000.ogg"));
            Set(gearC, "doneSound", AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactGeneric_light_000.ogg"));
            var hudC = player.AddComponent<KryvonHUD>(); Set(hudC, "font", AssetDatabase.LoadAssetAtPath<Font>($"{Root}/ThirdParty/Fonts/Rajdhani-SemiBold.ttf"));
            BuildZone();
            var drop = player.AddComponent<KryvonAirdrop>();
            Set(drop, "crateTemplate", KryvonWorld.MakeAirdropTemplate(GameObject.Find("Outskirts")?.transform));
            Set(drop, "flareSound", A("gun_pistol")); Set(drop, "planeSound", A("vehicle_engine"));
            var picker = player.AddComponent<KryvonLootPicker>(); Set(picker, "pickupSound", AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactSoft_heavy_000.ogg"));
            var fs = player.AddComponent<KryvonFootsteps>();
            Object[] K(string prefix, string fallback) { var k = Enumerable.Range(0, 5).Select(i => (Object)AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/{prefix}_{i:000}.ogg")).Where(x => x != null).ToArray(); return k.Length > 0 ? k : Enumerable.Range(0, 6).Select(i => (Object)A($"{fallback}_{i}")).ToArray(); }
            Set(fs, "hard", K("footstep_concrete", "step_hard"));
            Set(fs, "dirt", K("footstep_grass", "step_dirt"));
            var flashGo = new GameObject("MuzzleLight"); flashGo.transform.SetParent(player.transform, false); flashGo.transform.localPosition = new Vector3(0.25f, 1.35f, 0.75f);
            var fl = flashGo.AddComponent<Light>(); fl.type = LightType.Point; fl.range = 6; fl.color = new Color(1f, 0.75f, 0.4f); fl.enabled = false;
            var mfx = player.AddComponent<KryvonMuzzleFX>();
            Set(mfx, "flash", fl); Set(mfx, "source", src); var kHard = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactPlate_heavy_000.ogg"); var kBody = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactPunch_heavy_000.ogg");
            Set(mfx, "impactHard", kHard ? kHard : A("impact_concrete")); Set(mfx, "impactBody", kBody ? kBody : A("impact_body"));

            // ---------- the rest of the squad become live targets ----------
            if (squad != null)
            {
                int team = 1;
                foreach (Transform t in squad.transform)
                {
                    if (!t.name.StartsWith("w_") && t.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    {
                        if (t.name == PlayerCharacter) { Object.DestroyImmediate(t.gameObject); break; }
                    }
                }
                foreach (Transform t in squad.transform.Cast<Transform>().ToArray())
                {
                    if (t.name.StartsWith("w_")) continue;
                    var h = t.gameObject.AddComponent<Health>(); h.teamId = team++;
                    var hb = new GameObject("HitBody"); hb.transform.SetParent(t, false);
                    var cap = hb.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0, 0.8f, 0); cap.height = 1.6f; cap.radius = 0.32f;
                    hb.AddComponent<HitBox>().owner = h;
                    var hd = new GameObject("HitHead"); hd.transform.SetParent(t, false); hd.transform.localPosition = new Vector3(0, 1.66f, 0);
                    hd.AddComponent<SphereCollider>().radius = 0.14f;
                    var hh = hd.AddComponent<HitBox>(); hh.owner = h; hh.isHead = true;
                    var rc = BuildController(t.name); if (rc != null) Animate(t.gameObject, rc);
                    KryvonWorld.MakeGrave(t.gameObject, 30f);
                }
            }

            // ---------- camera: gameplay rig, no depth of field ----------
            var cam = Camera.main;
            cam.nearClipPlane = 0.05f; cam.fieldOfView = 70;
            var rig = cam.gameObject.AddComponent<ThirdPersonCamera>();
            Set(rig, "target", player.transform); Set(body, "cameraRig", rig);
            var ovPath = Root + "/Settings/PlayOverrides.asset";
            AssetDatabase.DeleteAsset(ovPath);
            var ov = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(ov, ovPath);
            var dof = ov.Add<DepthOfField>(true); dof.name = "DoF off"; dof.mode.Override(DepthOfFieldMode.Off); AssetDatabase.AddObjectToAsset(dof, ov);
            var v = new GameObject("PlayOverrides").AddComponent<Volume>(); v.isGlobal = true; v.priority = 10; v.sharedProfile = ov;

            // ---------- ambience + HUD ----------
            var amb = new GameObject("Ambience").AddComponent<KryvonAmbience>();
            Set(amb, "beds", new Object[] { A("amb_wind"), A("amb_birds") });
            var hud = new GameObject("HUD").AddComponent<KryvonDebugHUD>();
            Set(hud, "player", player.GetComponent<Health>()); Set(hud, "weapons", weapons); Set(hud, "body", body); Set(hud, "cameraRig", rig);

            var path = Root + "/Scenes/Play.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true), new EditorBuildSettingsScene(Root + "/Scenes/Test.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KRYVON: play scene ready – press Play");
        }

        // a real rifle in the right hand (for characters generated with empty hands); placed in the bind pose, then parented to the hand bone
        public static GameObject AttachGun(GameObject character, string gun, float length)
        {
            var hand = character.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RightHand" || t.name.EndsWith("RightHand"));
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Weapons/{gun}.glb");
            if (hand == null || src == null) { Debug.LogWarning("KRYVON: no RightHand bone or gun model"); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
            g.transform.localScale = Vector3.one;
            var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            g.transform.localScale = Vector3.one * (length / Mathf.Max(0.01f, b.size.z));
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var pivot = new GameObject("Gun_" + gun); g.transform.SetParent(pivot.transform, true);
            // grip sits ~35% from the stock end; model nose is +Z
            g.transform.position -= new Vector3(b.center.x, b.center.y, b.min.z + b.size.z * 0.35f);
            var root = character.transform;
            pivot.transform.position = hand.position + root.forward * 0.02f + root.up * 0.02f;
            pivot.transform.rotation = Quaternion.LookRotation(root.forward, root.up) * Quaternion.Euler(8f, 0, 0);
            pivot.transform.SetParent(hand, true);
            return pivot;
        }

        static void FitHeight(GameObject go, float h)
        {
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float s = h / Mathf.Max(0.01f, b.size.y); go.transform.localScale = Vector3.one * s;
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var p = go.transform.parent; go.transform.position += new Vector3(p.position.x - b.center.x, p.position.y - b.min.y, p.position.z - b.center.z);
        }

        public static void CaptureOutskirts()
        {
            var cam = Camera.main; var dir = Directory.GetCurrentDirectory();
            var ter = Terrain.activeTerrain;
            float G(float x, float z) => ter ? ter.SampleHeight(new Vector3(x, 0, z)) + ter.transform.position.y : 0;
            Vector3 P(float x, float h, float z) => new Vector3(x, G(x, z) + h, z);
            var shots = new[] { (P(-110, 2.2f, -12), P(-150, 1.5f, 10)), (P(120, 2.5f, 25), P(160, 1.5f, 70)),
                                (P(-30, 0.6f, -60), P(-45, 0.3f, -85)), (P(70, 45, -170), new Vector3(0, 0, 0)) };
            float fov = cam.fieldOfView; cam.fieldOfView = 60;
            for (int i = 0; i < shots.Length; i++) { cam.transform.position = shots[i].Item1; cam.transform.LookAt(shots[i].Item2); KryvonShowcase.Capture(Path.Combine(dir, $"World{i + 1}.png")); }
            cam.fieldOfView = fov;
        }

        // Al-Tawq with the match timings: 120 s landing (circle shows at 60 s), then five phases, ~24 min in all.
        static void BuildZone()
        {
            var go = new GameObject("Al-Tawq");
            var zone = go.AddComponent<SafeZone>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(wall.GetComponent<Collider>()); wall.name = "Al-Tawq wall";
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_Cull", 0); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetColor("_BaseColor", new Color(0.25f, 0.5f, 1f, 0.22f));
            AssetDatabase.DeleteAsset(Root + "/Materials/M_Zone.mat"); AssetDatabase.CreateAsset(m, Root + "/Materials/M_Zone.mat");
            wall.GetComponent<Renderer>().sharedMaterial = m; wall.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var so = new SerializedObject(zone);
            so.FindProperty("startRadius").floatValue = 640f; so.FindProperty("landingSeconds").floatValue = 120f;
            so.FindProperty("wallVisual").objectReferenceValue = wall.transform; so.FindProperty("wallHeight").floatValue = 300f;
            var ph = so.FindProperty("phases"); ph.arraySize = 5;
            float[,] p = { { 240, 180, 330, 1 }, { 180, 120, 190, 3 }, { 120, 90, 100, 6 }, { 90, 60, 50, 10 }, { 30, 45, 1, 22 } };   // wait, shrink, radius, % HP per second
            for (int i = 0; i < 5; i++)
            {
                var e = ph.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("waitSeconds").floatValue = p[i, 0]; e.FindPropertyRelative("shrinkSeconds").floatValue = p[i, 1];
                e.FindPropertyRelative("radius").floatValue = p[i, 2]; e.FindPropertyRelative("damagePerSecond").floatValue = p[i, 3];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void BuildAndCapture()
        {
            Build();
            var cam = Camera.main; var p = GameObject.Find("Player").transform;
            cam.transform.position = p.position + new Vector3(0.7f, 1.75f, -3.0f); cam.transform.LookAt(p.position + new Vector3(0.2f, 1.45f, 4f));
            KryvonShowcase.Capture(Path.Combine(Directory.GetCurrentDirectory(), "Play.png"));
            CaptureOutskirts();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}

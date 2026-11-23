// KryvonSetup – builds a ready-to-play test scene the first time the project opens
// (and any time from the menu: KRYVON → Rebuild Test Scene).
// Scene: ground, cover, a small building, the player with all scripts wired, two weapons
// (assault rifle + sniper with a 4x), training dummies at 10–100 m, Al-Tawq and a debug HUD.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kryvon.EditorTools
{
    [InitializeOnLoad]
    public static class KryvonSetup
    {
        const string ScenePath = "Assets/_Kryvon/Scenes/Test.unity";
        const string DataDir = "Assets/_Kryvon/Data";

        static KryvonSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
                else if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene(ScenePath);
            };
        }

        [MenuItem("KRYVON/Rebuild Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            System.IO.Directory.CreateDirectory("Assets/_Kryvon/Scenes");
            System.IO.Directory.CreateDirectory(DataDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.55f, 0.52f, 0.48f);

            // ---------- ground & cover ----------
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.localScale = new Vector3(40f, 1f, 40f);
            Paint(ground, new Color(0.72f, 0.64f, 0.48f));

            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f, r = 18f + (i % 3) * 9f;
                Box($"Cover {i}", new Vector3(Mathf.Cos(a) * r, 0.6f, Mathf.Sin(a) * r), new Vector3(2.5f, 1.2f, 0.6f), new Color(0.55f, 0.5f, 0.42f), a * Mathf.Rad2Deg);
            }
            // a small stone house with a door (walk in, crouch at the window)
            var house = new GameObject("House").transform; house.position = new Vector3(-14f, 0f, 10f);
            Color stone = new Color(0.82f, 0.75f, 0.6f);
            Box("Wall N", new Vector3(0, 1.6f, 4), new Vector3(8, 3.2f, 0.3f), stone, 0, house);
            Box("Wall E", new Vector3(4, 1.6f, 0), new Vector3(0.3f, 3.2f, 8), stone, 0, house);
            Box("Wall W", new Vector3(-4, 1.6f, 0), new Vector3(0.3f, 3.2f, 8), stone, 0, house);
            Box("Wall S-L", new Vector3(-2.6f, 1.6f, -4), new Vector3(2.8f, 3.2f, 0.3f), stone, 0, house);
            Box("Wall S-R", new Vector3(2.6f, 1.6f, -4), new Vector3(2.8f, 3.2f, 0.3f), stone, 0, house);
            Box("Lintel", new Vector3(0, 2.85f, -4), new Vector3(2.4f, 0.7f, 0.3f), stone, 0, house);
            Box("Roof", new Vector3(0, 3.35f, 0), new Vector3(8.4f, 0.3f, 8.4f), new Color(0.6f, 0.52f, 0.4f), 0, house);
            Box("Low ceiling (crouch)", new Vector3(10, 1.55f, 0), new Vector3(4, 0.3f, 4), new Color(0.4f, 0.4f, 0.45f), 0);
            Box("Ramp", new Vector3(14, 0.8f, -12), new Vector3(4, 0.3f, 10), new Color(0.5f, 0.5f, 0.5f), 0).transform.Rotate(-15f, 0f, 0f);

            // ---------- weapons ----------
            var rifle = WeaponAsset("AlSaqr", w =>
            {
                w.weaponId = "saqr"; w.displayName = "M4A1"; w.arabicName = "M4A1"; w.weaponClass = WeaponClass.AssaultRifle; w.ammoType = AmmoType.A556;
            });
            var sniper = WeaponAsset("AlQannas", w =>
            {
                w.weaponId = "qannas"; w.displayName = "L96A1"; w.arabicName = "L96A1"; w.weaponClass = WeaponClass.Sniper; w.ammoType = AmmoType.A300;
                w.damage = 78; w.headshotMultiplier = 2.5f; w.roundsPerMinute = 48; w.magazineSize = 5; w.reloadSeconds = 3.2f; w.tacticalReloadSeconds = 2.8f;
                w.fireModes = new[] { FireMode.Single }; w.range = 750; w.hipSpread = 3.5f; w.shoulderSpread = 2f; w.adsSpread = 0.02f; w.bloomPerShot = 0f;
                w.recoilPattern = new[] { new Vector2(0f, 2.6f) }; w.recoilRecovery = 0.85f; w.defaultScope = ScopeType.X4; w.moveSpeedMultiplier = 0.95f;
                w.allowedScopes = new[] { ScopeType.None, ScopeType.RedDot, ScopeType.X2, ScopeType.X3, ScopeType.X4, ScopeType.X6, ScopeType.X8 };
            });

            // ---------- player ----------
            var player = new GameObject("Player");
            var cc = player.AddComponent<CharacterController>(); cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.4f; cc.slopeLimit = 45f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Body (replace with your model)"; visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0, 0.9f, 0); visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Paint(visual, new Color(0.25f, 0.27f, 0.22f));
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.name = "Facing"; nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0.2f, 1.35f, 0.45f); nose.transform.localScale = new Vector3(0.08f, 0.1f, 0.6f);
            Paint(nose, new Color(0.12f, 0.12f, 0.12f));
            player.AddComponent<PlayerInputReader>();
            player.AddComponent<Health>().teamId = 0;
            var weapons = player.AddComponent<WeaponController>();
            var body = player.AddComponent<PlayerController>();

            var wso = new SerializedObject(weapons);
            var slots = wso.FindProperty("slots"); slots.arraySize = 2;
            SetSlot(slots.GetArrayElementAtIndex(0), rifle, ScopeType.RedDot, 30);
            SetSlot(slots.GetArrayElementAtIndex(1), sniper, ScopeType.X4, 5);
            var ammo = wso.FindProperty("ammo"); ammo.arraySize = 2;
            ammo.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex = (int)AmmoType.A556;
            ammo.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 600;
            ammo.GetArrayElementAtIndex(1).FindPropertyRelative("type").enumValueIndex = (int)AmmoType.A300;
            ammo.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = 60;
            wso.ApplyModifiedPropertiesWithoutUndo();

            // ---------- camera ----------
            var camGo = Camera.main.gameObject;
            Camera.main.nearClipPlane = 0.05f; Camera.main.farClipPlane = 2000f;
            var rig = camGo.AddComponent<ThirdPersonCamera>();
            Set(rig, "target", player.transform);
            Set(body, "cameraRig", rig);

            // ---------- training dummies ----------
            int team = 1;
            foreach (var (dist, side, strafe) in new[] { (10f, -3f, false), (25f, 3f, true), (50f, -4f, false), (75f, 5f, true), (100f, 0f, false) })
                Dummy(new Vector3(side, 0f, dist), team++, strafe);

            // ---------- match + Al-Tawq ----------
            var match = new GameObject("Match");
            var mm = match.AddComponent<MatchManager>();
            Set(mm, "islandSeconds", 3f); Set(mm, "planeFallbackSeconds", 2f);
            var zone = match.AddComponent<SafeZone>();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.name = "Al-Tawq wall";
            var wallMat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.2f, 0.45f, 1f, 0.22f) };
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;
            var zso = new SerializedObject(zone);
            zso.FindProperty("startRadius").floatValue = 160f;
            zso.FindProperty("wallVisual").objectReferenceValue = wall.transform;
            var phases = zso.FindProperty("phases"); phases.arraySize = 4;
            float[,] p = { { 30, 20, 90, 2 }, { 20, 15, 50, 4 }, { 15, 15, 22, 8 }, { 10, 10, 6, 12 } };
            for (int i = 0; i < 4; i++)
            {
                var e = phases.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("waitSeconds").floatValue = p[i, 0]; e.FindPropertyRelative("shrinkSeconds").floatValue = p[i, 1];
                e.FindPropertyRelative("radius").floatValue = p[i, 2]; e.FindPropertyRelative("damagePerSecond").floatValue = p[i, 3];
            }
            zso.ApplyModifiedPropertiesWithoutUndo();

            // ---------- HUD ----------
            var hud = new GameObject("Debug HUD").AddComponent<KryvonDebugHUD>();
            Set(hud, "player", player.GetComponent<Health>()); Set(hud, "weapons", weapons); Set(hud, "body", body); Set(hud, "cameraRig", rig); Set(hud, "zone", zone);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KRYVON: test scene ready – press Play. WASD move, mouse look, Shift sprint, Ctrl walk, C crouch, Z prone, Q/E lean, Alt free look, RMB aim (tap = scope, hold = shoulder), LMB fire, R reload, 1/2 weapons, B fire mode, F1 sensitivity panel.");
        }

        // ---------- helpers ----------
        static GameObject Box(string name, Vector3 pos, Vector3 size, Color c, float yaw, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            if (parent) g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = size; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Paint(g, c);
            return g;
        }

        static void Paint(GameObject g, Color c)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            g.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = c };
        }

        static void Dummy(Vector3 pos, int team, bool strafe)
        {
            var root = new GameObject($"Dummy {pos.z:0}m");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, 180, 0);
            var h = root.AddComponent<Health>(); h.teamId = team;
            h.vestReduction = team % 2 == 0 ? 0.4f : 0f; h.vestDurability = team % 2 == 0 ? 220f : 0f;

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Body"; torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = new Vector3(0, 0.8f, 0); torso.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
            Paint(torso, team % 2 == 0 ? new Color(0.3f, 0.4f, 0.6f) : new Color(0.75f, 0.7f, 0.6f));
            var hb = torso.AddComponent<HitBox>(); hb.owner = h;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head"; head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0, 1.75f, 0); head.transform.localScale = Vector3.one * 0.32f;
            Paint(head, new Color(0.85f, 0.7f, 0.55f));
            var hh = head.AddComponent<HitBox>(); hh.owner = h; hh.isHead = true;

            var tr = root.AddComponent<TargetRespawn>();
            Set(tr, "strafe", strafe);
        }

        static WeaponData WeaponAsset(string file, System.Action<WeaponData> setup)
        {
            string path = $"{DataDir}/{file}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w == null) { w = ScriptableObject.CreateInstance<WeaponData>(); setup(w); AssetDatabase.CreateAsset(w, path); }
            return w;
        }

        static void SetSlot(SerializedProperty slot, WeaponData data, ScopeType scope, int loaded)
        {
            slot.FindPropertyRelative("data").objectReferenceValue = data;
            slot.FindPropertyRelative("scope").enumValueIndex = (int)scope;
            slot.FindPropertyRelative("ammo").intValue = loaded;
        }

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"KRYVON setup: field '{field}' not found on {target.GetType().Name}"); return; }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

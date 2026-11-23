// Mobile builds: Android APK/AAB (Play Store, other Android stores, emulators like BlueStacks) and an iOS Xcode project (App Store).
// Menu: KRYVON/Mobile/...   remote: Kryvon.EditorTools.KryvonMobile.BuildAndroid
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kryvon.EditorTools
{
    public static class KryvonMobile
    {
        const string Root = "Assets/_Kryvon";
        static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Boot (the real loading screen) → Lobby → Play (the match itself is on the Levant map now)
        static string[] Scenes { get { var s = new[] { EnsureBoot(), Root + "/Scenes/Lobby.unity", Root + "/Scenes/Play.unity" }; EditorBuildSettings.scenes = s.Select(p => new EditorBuildSettingsScene(p, true)).ToArray(); return s; } }

        /// the tiny first scene: a black camera and KryvonBoot, which shows the key art and loads the lobby with a real progress bar
        [MenuItem("KRYVON/Mobile/Rebuild Boot Scene")]
        public static string EnsureBoot()
        {
            const string path = Root + "/Scenes/Boot.unity";
            // keep what the earlier build steps made, then start a fresh scene on its own (additive fails over an untitled scene)
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            { var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if (s.isDirty && !string.IsNullOrEmpty(s.path)) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s); }
            var sc = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var cam = new GameObject("Camera").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = 0;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cam.gameObject, sc);
            var boot = new GameObject("Boot", typeof(RectTransform)); boot.AddComponent<KryvonBoot>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boot, sc);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc, path);
            return path;
        }
        public static void BuildLevantWindows() { KryvonResources.Ensure(); KryvonLevant.Build(); BuildWindows(); }

        /// point Unity at the Android SDK / NDK / JDK on this machine (Unity's own module paths win if present)
        public static void ConfigureAndroidTools()
        {
            string sdk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk");
            string ndk = Directory.GetDirectories(Path.Combine(Home, "AndroidUnity"), "android-ndk-r27c*").FirstOrDefault() ?? Path.Combine(Home, "AndroidUnity", "android-ndk-r27c");
            string jdk = Directory.GetDirectories(Path.Combine(Home, "AndroidUnity"), "jdk*").FirstOrDefault() ?? Path.Combine(Home, "AndroidUnity", "jdk");
            var t = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            if (t == null) { Debug.LogError("KRYVON: Android Build Support is not installed"); return; }
            void SetPath(string prop, string useEmbedded, string value)
            {
                if (!Directory.Exists(value)) return;
                t.GetProperty(useEmbedded)?.SetValue(null, false);
                t.GetProperty(prop)?.SetValue(null, value);
                Debug.Log($"KRYVON: {prop} = {value}");
            }
            string pe = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
            if (!Directory.Exists(Path.Combine(pe, "SDK", "platforms"))) SetPath("sdkRootPath", "useEmbeddedSdk", sdk);
            if (!Directory.Exists(Path.Combine(pe, "NDK"))) SetPath("ndkRootPath", "useEmbeddedNdk", ndk);
            if (!Directory.Exists(Path.Combine(pe, "OpenJDK"))) SetPath("jdkRootPath", "useEmbeddedJdk", jdk);
        }

        static void CommonPlayerSettings()
        {
            PlayerSettings.companyName = "KRAYVON"; PlayerSettings.productName = "KRAYVON"; Splash(); Icons();
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.krayvon.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.krayvon.game");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;   // phones and BlueStacks (arm64 translation); add X86_64 for Android Studio emulators
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            MobileQuality();
        }

        /// a lighter URP asset for phones: shorter shadows, no MSAA, 80 % render scale, no SSAO; used by quality level 0 on mobile
        static void MobileQuality()
        {
            var path = Root + "/Settings/KRYVON_URP_Mobile.asset";
            var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (a == null)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rd, Root + "/Settings/KRYVON_Renderer_Mobile.asset");
                a = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(a, path);
            }
            a.shadowDistance = 60f; a.shadowCascadeCount = 2; a.msaaSampleCount = 1; a.renderScale = 0.8f; a.supportsHDR = true;
            EditorUtility.SetDirty(a);
            int was = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false); QualitySettings.renderPipeline = a; QualitySettings.shadowDistance = 60; QualitySettings.lodBias = 0.6f;
            QualitySettings.SetQualityLevel(was, false);
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var def = qs.FindProperty("m_PerPlatformDefaultQuality");
            if (def != null) for (int i = 0; i < def.arraySize; i++) { var e = def.GetArrayElementAtIndex(i); var k = e.FindPropertyRelative("first").stringValue; if (k == "Android" || k == "iPhone") e.FindPropertyRelative("second").intValue = 0; }
            qs.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// batch: rebuild the game and lobby scenes, then the Android APK
        public static void RebuildAllAndroid()
        {
            KryvonPlay.BuildAndCapture();
            KryvonLobbyBuild.Build();
            BuildAndroid();
        }

        public static void RebuildLobbyWindows() { KryvonResources.Ensure(); KryvonLobbyBuild.Build(); BuildWindows(); }
        public static void RebuildAllWindows() { KryvonResources.Ensure(); KryvonPlay.BuildAndCapture(); KryvonLobbyBuild.Build(); BuildWindows(); }

        [MenuItem("KRYVON/Build Windows (play on this PC)")]
        public static void BuildWindows()
        {
            KryvonShowcase.EnsureLiteRenderer();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.companyName = "KRAYVON"; PlayerSettings.productName = "KRAYVON"; Splash(); Icons();
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);   // fast to build
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true; PlayerSettings.visibleInBackground = true;
            Directory.CreateDirectory("Builds/Windows");
            KryvonSlim.Prepare();
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/Windows/KRAYVON.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.CleanBuildCache });
            Report(r, "Builds/Windows/KRAYVON.exe");
        }

        [MenuItem("KRYVON/Mobile/Build Android APK")]
        public static void BuildAndroid() => Android(false);
        [MenuItem("KRYVON/Mobile/Build Android App Bundle (Play Store)")]
        public static void BuildAndroidBundle() => Android(true);

        static void Android(bool bundle)
        {
            KryvonShowcase.EnsureLiteRenderer();
            ConfigureAndroidTools();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            CommonPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = bundle;
            Directory.CreateDirectory("Builds/Android");
            var out_ = bundle ? "Builds/Android/KRAYVON.aab" : "Builds/Android/KRAYVON.apk";
            KryvonSlim.Prepare();
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = out_, target = BuildTarget.Android, options = BuildOptions.CleanBuildCache });
            Report(r, out_);
        }

        [MenuItem("KRYVON/Mobile/Export iOS Xcode Project (App Store)")]
        public static void BuildIOS()
        {
            KryvonShowcase.EnsureLiteRenderer();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            CommonPlayerSettings();
            Directory.CreateDirectory("Builds/iOS");
            KryvonSlim.Prepare();
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = "Builds/iOS", target = BuildTarget.iOS, options = BuildOptions.None });
            Report(r, "Builds/iOS");
        }

        static void Report(BuildReport r, string where)
        {
            var s = r.summary;
            var msg = $"KRYVON build {s.result}: {where}  size {s.totalSize / (1024 * 1024)} MB  time {s.totalTime}  errors {s.totalErrors}";
            Debug.Log(msg);
            File.WriteAllText("Builds/last_build.txt", msg + "\n" + string.Join("\n", r.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error).Select(m => m.content).Take(30)));
        }

        /// launch screen: the KRAYVON key art (Sara and Hamza) instead of the Unity logo
        static void Splash()
        {
            const string path = "Assets/_Kryvon/Art/splash_bg.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp && imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; imp.maxTextureSize = 2048; imp.SaveAndReimport(); }
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            PlayerSettings.SplashScreen.show = true; PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[0];
            PlayerSettings.SplashScreen.background = sp; PlayerSettings.SplashScreen.backgroundPortrait = sp;
            PlayerSettings.SplashScreen.backgroundColor = Color.black; PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.blurBackgroundImage = false; PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        }

        /// app icon on the phone's home screen (and the Windows exe): the KRAYVON key-art icon.
        /// Android: legacy + round = the whole art; adaptive = art inside the 72 dp safe zone over its own edge colour,
        /// so the launcher's mask (circle / squircle) never cuts the logo or the faces. iOS rounds the square itself.
        static void Icons()
        {
            Texture2D T(string f, bool alpha)
            {
                var path = "Assets/_Kryvon/Art/" + f; var imp = AssetImporter.GetAtPath(path) as TextureImporter; if (!imp) return null;
                if (imp.textureType != TextureImporterType.Default || imp.maxTextureSize != 1024 || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None)
                { imp.textureType = TextureImporterType.Default; imp.maxTextureSize = 1024; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false; imp.alphaIsTransparency = alpha; imp.npotScale = TextureImporterNPOTScale.None; imp.SaveAndReimport(); }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var full = T("icon.png", false); var fg = T("icon_fg.png", true); var bg = T("icon_bg.png", false);
            if (!full) { Debug.LogWarning("KRYVON: icon.png missing"); return; }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
            var sa = UnityEditor.Build.NamedBuildTarget.Standalone; var ss = PlayerSettings.GetIconSizes(sa, IconKind.Application);
            PlayerSettings.SetIcons(sa, System.Linq.Enumerable.Repeat(full, ss.Length).ToArray(), IconKind.Application);
            foreach (var nt in new[] { UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.NamedBuildTarget.iOS })
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(nt))
                {
                    var icons = PlayerSettings.GetPlatformIcons(nt, kind);
                    foreach (var ic in icons)
                    {
                        if (ic.maxLayerCount >= 2 && fg && bg) ic.SetTextures(bg, fg);     // Android adaptive: background, foreground
                        else ic.SetTexture(full);
                    }
                    PlayerSettings.SetPlatformIcons(nt, kind, icons);
                }
            Debug.Log("KRYVON: app icons set (Android legacy/round/adaptive, iOS, Windows)");
        }
    }
}

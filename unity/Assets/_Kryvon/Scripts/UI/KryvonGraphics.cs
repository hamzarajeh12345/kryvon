// Graphics settings that really change the picture (saved in PlayerPrefs, applied on every scene load):
//   Quality  Smooth · Balanced · HD · HDR · Ultra HDR  → render resolution, shadow range/resolution/cascades,
//            anti-aliasing (MSAA + SMAA), HDR, ambient occlusion, soft shadows, LOD distance
//   Frame rate 30 · 60 · 90 · 120 (vsync off so the cap holds; the device must support it)
//   Style    Classic · Colorful · Realistic · Soft · Film  → colour grading volume
//   Zone colour for colour-blind players: Normal (blue) · Protanopia (yellow) · Deuteranopia (magenta) · Tritanopia (red)
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public static class KryvonGraphics
    {
        public static readonly string[] Qualities = { "SMOOTH", "BALANCED", "HD", "HDR", "ULTRA HDR" };
        public static readonly int[] FrameRates = { 30, 60, 90, 120 };
        public static readonly string[] Styles = { "CLASSIC", "COLORFUL", "REALISTIC", "SOFT", "FILM" };
        public static readonly string[] ColorBlind = { "NORMAL", "PROTANOPIA", "DEUTERANOPIA", "TRITANOPIA" };
        public static readonly Color[] ZoneColors = { new(0.25f, 0.5f, 1f), new(1f, 0.85f, 0.1f), new(1f, 0.2f, 0.9f), new(1f, 0.2f, 0.15f) };

        public static bool WeakGpu => Application.isMobilePlatform || SystemInfo.graphicsDeviceVendor.ToLower().Contains("intel") || SystemInfo.graphicsMemorySize < 3000;
        // ---- what this device can hold, read from its specs: GPU family, video memory, RAM, cores, screen refresh ----
        static int maxQ = -1;
        public static int MaxQuality
        {
            get
            {
                if (maxQ >= 0) return maxQ;
                string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
                int ram = SystemInfo.systemMemorySize, vram = SystemInfo.graphicsMemorySize, cores = SystemInfo.processorCount;
                int Num(string after) { int i = gpu.IndexOf(after); if (i < 0) return 0; i += after.Length; int n = 0; while (i < gpu.Length && !char.IsDigit(gpu[i])) i++; while (i < gpu.Length && char.IsDigit(gpu[i])) n = n * 10 + (gpu[i++] - '0'); return n; }
                if (Application.isMobilePlatform)
                {
                    int s = (ram >= 4000 ? 1 : 0) + (ram >= 6000 ? 1 : 0) + (ram >= 8000 ? 1 : 0) + (cores >= 8 ? 1 : 0);
                    int cap = 3;                                                                  // phones top out at HDR
                    if (gpu.Contains("adreno")) cap = Num("adreno") >= 730 ? 3 : Num("adreno") >= 640 ? 2 : 1;
                    else if (gpu.Contains("immortalis")) cap = 3;
                    else if (gpu.Contains("mali")) cap = Num("mali-g") >= 710 ? 3 : Num("mali-g") >= 57 ? 2 : 1;
                    else if (gpu.Contains("apple")) cap = 3;
                    else if (gpu.Contains("powervr") || gpu.Contains("xclipse")) cap = gpu.Contains("xclipse") ? 3 : 1;
                    maxQ = Mathf.Clamp(Mathf.Min(s, cap), 0, 3);
                }
                else if (gpu.Contains("intel") && !gpu.Contains("arc")) maxQ = 1;              // integrated graphics
                else maxQ = vram < 3000 ? 1 : vram < 6000 ? 2 : vram < 8000 ? 3 : 4;
                return maxQ;
            }
        }
        /// highest frame-rate option the screen can show (refresh rate)
        public static int MaxFps
        {
            get
            {
                double hz = Screen.currentResolution.refreshRateRatio.value; if (hz < 1) hz = 60;
                int m = 0; for (int i = 0; i < FrameRates.Length; i++) if (FrameRates[i] <= hz + 1) m = i;
                return m;
            }
        }
        public static string DeviceName => SystemInfo.graphicsDeviceName;
        public static int Quality { get => Mathf.Min(PlayerPrefs.GetInt("gfx.quality", Mathf.Min(MaxQuality, 3)), MaxQuality); set { PlayerPrefs.SetInt("gfx.quality", Mathf.Min(value, MaxQuality)); Apply(); } }
        public static int Fps { get => Mathf.Min(PlayerPrefs.GetInt("gfx.fps", Mathf.Min(1, MaxFps)), MaxFps); set { PlayerPrefs.SetInt("gfx.fps", Mathf.Min(value, MaxFps)); Apply(); } }
        public static int Style { get => PlayerPrefs.GetInt("gfx.style", 2); set { PlayerPrefs.SetInt("gfx.style", value); Apply(); } }
        public static int Blind { get => PlayerPrefs.GetInt("gfx.blind", 0); set { PlayerPrefs.SetInt("gfx.blind", value); Apply(); } }
        public static Color ZoneColor => ZoneColors[Mathf.Clamp(Blind, 0, 3)];

        /// texture detail is set once, before anything is loaded: changing it later re-uploads every texture
        /// (freezes weak GPUs). Low levels load textures at half resolution = a quarter of the memory.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Early()
        {
            var args = System.Environment.GetCommandLineArgs();
            int qi = System.Array.IndexOf(args, "-quality"); if (qi >= 0 && qi + 1 < args.Length && int.TryParse(args[qi + 1], out var qv)) PlayerPrefs.SetInt("gfx.quality", qv);
            int q = Mathf.Clamp(Quality, 0, 4);
            QualitySettings.globalTextureMipmapLimit = q <= 1 ? 1 : 0;
            QualitySettings.streamingMipmapsActive = true; QualitySettings.streamingMipmapsMemoryBudget = q <= 1 ? 768 : 2048;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-nogfx") >= 0) return;
            int qi = System.Array.IndexOf(args, "-quality"); if (qi >= 0 && qi + 1 < args.Length && int.TryParse(args[qi + 1], out var qv)) PlayerPrefs.SetInt("gfx.quality", qv);
            SceneManager.sceneLoaded += (_, _) => Apply(); Apply();
        }

        static void SetPrivate(object o, string field, object v) { var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance); if (f != null) f.SetValue(o, v); }

        /// in the jet, free fall and under the canopy the whole map has to be readable: the low presets' short view distance
        /// (450–650 m) and thick fog hid the ground completely from 300 m up. Sky view lifts both until the player lands.
        static bool sky; static float groundFog = -1;
        public static void SkyView(bool on, float height = 300f)
        {
            var cam = Camera.main;
            if (on)
            {
                if (!sky) { groundFog = RenderSettings.fogDensity; sky = true; }
                float far = Mathf.Max(new[] { 450f, 650f, 900f, 1300f, 1800f }[Mathf.Clamp(Quality, 0, 4)], 1500f + height * 2f);
                if (cam && cam.farClipPlane < far) cam.farClipPlane = far;
                // thin the fog with height so the towns stay visible, back to the ground value near the end of the fall
                float k = Mathf.Clamp01((height - 40f) / 200f);
                RenderSettings.fogDensity = Mathf.Lerp(groundFog, Mathf.Min(groundFog, 0.0006f), k);
            }
            else if (sky)
            {
                sky = false; if (groundFog >= 0) RenderSettings.fogDensity = groundFog;
                if (cam) cam.farClipPlane = new[] { 450f, 650f, 900f, 1300f, 1800f }[Mathf.Clamp(Quality, 0, 4)];
            }
        }

        public static void Apply()
        {
            sky = false;
            int q = Mathf.Clamp(Quality, 0, 4);
            // ---- frame rate ----
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRates[Mathf.Clamp(Fps, 0, 3)];

            // ---- render pipeline ----
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a)
            {
                float[] scale = { 0.6f, 0.8f, 1f, 1f, 1.25f };
                float[] shadowDist = { 30, 70, 130, 200, 280 };
                int[] cascades = { 1, 2, 2, 4, 4 }, msaa = { 1, 1, 2, 4, 8 }, shadowRes = { 512, 1024, 2048, 4096, 4096 };
                a.renderScale = scale[q]; a.shadowDistance = shadowDist[q]; a.shadowCascadeCount = cascades[q];
                a.msaaSampleCount = msaa[q]; a.supportsHDR = q >= 3;
                SetPrivate(a, "m_MainLightShadowmapResolution", shadowRes[q]);
                SetPrivate(a, "m_SoftShadowsSupported", q >= 2);
                SetPrivate(a, "m_AdditionalLightShadowsSupported", q >= 3);
                // ambient occlusion only on HD and up: cameras pick the full renderer (0) or the lite one without SSAO (1).
                // (switching the SSAO feature itself at runtime freezes some Intel GPUs)
                liteRenderer = q >= 2 ? 0 : 1;
            }
            QualitySettings.lodBias = new[] { 0.45f, 0.75f, 1.1f, 1.5f, 2.2f }[q];
            QualitySettings.pixelLightCount = q <= 1 ? 1 : 4;
            if (RenderSettings.fog && RenderSettings.fogMode == FogMode.Exponential) RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, new[] { 0.007f, 0.005f, 0.0f, 0.0f, 0.0f }[q]);
            QualitySettings.anisotropicFiltering = q >= 2 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            foreach (var cam in Object.FindObjectsByType<Camera>())
            {
                cam.farClipPlane = new[] { 450f, 650f, 900f, 1300f, 1800f }[q];
                var cd = cam.GetComponent<UniversalAdditionalCameraData>(); if (!cd) continue;
                cd.antialiasing = q <= 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.antialiasingQuality = q >= 3 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
                cd.renderPostProcessing = true; cam.allowHDR = q >= 3;   // toggling post/shadows/AA modes at runtime freezes some Intel GPUs
                // renderer stays fixed: changing renderer or SSAO at runtime freezes some Intel GPUs
            }
            var grass = GameObject.Find("Grass"); if (grass) grass.SetActive(q >= 1);
            foreach (var t in Object.FindObjectsByType<Terrain>()) { t.detailObjectDensity = new[] { 0f, 0.35f, 0.7f, 0.9f, 1f }[q]; t.detailObjectDistance = new[] { 50, 90, 140, 190, 250 }[q]; }

            // ---- colour style ----
            var vol = GetStyleVolume();
            var p = vol.profile;
            var ca = Get<ColorAdjustments>(p); var bl = Get<Bloom>(p); var fg = Get<FilmGrain>(p); var vg = Get<Vignette>(p); var st = Get<SplitToning>(p); var tm = Get<Tonemapping>(p);
            ca.postExposure.Override(0); bl.threshold.Override(0.9f); bl.scatter.Override(0.7f); st.balance.Override(0);    // shared defaults, styles override
            switch (Mathf.Clamp(Style, 0, 4))
            {
                case 0: ca.saturation.Override(0); ca.contrast.Override(5); bl.intensity.Override(0.3f); fg.intensity.Override(0); vg.intensity.Override(0.15f); st.balance.Override(0); st.shadows.Override(Color.grey); st.highlights.Override(Color.grey); tm.mode.Override(TonemappingMode.Neutral); break;
                case 1: ca.saturation.Override(28); ca.contrast.Override(14); bl.intensity.Override(0.6f); fg.intensity.Override(0); vg.intensity.Override(0.18f); st.shadows.Override(new Color(0.45f, 0.5f, 0.6f)); st.highlights.Override(new Color(0.6f, 0.55f, 0.45f)); tm.mode.Override(TonemappingMode.ACES); break;
                // REALISTIC (default): warm desert sun, slightly cool shadows, high contrast so operators stand out, a gentle bloom that
                // only catches the sun on metal, muzzle flashes and fire (threshold above white)
                case 2: ca.saturation.Override(6); ca.contrast.Override(20); ca.postExposure.Override(0.12f); bl.intensity.Override(0.32f); bl.threshold.Override(1.05f); bl.scatter.Override(0.6f); fg.intensity.Override(0.04f); vg.intensity.Override(0.22f);
                    st.shadows.Override(new Color(0.47f, 0.5f, 0.55f)); st.highlights.Override(new Color(0.6f, 0.54f, 0.45f)); st.balance.Override(10); tm.mode.Override(TonemappingMode.ACES); break;
                case 3: ca.saturation.Override(-6); ca.contrast.Override(-8); bl.intensity.Override(1.2f); bl.scatter.Override(0.85f); fg.intensity.Override(0); vg.intensity.Override(0.2f); st.highlights.Override(new Color(0.62f, 0.55f, 0.48f)); st.shadows.Override(new Color(0.52f, 0.5f, 0.5f)); tm.mode.Override(TonemappingMode.ACES); break;
                case 4: ca.saturation.Override(-12); ca.contrast.Override(26); bl.intensity.Override(0.7f); fg.intensity.Override(0.35f); vg.intensity.Override(0.38f); st.shadows.Override(new Color(0.35f, 0.5f, 0.55f)); st.highlights.Override(new Color(0.7f, 0.55f, 0.4f)); st.balance.Override(-10); tm.mode.Override(TonemappingMode.ACES); break;
            }
            ca.postExposure.Override(0f);

            // ---- zone colour ----
            var wall = GameObject.Find("Al-Tawq wall");
            if (wall) { var r = wall.GetComponent<Renderer>(); var c = ZoneColor; c.a = 0.24f; r.material.SetColor("_BaseColor", c); }
        }

        static int liteRenderer;
        static Volume styleVol;
        static Volume GetStyleVolume()
        {
            if (styleVol) return styleVol;
            var g = new GameObject("KRYVON Style"); Object.DontDestroyOnLoad(g);
            styleVol = g.AddComponent<Volume>(); styleVol.isGlobal = true; styleVol.priority = 50;
            styleVol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            return styleVol;
        }
        static T Get<T>(VolumeProfile p) where T : VolumeComponent { if (!p.TryGet<T>(out var c)) c = p.Add<T>(true); c.active = true; return c; }
    }
}

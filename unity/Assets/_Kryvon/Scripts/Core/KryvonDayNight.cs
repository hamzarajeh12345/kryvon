// Time of day in a match: the drop happens in golden sunset light (Petra glowing rose-gold), the sun sinks through dusk,
// and from about the 9th minute the map is in deep night under a cold moon. In the dark:
//   • bots see much less far (unless they are close or you are shooting)
//   • vehicles switch on their headlights (cones you can see enemies in)
//   • NIGHT VISION GOGGLES (new loot, found next to helmets) turn the night green and bright – N or the NVG button
// Command line: -night starts the match already dark, -day keeps the sunset all match.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public class KryvonDayNight : MonoBehaviour
    {
        /// 0 = sunset, 1 = full night
        public static float Darkness { get; private set; }
        /// how far bots can see because of the light (1 = full daylight)
        public static float SightMul => Mathf.Lerp(1f, 0.42f, Darkness);
        public static bool IsNight => Darkness > 0.6f;

        const float DuskStart = 150f, NightAt = 540f;          // seconds after landing
        float t, sunI0; Color sunC0, amb0, fog0; Quaternion sunR0; bool announcedDusk, announcedNight, frozen, forcedNight;
        KryvonDrop drop; Light moon;
        readonly Dictionary<KryvonVehicle, Light[]> heads = new();
        float nextLights;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Run(SceneManager.GetActiveScene()); SceneManager.sceneLoaded += (s, _) => Run(s); }
        static void Run(Scene s)
        {
            Darkness = 0;
            if (s.name != "Play" || FindAnyObjectByType<KryvonDayNight>()) return;
            new GameObject("DayNight").AddComponent<KryvonDayNight>();
        }

        void Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            forcedNight = System.Array.IndexOf(args, "-night") >= 0; frozen = System.Array.IndexOf(args, "-day") >= 0;
            drop = FindAnyObjectByType<KryvonDrop>();
            var sun = RenderSettings.sun;
            if (sun)
            {   // golden hour: low sun, warm light
                sunR0 = Quaternion.Euler(14f, sun.transform.eulerAngles.y, 0f); sun.transform.rotation = sunR0;
                sunC0 = Color.Lerp(sun.color, new Color(1f, 0.72f, 0.45f), 0.6f); sun.color = sunC0; sunI0 = sun.intensity;
            }
            amb0 = RenderSettings.ambientLight; fog0 = RenderSettings.fogColor;
            moon = new GameObject("Moon").AddComponent<Light>(); moon.type = LightType.Directional; moon.shadows = LightShadows.None;
            moon.color = new Color(0.55f, 0.65f, 1f); moon.intensity = 0f; moon.transform.rotation = Quaternion.Euler(55f, (sun ? sun.transform.eulerAngles.y : 0) + 160f, 0);
            SpawnGoggles();
        }

        // night vision goggles lie next to about a third of the helmets on the map
        void SpawnGoggles()
        {
            var rng = new System.Random(SceneManager.GetActiveScene().name.GetHashCode() ^ System.DateTime.Now.Minute);
            foreach (var l in KryvonLoot.All.ToArray())
            {
                if (!l || l.kind != LootKind.Helmet || rng.NextDouble() > 0.35) continue;
                var g = Instantiate(l.gameObject, l.transform.position + new Vector3(0.6f, 0, 0.3f), l.transform.rotation, l.transform.parent);
                var n = g.GetComponent<KryvonLoot>(); n.kind = LootKind.NightVision; n.level = 1; n.rarity = 3; g.name = "Loot_NVG";
                g.transform.localScale = l.transform.localScale * 0.7f;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) { var mpb = new MaterialPropertyBlock(); mpb.SetColor("baseColorFactor", new Color(0.2f, 0.32f, 0.2f)); mpb.SetColor("_BaseColor", new Color(0.2f, 0.32f, 0.2f)); mpb.SetColor("emissiveFactor", new Color(0.1f, 0.9f, 0.2f) * 0.4f); mpb.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 0.2f) * 0.4f); r.SetPropertyBlock(mpb); }
            }
        }

        void Update()
        {
            bool landed = drop == null || drop.S == KryvonDrop.Stage.Landed;
            if (landed && !frozen) t += Time.deltaTime;
            float d = forcedNight ? 1f : frozen ? 0f : Mathf.Clamp01((t - DuskStart) / (NightAt - DuskStart));
            Darkness = Mathf.SmoothStep(0, 1, d);
            var sun = RenderSettings.sun;
            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(14f, -8f, Darkness), sunR0.eulerAngles.y, 0);
                sun.color = Color.Lerp(sunC0, new Color(1f, 0.4f, 0.25f), Mathf.Clamp01(Darkness * 2f));
                sun.intensity = Mathf.Lerp(sunI0, 0f, Mathf.Clamp01(Darkness * 1.3f));
                sun.shadows = Darkness > 0.8f ? LightShadows.None : sun.shadows;
            }
            moon.intensity = Mathf.Lerp(0f, 0.22f, Mathf.Clamp01(Darkness * 1.5f - 0.5f));
            var nightAmb = new Color(0.05f, 0.06f, 0.1f);
            bool nvg = KryvonNightVision.On;
            RenderSettings.ambientLight = nvg ? new Color(0.35f, 0.75f, 0.35f) : Color.Lerp(amb0, nightAmb, Darkness);
            if (RenderSettings.fog) RenderSettings.fogColor = nvg ? new Color(0.08f, 0.25f, 0.08f) : Color.Lerp(RenderSettings.fogColor, Color.Lerp(fog0, new Color(0.03f, 0.04f, 0.07f), Darkness), 0.5f);

            if (!announcedDusk && Darkness > 0.05f && landed) { announcedDusk = true; KryvonKillFeed.Notice("THE SUN IS GOING DOWN  ·  FIND NIGHT VISION"); }
            if (!announcedNight && Darkness > 0.95f && landed) { announcedNight = true; KryvonKillFeed.Notice("NIGHT  ·  ENEMIES SEE LESS  ·  HEADLIGHTS ON"); }
            if (Time.time >= nextLights) { nextLights = Time.time + 0.5f; Headlights(); }
        }

        // two spot lights on every vehicle within 120 m of the camera, only after dusk
        void Headlights()
        {
            var cam = Camera.main; bool on = Darkness > 0.35f;
            foreach (var v in FindObjectsByType<KryvonVehicle>(FindObjectsSortMode.None))
            {
                bool near = on && cam && !v.Wrecked && (v.transform.position - cam.transform.position).sqrMagnitude < 120f * 120f;
                if (!heads.TryGetValue(v, out var ls))
                {
                    if (!near) continue;
                    var b = v.GetComponentInChildren<Renderer>() ? v.GetComponentInChildren<Renderer>().bounds : new Bounds(v.transform.position, Vector3.one * 3);
                    float fwd = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(v.transform.forward.x), Mathf.Abs(v.transform.forward.y), Mathf.Abs(v.transform.forward.z)));
                    ls = new Light[2];
                    for (int i = 0; i < 2; i++)
                    {
                        var l = new GameObject("Headlight").AddComponent<Light>(); l.transform.SetParent(v.transform, false);
                        l.transform.localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 0.8f, fwd / Mathf.Max(0.01f, v.transform.lossyScale.z));
                        l.transform.localRotation = Quaternion.Euler(8f, 0, 0);
                        l.type = LightType.Spot; l.spotAngle = 60f; l.range = 45f; l.intensity = 6f; l.color = new Color(1f, 0.93f, 0.8f); l.shadows = LightShadows.None;
                        ls[i] = l;
                    }
                    heads[v] = ls;
                }
                foreach (var l in ls) if (l) l.enabled = near;
            }
        }
    }

    /// night vision goggles: N (or the NVG button) toggles them once picked up; green, grainy, bright
    public class KryvonNightVision : MonoBehaviour
    {
        public static bool On { get; private set; }
        public static KryvonNightVision Mine;
        bool tap; public void Touch() => tap = true;
        static Texture2D grain;

        public static void Give(GameObject player) { var n = player.GetComponent<KryvonNightVision>() ?? player.AddComponent<KryvonNightVision>(); Mine = n; }
        void OnDestroy() { if (Mine == this) { Mine = null; On = false; } }

        void Update()
        {
            var kb = Keyboard.current;
            if ((kb != null && kb.nKey.wasPressedThisFrame) || tap)
                { On = !On; KryvonKillFeed.Notice(On ? "NIGHT VISION ON" : "NIGHT VISION OFF"); }
            tap = false;
            var h = GetComponent<Health>(); if (h && !h.IsAlive) On = false;
        }

        void OnGUI()
        {
            if (!On) return;
            if (!grain)
            {
                grain = new Texture2D(128, 128) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
                var rng = new System.Random(7);
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                { float n = (float)rng.NextDouble() * 0.25f + (y % 3 == 0 ? 0.12f : 0f); grain.SetPixel(x, y, new Color(0.1f, 0.9f, 0.2f, n)); }
                grain.Apply();
            }
            var full = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            GUI.color = new Color(0.15f, 1f, 0.25f, 0.22f); GUI.DrawTexture(full, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float o = Time.time * 37f % 128f;
            GUI.DrawTextureWithTexCoords(full, grain, new Rect(o / 128f, (o * 0.7f) / 128f, Screen.width / 256f, Screen.height / 256f));
            // dark vignette rim of the goggle tubes
            GUI.color = new Color(0, 0, 0, 0.55f);
            float bw = Screen.width * 0.08f; GUI.DrawTexture(new Rect(0, 0, bw, Screen.height), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(Screen.width - bw, 0, bw, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

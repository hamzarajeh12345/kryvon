// The world around the fight:
//   WEATHER     each match rolls one: clear sky, morning haze, or a sandstorm (orange-brown fog, blowing dust, low sun,
//               and the bots see less far too). Announced when you land.
//   ACOUSTICS   indoors the world gets a room echo (a reverb filter on the listener); gunfire behind walls or far away is
//               muffled (low-pass), so you can tell where a fight is from the sound alone
//   FINISHER    the kill that wins the match runs in slow motion with a gold KRAYVON flash
//   SCOPE GLARE enemy snipers aiming at you flash a sun glint from their scope
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public enum Weather { Clear, Haze, Sandstorm }

    public class KryvonWeather : MonoBehaviour
    {
        public static Weather Now { get; private set; } = Weather.Clear;
        /// how far bots can see in this weather (1 = clear)
        public static float Visibility => Now == Weather.Sandstorm ? 0.5f : Now == Weather.Haze ? 0.78f : 1f;
        ParticleSystem dust; bool announced; KryvonDrop drop; float wDensity = -1; Color wColor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Run(SceneManager.GetActiveScene()); SceneManager.sceneLoaded += (s, _) => Run(s); }
        static void Run(Scene s)
        {
            if (s.name != "Play") { Now = Weather.Clear; return; }
            if (FindAnyObjectByType<KryvonWeather>()) return;
            new GameObject("Weather").AddComponent<KryvonWeather>();
        }

        void Start()
        {
            float r = Random.value;
            var force = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-weather");
            Now = r < 0.55f ? Weather.Clear : r < 0.8f ? Weather.Haze : Weather.Sandstorm;
            if (force >= 0 && force + 1 < System.Environment.GetCommandLineArgs().Length && System.Enum.TryParse(System.Environment.GetCommandLineArgs()[force + 1], true, out Weather forced)) Now = forced;
            drop = FindAnyObjectByType<KryvonDrop>();
            var sun = RenderSettings.sun;
            System.Action keep = () => { wDensity = RenderSettings.fogDensity; wColor = RenderSettings.fogColor; };
            switch (Now)
            {
                case Weather.Haze:
                    RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
                    RenderSettings.fogColor = new Color(0.8f, 0.82f, 0.84f); RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity * 3.5f, 0.0035f);
                    if (sun) sun.intensity *= 0.85f;
                    keep(); break;
                case Weather.Sandstorm:
                    RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
                    RenderSettings.fogColor = new Color(0.74f, 0.56f, 0.36f); RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity * 7f, 0.008f);
                    if (sun) { sun.intensity *= 0.6f; sun.color = Color.Lerp(sun.color, new Color(1f, 0.72f, 0.45f), 0.5f); }
                    RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, new Color(0.62f, 0.48f, 0.32f), 0.4f);
                    dust = KryvonFX.Wind(new Color(0.78f, 0.62f, 0.42f));
                    keep(); break;
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam) return;
            // the weather holds on the ground (the sky view in the jet thins the fog and gives it back afterwards)
            bool air = drop && (drop.S == KryvonDrop.Stage.Plane || drop.S == KryvonDrop.Stage.Freefall || drop.S == KryvonDrop.Stage.Chute);
            if (wDensity > 0 && !air) { RenderSettings.fogDensity = wDensity; RenderSettings.fogColor = wColor; }
            if (dust) { dust.transform.position = cam.transform.position + cam.transform.forward * 6f; dust.gameObject.SetActive(!(drop && drop.S == KryvonDrop.Stage.Plane)); }
            if (!announced && drop && drop.S == KryvonDrop.Stage.Landed && Now != Weather.Clear)
            { announced = true; KryvonKillFeed.Notice(Now == Weather.Sandstorm ? "SANDSTORM  ·  VISIBILITY LOW" : "MORNING HAZE  ·  LONG SHOTS ARE HARDER"); }
        }
    }

    /// operations-room radio: short callouts at the big moments (zone moving, last circles, sector clear)
    public class KryvonRadio : MonoBehaviour
    {
        static KryvonRadio inst; string line; float t0 = -9; GUIStyle st; AudioSource src; static AudioClip chirp;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Hook(); SceneManager.sceneLoaded += (_, _) => Hook(); }
        static void Hook()
        {
            var zone = Object.FindAnyObjectByType<SafeZone>(); if (!zone || inst) return;
            inst = new GameObject("Radio").AddComponent<KryvonRadio>();
            zone.OnShrinkStarted += i => Say(i >= 4 ? "FINAL CIRCLES  ·  HOLD YOUR GROUND" : "COMMAND: THE ZONE IS CLOSING  ·  MOVE NOW");
            zone.OnWaitStarted += i => { if (i > 0) Say("COMMAND: NEW SAFE ZONE MARKED ON YOUR MAP"); };
        }
        public static void Say(string s)
        {
            if (!inst) return; inst.line = s; inst.t0 = Time.unscaledTime;
            if (!chirp)
            {   // a two-tone radio squelch made in code
                int rate = 22050, n = rate / 4; var d = new float[n]; uint seed = 99;
                for (int i = 0; i < n; i++) { float t = i / (float)rate; seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; float noise = (seed / (float)uint.MaxValue) * 2 - 1; d[i] = (t < 0.08f ? Mathf.Sin(2 * Mathf.PI * 1250 * t) * 0.35f : noise * 0.18f * Mathf.Exp(-(t - 0.08f) * 18f)); }
                chirp = AudioClip.Create("radio", n, 1, rate, false); chirp.SetData(d, 0);
            }
            if (!inst.src) { inst.src = inst.gameObject.AddComponent<AudioSource>(); inst.src.spatialBlend = 0; }
            inst.src.PlayOneShot(chirp, 0.7f);
        }
        void OnGUI()
        {
            float t = Time.unscaledTime - t0; if (t > 4f || string.IsNullOrEmpty(line) || KryvonTrailer.HideHud) return;
            st ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.Max(16, Screen.height / 36) };
            float a = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((4f - t) * 2f), w = Screen.width * 0.56f, h = Screen.height * 0.055f, x = (Screen.width - w) / 2f, y = Screen.height * 0.17f;
            GUI.color = new Color(0.071f, 0.071f, 0.071f, 0.78f * a); GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.831f, 0.686f, 0.216f, a); GUI.DrawTexture(new Rect(x, y, 4, h), Texture2D.whiteTexture);
            st.normal.textColor = new Color(1, 1, 1, a); GUI.color = Color.white; GUI.Label(new Rect(x, y, w, h), "◉  " + line, st);
        }
    }

    /// 3-D sound with walls and rooms
    public static class KryvonAudio
    {
        /// plays a one-shot in the world: muffled if a wall is between it and the listener, duller with distance
        public static void PlayAt(AudioClip clip, Vector3 at, float vol = 1f)
        {
            if (!clip) return;
            var g = new GameObject("Sound"); g.transform.position = at;
            var s = g.AddComponent<AudioSource>(); s.clip = clip; s.volume = vol; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 4f; s.maxDistance = 450f; s.dopplerLevel = 0f;
            var lis = Object.FindAnyObjectByType<AudioListener>();
            if (lis)
            {
                var lp = lis.transform.position; float d = Vector3.Distance(lp, at);
                bool wall = Physics.Linecast(at + Vector3.up * 0.3f, lp, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<Health>() && (hit.point - lp).sqrMagnitude > 1f;
                float cut = 22000f;
                if (d > 90f) cut = Mathf.Lerp(9000f, 2200f, Mathf.InverseLerp(90f, 400f, d));         // air takes the highs away
                if (wall) { cut = Mathf.Min(cut, 1100f); s.volume *= 0.65f; }
                if (cut < 21000f) { var f = g.AddComponent<AudioLowPassFilter>(); f.cutoffFrequency = cut; }
            }
            s.Play(); Object.Destroy(g, clip.length + 0.1f);
        }
    }

    /// the listener's room echo: on when the player is inside a building
    public class KryvonAcoustics : MonoBehaviour
    {
        AudioReverbFilter rev; KryvonStealth st; float k;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Hook(); SceneManager.sceneLoaded += (_, _) => Hook(); }
        static void Hook()
        {
            var lis = Object.FindAnyObjectByType<AudioListener>(); if (!lis || lis.GetComponent<KryvonAcoustics>()) return;
            if (!Object.FindAnyObjectByType<PlayerController>()) return;
            lis.gameObject.AddComponent<KryvonAcoustics>();
        }
        void Start() { rev = gameObject.AddComponent<AudioReverbFilter>(); rev.reverbPreset = AudioReverbPreset.Off; st = FindAnyObjectByType<KryvonStealth>(); }
        void Update()
        {
            bool indoors = st && st.Indoors;
            float want = indoors ? 1f : 0f; if (Mathf.Approximately(want, k)) return;
            k = want; rev.reverbPreset = indoors ? AudioReverbPreset.Room : AudioReverbPreset.Off;
        }
    }

    /// the match-winning kill: slow motion and a gold KRAYVON flash
    public class KryvonFinisher : MonoBehaviour
    {
        float t0; GUIStyle big, small;
        public static void Play()
        {
            if (FindAnyObjectByType<KryvonFinisher>()) return;
            new GameObject("Finisher").AddComponent<KryvonFinisher>().t0 = Time.unscaledTime;
            Time.timeScale = 0.2f; Time.fixedDeltaTime = 0.02f * 0.2f;
            CameraShake.Kick(0.5f);
        }
        void Update()
        {
            float t = Time.unscaledTime - t0;
            if (t > 1.6f && Time.timeScale < 1f) { Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 2f); Time.fixedDeltaTime = 0.02f * Time.timeScale; }
            if (t > 3.2f) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; Destroy(gameObject); }
        }
        void OnDestroy() { if (Time.timeScale < 1f) { Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; } }
        void OnGUI()
        {
            float t = Time.unscaledTime - t0;
            big ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.Max(40, Screen.height / 7) };
            small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.Max(18, Screen.height / 26) };
            float a = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((3f - t) * 2f);
            GUI.color = new Color(1, 1, 1, 0.35f * Mathf.Clamp01(1f - t * 2.5f)); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            float s = 1f + 0.08f * Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI);
            var m = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(Screen.width / 2f, Screen.height * 0.4f));
            big.normal.textColor = new Color(0, 0, 0, a * 0.6f); GUI.Label(new Rect(4, Screen.height * 0.32f + 4, Screen.width, Screen.height * 0.16f), "KRAYVON", big);
            big.normal.textColor = new Color(0.831f, 0.686f, 0.216f, a); GUI.Label(new Rect(0, Screen.height * 0.32f, Screen.width, Screen.height * 0.16f), "KRAYVON", big);
            GUI.matrix = m;
            small.normal.textColor = new Color(1, 1, 1, a); GUI.Label(new Rect(0, Screen.height * 0.48f, Screen.width, 50), "FINAL KILL", small);
            GUI.color = Color.white;
        }
    }

    /// a sun glint on the scope of an enemy sniper who is aiming at you (visible from far away)
    public class KryvonScopeGlare : MonoBehaviour
    {
        KryvonBot bot; Transform flare; MeshRenderer mr; static MaterialPropertyBlock mpb;
        void Start()
        {
            bot = GetComponent<KryvonBot>();
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "ScopeGlare";
            flare = q.transform; flare.SetParent(transform, false); mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = KryvonFX.BeamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            q.SetActive(false);
        }
        void LateUpdate()
        {
            var cam = Camera.main; if (!cam || !bot || !flare) return;
            bool on = bot.S == KryvonBot.State.Fight && bot.IsSniper && bot.Target && bot.Target.CompareTag("Player") && !KryvonDrop.Active;
            if (flare.gameObject.activeSelf != on) flare.gameObject.SetActive(on);
            if (!on) return;
            var head = transform.position + Vector3.up * 1.62f + transform.forward * 0.25f;
            float d = Vector3.Distance(cam.transform.position, head);
            flare.position = head; flare.rotation = Quaternion.LookRotation(flare.position - cam.transform.position);
            flare.localScale = Vector3.one * Mathf.Clamp(d * 0.018f, 0.25f, 6f) * (0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 4f, 1.3f));
            mpb ??= new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", new Color(1f, 0.96f, 0.85f, 0.95f)); mr.SetPropertyBlock(mpb);
        }
    }
}

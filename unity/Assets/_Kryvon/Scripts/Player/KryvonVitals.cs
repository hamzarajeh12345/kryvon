// The body under the gear – what the player feels in the match:
//   STAMINA       sprinting drains it (~25 s of sprint); empty = no sprint until it recovers; heavy breathing and a heartbeat
//                 rise with exhaustion (synthesised, no audio files needed)
//   SCOPE SWAY    with a 2x+ scope the aim drifts in a slow figure-eight, more when exhausted; HOLD BREATH (Shift / the
//                 touch BREATH button) steadies it for up to 5 s, then you gasp and it sways more for a moment
//   LEG WOUND     a bullet in the leg = limping: 35 % slower with an uneven step and no sprint for 30 s or until healed
//   FLINCH        sudden damage kicks the view, shakes the screen and, on phones, buzzes the vibration motor
//   SWIMMING      in the sea around the island you float and swim slowly (no shooting) instead of walking on the sea bed
//   SHELL SHOCK   (static) a blast close by: ringing ears, muffled world, dark blurred edges for a few seconds
using UnityEngine;

namespace Kryvon
{
    public class KryvonVitals : MonoBehaviour
    {
        public static KryvonVitals Local { get; private set; }
        public float Stamina { get; private set; } = 100f;
        public bool HoldingBreath { get; private set; }
        public bool LegWounded => Time.time < legUntil;
        public bool Swimming => body && body.IsSwimming;
        public float Exertion { get; private set; }               // 0 rested … 1 exhausted

        PlayerController body; WeaponController wc; PlayerInputReader input; Health hp; ThirdPersonCamera rig;
        AudioSource breathSrc, heartSrc; float legUntil, lastHp, breathT, gaspUntil, nextBuzz, swayT; Vector2 swayLast;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Hook(); UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Hook(); }
        static void Hook()
        {
            if (FindAnyObjectByType<KryvonVitals>()) return;
            var pc = FindAnyObjectByType<PlayerController>(); if (pc) pc.gameObject.AddComponent<KryvonVitals>();
        }

        void Start()
        {
            Local = this;
            body = GetComponent<PlayerController>(); wc = GetComponent<WeaponController>(); input = GetComponent<PlayerInputReader>(); hp = GetComponent<Health>();
            if (Camera.main) rig = Camera.main.GetComponent<ThirdPersonCamera>();
            breathSrc = gameObject.AddComponent<AudioSource>(); breathSrc.clip = KryvonSynth.Breath(); breathSrc.loop = true; breathSrc.volume = 0; breathSrc.spatialBlend = 0; breathSrc.Play();
            heartSrc = gameObject.AddComponent<AudioSource>(); heartSrc.clip = KryvonSynth.Heartbeat(); heartSrc.loop = true; heartSrc.volume = 0; heartSrc.spatialBlend = 0; heartSrc.Play();
            if (hp) { lastHp = hp.Current; hp.OnDamaged += OnHurt; }
            var sea = GameObject.Find("Sea"); if (sea && body) body.WaterY = sea.transform.position.y;
        }
        void OnDestroy() { if (hp) hp.OnDamaged -= OnHurt; }

        void OnHurt(Health h, DamageInfo info)
        {
            if (info.Cause == "Al-Tawq" || info.Amount <= 0.5f) return;
            // a bullet in the leg: limp
            if (info.Limb && info.Amount > 5f) { if (!LegWounded) KryvonKillFeed.Notice("LEG WOUNDED  ·  HEAL TO STOP LIMPING"); legUntil = Time.time + 30f; }
            // flinch: the view kicks up and sideways, the screen shakes; phones buzz on the bigger hits
            float k = Mathf.Clamp01(info.Amount / 45f);
            rig?.AddRecoil(new Vector2(Random.Range(-0.7f, 0.7f) * k * 2f, 1.4f * k));
            CameraShake.Kick(0.15f + 0.45f * k);
            if (Application.isMobilePlatform && info.Amount >= 12f && Time.time > nextBuzz && PlayerPrefs.GetInt("kryvon.vibrate", 1) == 1)
            { nextBuzz = Time.time + 0.5f;
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }
        }

        void Update()
        {
            if (!body || !hp) return;
            float dt = Time.deltaTime;
            // ---- healing ends a leg wound ----
            if (hp.Current - lastHp > 9f) legUntil = 0f;
            lastHp = hp.Current;

            // ---- stamina ----
            bool sprint = body.IsSprinting;
            if (sprint) { Stamina -= 4f * dt; breathT = 0; }
            else if (HoldingBreath) Stamina -= 9f * dt;
            else { breathT += dt; if (breathT > 1f) Stamina += (body.IsMoving ? 7f : 12f) * dt; }
            Stamina = Mathf.Clamp(Stamina, 0f, 100f);
            if (Stamina <= 0.5f) body.SprintLocked = true; else if (Stamina > 30f && !LegWounded) body.SprintLocked = false;
            if (LegWounded) body.SprintLocked = true;
            // exertion follows stamina with a lag (you keep panting after you stop)
            float wantEx = Mathf.Clamp01((70f - Stamina) / 70f);
            Exertion = Mathf.MoveTowards(Exertion, wantEx, (wantEx > Exertion ? 0.6f : 0.12f) * dt);
            breathSrc.volume = Mathf.Lerp(breathSrc.volume, Exertion * 0.55f * (HoldingBreath ? 0f : 1f), 4f * dt);
            breathSrc.pitch = 0.85f + 0.45f * Exertion;
            heartSrc.volume = Mathf.Lerp(heartSrc.volume, Mathf.Clamp01(Exertion * 1.2f - 0.25f) * 0.6f + (hp.Current < hp.Max * 0.25f ? 0.35f : 0f), 3f * dt);
            heartSrc.pitch = 0.9f + 0.7f * Exertion;

            // ---- leg wound: slower, with an uneven (limping) step ----
            body.InjuryMul = LegWounded ? 0.65f * (0.8f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 4.2f))) : 1f;

            // ---- scope sway + hold breath ----
            bool scoped = wc && wc.IsAds && wc.CurrentScope >= ScopeType.X2;
            bool wantHold = scoped && input && input.SprintHeld && Stamina > 5f;
            if (HoldingBreath && (!wantHold || breathHeld > 5f)) { HoldingBreath = false; gaspUntil = Time.time + 2f; }
            else if (!HoldingBreath && wantHold && Time.time > gaspUntil) { HoldingBreath = true; breathHeld = 0; }
            if (HoldingBreath) breathHeld += dt;
            if (scoped && rig)
            {
                float amp = (0.28f + 1.3f * Exertion) * (HoldingBreath ? 0.08f : Time.time < gaspUntil ? 1.7f : 1f);
                if (body.Stance == Stance.Prone) amp *= 0.45f; else if (body.Stance == Stance.Crouch) amp *= 0.7f;
                swayT += dt * (0.55f + 0.5f * Exertion);
                var off = new Vector2(Mathf.Sin(swayT * 1.1f) * 0.8f + (Mathf.PerlinNoise(swayT * 0.7f, 3.1f) - 0.5f) * 0.6f, Mathf.Sin(swayT * 2.2f) * 0.45f + (Mathf.PerlinNoise(7.7f, swayT * 0.7f) - 0.5f) * 0.6f) * amp;
                rig.AddRecoil(off - swayLast); swayLast = off;          // only the change: the aim drifts, it doesn't run away
            }
            else swayLast = Vector2.zero;
        }
        float breathHeld;

        void OnGUI()
        {
            // a thin stamina bar under the health bar while it isn't full, plus the hold-breath / leg-wound hints
            if (KryvonTrailer.HideHud || KryvonDrop.Active) return;
            if (Stamina < 99f || HoldingBreath)
            {
                float w = Screen.height * 0.4f, h = Mathf.Max(3f, Screen.height * 0.005f), x = (Screen.width - w) / 2f, y = Screen.height * 0.905f;
                GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), Texture2D.whiteTexture);
                GUI.color = Stamina < 25f ? new Color(0.95f, 0.45f, 0.3f, 0.9f) : new Color(0.55f, 0.85f, 1f, 0.85f); GUI.DrawTexture(new Rect(x, y, w * Stamina / 100f, h), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (wc && wc.IsAds && wc.CurrentScope >= ScopeType.X2)
            {
                hint ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.Max(14, Screen.height / 48) };
                hint.normal.textColor = HoldingBreath ? new Color(0.55f, 0.85f, 1f) : new Color(1, 1, 1, 0.75f);
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 30), HoldingBreath ? "HOLDING BREATH" : KryvonTouchControls.Wanted ? "HOLD  BREATH  TO STEADY" : "HOLD  SHIFT  TO STEADY", hint);
            }
        }
        GUIStyle hint;
    }

    /// a blast close by: ringing ears, the world goes muffled and the screen edges darken for a few seconds
    public class KryvonShellShock : MonoBehaviour
    {
        static KryvonShellShock inst; AudioSource ring; AudioLowPassFilter lpf; float until, strength; Texture2D vig;
        public static void Near(Vector3 blast, float scale)
        {
            var cam = Camera.main; if (!cam) return;
            float d = Vector3.Distance(cam.transform.position, blast), r = 13f * scale;
            if (d > r) return;
            if (!inst) inst = cam.gameObject.AddComponent<KryvonShellShock>();
            float s = Mathf.Clamp01(1.15f - d / r);
            inst.strength = Mathf.Max(inst.strength, s); inst.until = Time.time + 2.5f + 3f * s;
            if (!inst.ring) { inst.ring = inst.gameObject.AddComponent<AudioSource>(); inst.ring.clip = KryvonSynth.Tinnitus(); inst.ring.loop = true; inst.ring.spatialBlend = 0; inst.ring.Play(); }
            if (!inst.lpf) inst.lpf = inst.GetComponent<AudioLowPassFilter>() ?? inst.gameObject.AddComponent<AudioLowPassFilter>();
            CameraShake.Kick(0.4f + 0.6f * s);
        }
        void Update()
        {
            float left = until - Time.time, k = left > 0 ? Mathf.Clamp01(left / 2.5f) * strength : 0f;
            if (left <= 0) strength = Mathf.MoveTowards(strength, 0, Time.deltaTime);
            if (ring) ring.volume = k * 0.5f;
            if (lpf) { lpf.enabled = k > 0.02f; lpf.cutoffFrequency = Mathf.Lerp(22000f, 650f, k); }
        }
        void OnGUI()
        {
            float left = until - Time.time; if (left <= 0) return;
            float k = Mathf.Clamp01(left / 2.5f) * strength;
            if (!vig)
            {
                const int S = 128; vig = new Texture2D(S, S, TextureFormat.RGBA32, false);
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float u = x / (S - 1f) * 2 - 1, v = y / (S - 1f) * 2 - 1; float r = Mathf.Sqrt(u * u + v * v); vig.SetPixel(x, y, new Color(0.05f, 0.04f, 0.04f, Mathf.Clamp01((r - 0.45f) / 0.75f))); }
                vig.Apply();
            }
            GUI.color = new Color(1, 1, 1, k * 0.95f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vig);
            GUI.color = new Color(1, 1, 1, k * 0.25f * Mathf.Clamp01(left - 4f)); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);   // the first white flash
            GUI.color = Color.white;
        }
    }

    /// sounds made in code (no audio files): breathing, a heartbeat, ear ringing
    public static class KryvonSynth
    {
        const int Rate = 22050;
        static AudioClip breath, heart, tinnitus;
        static float Noise(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s / (float)uint.MaxValue) * 2f - 1f; }
        public static AudioClip Breath()
        {
            if (breath) return breath;
            int n = Rate * 2; var d = new float[n]; uint seed = 12345; float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;                          // 2 s: inhale 0–0.8, exhale 0.9–1.9
                float inh = t < 0.8f ? Mathf.Sin(Mathf.PI * t / 0.8f) : 0f, exh = t > 0.9f && t < 1.9f ? Mathf.Sin(Mathf.PI * (t - 0.9f) / 1f) : 0f;
                float x = Noise(ref seed); lp += (x - lp) * 0.18f; lp2 += (lp - lp2) * 0.35f;     // soft, breathy noise
                d[i] = lp2 * (inh * 0.55f + exh * 0.8f) * 1.6f;
            }
            breath = AudioClip.Create("breath", n, 1, Rate, false); breath.SetData(d, 0); return breath;
        }
        public static AudioClip Heartbeat()
        {
            if (heart) return heart;
            int n = Rate; var d = new float[n];                     // 1 s loop = 60 bpm at pitch 1 (faster with pitch)
            void Thump(float at, float amp) { int s = (int)(at * Rate); for (int i = 0; i < Rate / 8 && s + i < n; i++) { float t = i / (float)Rate; d[s + i] += Mathf.Sin(2 * Mathf.PI * 52f * t) * Mathf.Exp(-t * 28f) * amp; } }
            Thump(0f, 0.9f); Thump(0.28f, 0.6f);
            heart = AudioClip.Create("heart", n, 1, Rate, false); heart.SetData(d, 0); return heart;
        }
        public static AudioClip Tinnitus()
        {
            if (tinnitus) return tinnitus;
            int n = Rate; var d = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = (Mathf.Sin(2 * Mathf.PI * 4100f * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * 4125f * t) * 0.4f) * 0.35f; }
            tinnitus = AudioClip.Create("tinnitus", n, 1, Rate, false); tinnitus.SetData(d, 0); return tinnitus;
        }
    }
}

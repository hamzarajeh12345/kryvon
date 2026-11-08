// Synthesises KRYVON's sound effects as WAV assets (no third-party audio, no licences to track).
// Menu: KRYVON/Generate Sounds
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonAudioGen
    {
        const string Dir = "Assets/_Kryvon/Audio";
        const int SR = 44100;

        [MenuItem("KRYVON/Generate Sounds")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);
            var rng = new System.Random(1946);
            // gunshots: (name, length s, body Hz, crack brightness, tail s, gain)
            Gun("gun_ar", rng, 0.9f, 95f, 0.75f, 0.55f, 1f);
            Gun("gun_smg", rng, 0.6f, 120f, 0.85f, 0.35f, 0.85f);
            Gun("gun_sniper", rng, 1.8f, 62f, 0.6f, 1.4f, 1f);
            Gun("gun_shotgun", rng, 1.2f, 70f, 0.5f, 0.9f, 1f);
            Gun("gun_pistol", rng, 0.6f, 140f, 0.9f, 0.35f, 0.8f);
            Gun("gun_lmg", rng, 0.9f, 85f, 0.7f, 0.6f, 1f);
            Gun("gun_suppressed", rng, 0.35f, 180f, 0.25f, 0.12f, 0.45f);
            for (int i = 0; i < 6; i++) Step($"step_hard_{i}", rng, 0.18f, 2600f, 0.55f);
            for (int i = 0; i < 6; i++) Step($"step_dirt_{i}", rng, 0.24f, 900f, 0.75f);
            Wind("amb_wind", rng, 12f);
            Birds("amb_birds", rng, 14f);
            Click("ui_dry_fire", rng); Reload("reload", rng);
            Impact("impact_concrete", rng, 3200f); Impact("impact_body", rng, 500f);
            Engine("vehicle_engine", 2f);
            AssetDatabase.Refresh();
            foreach (var f in Directory.GetFiles(Dir, "*.wav"))
            {
                var ai = AssetImporter.GetAtPath(f.Replace('\\', '/')) as AudioImporter; if (ai == null) continue;
                var s = ai.defaultSampleSettings; s.loadType = f.Contains("amb_") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                ai.defaultSampleSettings = s; ai.SaveAndReimport();
            }
            Debug.Log("KRYVON: sounds generated");
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2 - 1);

        // one-pole low/high pass helpers
        struct LP { float y; public float Run(float x, float a) => y += a * (x - y); }
        static float Coef(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * hz / SR);

        static void Gun(string name, System.Random r, float len, float body, float crack, float tail, float gain)
        {
            int n = (int)(len * SR); var d = new float[n];
            LP lp1 = default, lp2 = default, lpT = default; float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, w = Noise(r);
                float crackEnv = Mathf.Exp(-t * 90f);                    // sharp supersonic crack
                float boomEnv = Mathf.Exp(-t * 18f);                     // muzzle blast
                float tailEnv = Mathf.Exp(-t / Mathf.Max(0.05f, tail) * 3f) * Mathf.Clamp01(t * 30f); // echo off buildings
                float hi = w - lp1.Run(w, Coef(1800f));
                float mid = lp2.Run(w, Coef(900f));
                ph += 2 * Mathf.PI * body * (1f + 2.5f * Mathf.Exp(-t * 40f)) / SR;
                float thump = Mathf.Sin(ph) * boomEnv;
                float echo = lpT.Run(w, Coef(500f)) * tailEnv * 2.2f;
                d[i] = (hi * crackEnv * crack * 1.4f + mid * boomEnv * 1.6f + thump * 0.9f + echo) * gain;
            }
            Normalize(d, 0.95f); Write(name, d);
        }

        static void Step(string name, System.Random r, float len, float bright, float gain)
        {
            int n = (int)(len * SR); var d = new float[n]; LP a = default, b = default;
            float heel = 0.012f + (float)r.NextDouble() * 0.01f, toe = 0.06f + (float)r.NextDouble() * 0.03f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, w = Noise(r);
                float env = Mathf.Exp(-Mathf.Max(0, t - heel) * 60f) * Mathf.Clamp01(t / heel) + 0.55f * Mathf.Exp(-Mathf.Max(0, t - toe) * 45f) * (t > toe ? 1 : 0);
                float grit = a.Run(w, Coef(bright)); float low = b.Run(w, Coef(160f));
                d[i] = (grit * 0.9f + low * 2.2f) * env * gain;
            }
            Normalize(d, 0.6f); Write(name, d);
        }

        static void Wind(string name, System.Random r, float len)
        {
            int n = (int)(len * SR); var d = new float[n]; LP a = default, b = default;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, w = Noise(r);
                float gust = 0.55f + 0.3f * Mathf.Sin(t * 0.7f) + 0.15f * Mathf.Sin(t * 2.3f + 1f);
                float cut = 250f + 400f * gust;
                d[i] = (a.Run(w, Coef(cut)) - b.Run(w, Coef(60f)) * 0.6f) * gust;
            }
            Loopify(d, SR / 2); Normalize(d, 0.35f); Write(name, d);
        }

        static void Birds(string name, System.Random r, float len)
        {
            int n = (int)(len * SR); var d = new float[n];
            for (int k = 0; k < 22; k++)
            {
                int start = r.Next(0, n - SR); float f0 = 2600f + (float)r.NextDouble() * 2200f; int notes = 2 + r.Next(5);
                float vol = 0.15f + (float)r.NextDouble() * 0.35f;
                for (int m = 0; m < notes; m++)
                {
                    int s0 = start + m * (int)(0.09f * SR); float dur = 0.05f + (float)r.NextDouble() * 0.05f, ph = 0;
                    float sweep = ((float)r.NextDouble() - 0.3f) * 1800f;
                    for (int i = 0; i < dur * SR && s0 + i < n; i++)
                    {
                        float t = i / (float)SR, f = f0 + sweep * t / dur + 120f * Mathf.Sin(t * 180f);
                        ph += 2 * Mathf.PI * f / SR;
                        d[s0 + i] += Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * t / dur) * vol;
                    }
                }
            }
            Normalize(d, 0.3f); Write(name, d);
        }

        static void Click(string name, System.Random r) { var d = new float[(int)(0.08f * SR)]; for (int i = 0; i < d.Length; i++) d[i] = Noise(r) * Mathf.Exp(-i / (float)SR * 160f); Normalize(d, 0.5f); Write(name, d); }

        static void Reload(string name, System.Random r)
        {
            var d = new float[(int)(1.6f * SR)];
            foreach (var (at, len, hz) in new[] { (0.1f, 0.06f, 2500f), (0.55f, 0.08f, 1400f), (1.25f, 0.05f, 3000f), (1.33f, 0.07f, 1800f) })
            {
                LP a = default; int s0 = (int)(at * SR);
                for (int i = 0; i < len * SR; i++) { float t = i / (float)SR, w = Noise(r); d[s0 + i] += (w - a.Run(w, Coef(hz))) * Mathf.Exp(-t * 70f); }
            }
            Normalize(d, 0.55f); Write(name, d);
        }

        static void Impact(string name, System.Random r, float hz)
        {
            var d = new float[(int)(0.25f * SR)]; LP a = default;
            for (int i = 0; i < d.Length; i++) { float t = i / (float)SR, w = Noise(r); d[i] = a.Run(w, Coef(hz)) * Mathf.Exp(-t * 35f); }
            Normalize(d, 0.6f); Write(name, d);
        }

        static void Engine(string name, float len)
        {
            int n = (int)(len * SR); var d = new float[n]; float ph = 0, ph2 = 0;
            for (int i = 0; i < n; i++) { ph += 2 * Mathf.PI * 42f / SR; ph2 += 2 * Mathf.PI * 84f / SR; d[i] = Mathf.Sign(Mathf.Sin(ph)) * 0.35f + Mathf.Sin(ph2) * 0.3f + Mathf.Sin(ph * 3) * 0.15f; }
            Normalize(d, 0.4f); Write(name, d);
        }

        static void Loopify(float[] d, int fade) { for (int i = 0; i < fade; i++) { float k = i / (float)fade; d[i] = d[i] * k + d[d.Length - fade + i] * (1 - k); } Array.Resize(ref d, d.Length); }
        static void Normalize(float[] d, float peak) { float m = 1e-6f; foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v)); for (int i = 0; i < d.Length; i++) d[i] = d[i] / m * peak; }

        static void Write(string name, float[] d)
        {
            using var fs = new FileStream(Path.Combine(Dir, name + ".wav"), FileMode.Create);
            using var w = new BinaryWriter(fs);
            int bytes = d.Length * 2;
            w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(bytes);
            foreach (var v in d) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767));
        }
    }
}

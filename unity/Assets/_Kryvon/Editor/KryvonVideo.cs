// Renders a short gameplay trailer of the Play scene: JPG frames + a mixed WAV soundtrack in <project>/Temp/video.
// tools/make_video.py then encodes them to an MP4.   remote/batch: Kryvon.EditorTools.KryvonVideo.Render
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kryvon.EditorTools
{
    public static class KryvonVideo
    {
        const int FPS = 30, W = 1280, H = 720;
        const float LEN = 22f;
        const string Root = "Assets/_Kryvon";

        static AnimationClip Clip(string ch, string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Root}/Animations/{ch}/{name}.anim");

        public static void Render()
        {
            if (!File.Exists(Root + "/Scenes/Play.unity")) KryvonPlay.Build();
            EditorSceneManager.OpenScene(Root + "/Scenes/Play.unity");
            ShaderUtil.allowAsyncCompilation = false;
            var outDir = Path.Combine(Directory.GetCurrentDirectory(), "VideoFrames");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var cam = Camera.main; cam.fieldOfView = 55;
            var player = GameObject.Find("Player").transform;
            var playerAnim = player.GetComponentInChildren<Animator>().gameObject;
            var flash = player.Find("MuzzleLight").GetComponent<Light>();
            var squad = GameObject.Find("Squad").transform.Cast<Transform>()
                .Where(t => !t.name.StartsWith("w_") && t.GetComponentInChildren<Animator>() != null)
                .Select(t => (go: t.GetComponentInChildren<Animator>().gameObject, idle: Clip(t.name, "HumanSpIdle"), off: Random.value * 3f)).ToList();
            var hum = playerAnim.GetComponent<Animator>().avatar != null && playerAnim.GetComponent<Animator>().avatar.isHuman ? KryvonHumanoid.Clips() : null;
            AnimationClip HC(string n) => hum != null && hum.TryGetValue(n, out var c) ? c : null;
            var run = HC("Jog_Fwd_Loop") ?? Clip(KryvonPlay.PlayerCharacter, "HumanSpRun"); var idle = HC("Pistol_Idle_Loop") ?? Clip(KryvonPlay.PlayerCharacter, "HumanSpIdle");
            var aimC = HC("Pistol_Aim_Neutral"); var shootC = HC("Pistol_Shoot");
            var pan = playerAnim.GetComponent<Animator>();
            void PP(AnimationClip c, float tt) { if (c == null) return; if (hum != null) KryvonHumanoid.Pose(pan, c, tt); else c.SampleAnimation(playerAnim, tt); }
            var overrides = GameObject.Find("PlayOverrides")?.GetComponent<Volume>();

            float[] shots = { 13.6f, 13.72f, 13.84f, 13.96f, 14.9f, 15.02f, 15.14f, 15.26f, 15.38f, 16.4f };
            var steps = new List<float>();
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            int frames = Mathf.RoundToInt(LEN * FPS);
            float nextStep = 5.2f;

            for (int f = 0; f < frames; f++)
            {
                float t = f / (float)FPS;
                foreach (var s in squad) if (s.idle) s.idle.SampleAnimation(s.go, (t + s.off) % s.idle.length);
                if (overrides) overrides.enabled = t >= 5f;                  // depth of field only on the opening shot
                flash.enabled = false;
                Vector3 camPos, look;
                if (t < 5f)                                                  // A: dolly along the squad
                {
                    player.gameObject.SetActive(false);
                    float k = Mathf.SmoothStep(0, 1, t / 5f);
                    camPos = new Vector3(Mathf.Lerp(-7f, 6f, k), 1.35f + 0.25f * k, -6.5f + Mathf.Sin(k * Mathf.PI) * 1.2f);
                    look = new Vector3(Mathf.Lerp(-2f, 2f, k), 1.35f, 1f);
                }
                else if (t < 13f)                                            // B: run down the street
                {
                    player.gameObject.SetActive(true);
                    float u = t - 5f, x = -25f + u * 5f;
                    player.position = new Vector3(x, 0.02f, -0.9f); player.rotation = Quaternion.Euler(0, 90, 0);
                    PP(run, u % (run ? run.length : 1));
                    float sway = Mathf.Sin(u * 2.2f) * 0.15f;
                    camPos = player.position + new Vector3(-3.6f, 1.75f + sway * 0.3f, -1.1f + sway);
                    look = player.position + new Vector3(4f, 1.3f, 0.4f);
                    if (t >= nextStep) { steps.Add(t); nextStep += 0.36f; }
                }
                else if (t < 17f)                                            // C: stop, aim at the buildings and fire
                {
                    float u = t - 13f;
                    player.position = new Vector3(15f, 0.02f, -0.9f);
                    player.rotation = Quaternion.Euler(0, Mathf.Lerp(90f, 20f, Mathf.SmoothStep(0, 1, u / 0.5f)), 0);
                    float lastShot = -9f; foreach (var s2 in shots) if (t >= s2) lastShot = s2;
                    if (shootC && t - lastShot < 0.25f) PP(shootC, (t - lastShot) * 1.6f);
                    else if (aimC && u > 0.4f) PP(aimC, 0.1f);
                    else if (idle) PP(idle, u % idle.length);
                    float kick = 0f;
                    foreach (var s in shots) if (t >= s && t < s + 2f / FPS) { flash.enabled = true; flash.intensity = Random.Range(4f, 7f); }
                    foreach (var s in shots) if (t >= s) kick += Mathf.Exp(-(t - s) * 14f) * 0.05f;
                    camPos = player.position + player.rotation * new Vector3(0.6f, 1.7f, -1.9f) + Vector3.up * kick;
                    look = player.position + player.rotation * new Vector3(0.15f, 1.55f + kick * 3f, 12f);
                }
                else                                                         // D: crane up over the town
                {
                    float k = Mathf.SmoothStep(0, 1, (t - 17f) / 5f);
                    if (idle) PP(idle, (t - 17f) % idle.length);
                    camPos = Vector3.Lerp(new Vector3(24f, 2.2f, -10f), new Vector3(-6f, 32f, -46f), k);
                    look = Vector3.Lerp(new Vector3(14f, 1.5f, 0f), new Vector3(0f, 4f, 10f), k);
                }
                cam.transform.position = camPos; cam.transform.LookAt(look);
                cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, $"f_{f:00000}.jpg"), tex.EncodeToJPG(93));
                if (f % 60 == 0) Debug.Log($"KRYVON video: frame {f}/{frames}");
            }
            KryvonHumanoid.EndPose();
            cam.targetTexture = null; Object.DestroyImmediate(rt);
            MixAudio(Path.Combine(outDir, "mix.wav"), steps, shots);
            Debug.Log("KRYVON video: frames + audio ready in " + outDir);
        }

        // ---------- soundtrack ----------
        static float[] Wav(string name)
        {
            var p = Path.Combine(Directory.GetCurrentDirectory(), Root, "Audio", name + ".wav");
            if (!File.Exists(p)) return new float[0];
            var b = File.ReadAllBytes(p); int n = (b.Length - 44) / 2; var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = System.BitConverter.ToInt16(b, 44 + i * 2) / 32768f;
            return d;
        }
        static void Add(float[] mix, float[] s, float at, float vol)
        {
            int o = (int)(at * 44100);
            for (int i = 0; i < s.Length && o + i < mix.Length; i++) if (o + i >= 0) mix[o + i] += s[i] * vol;
        }
        static void MixAudio(string file, List<float> steps, float[] shots)
        {
            var mix = new float[(int)(LEN * 44100)];
            var wind = Wav("amb_wind"); var birds = Wav("amb_birds");
            for (float t = 0; t < LEN; t += wind.Length / 44100f) Add(mix, wind, t, 0.45f);
            for (float t = 0; t < LEN; t += birds.Length / 44100f) Add(mix, birds, t, 0.35f);
            var hard = Enumerable.Range(0, 6).Select(i => Wav($"step_hard_{i}")).ToArray();
            for (int i = 0; i < steps.Count; i++) Add(mix, hard[i % hard.Length], steps[i], 0.55f);
            var gun = Wav("gun_ar"); foreach (var s in shots) Add(mix, gun, s, 0.7f);
            Add(mix, Wav("reload"), 16.9f, 0.6f);
            float peak = mix.Max(v => Mathf.Abs(v)); if (peak > 0.98f) for (int i = 0; i < mix.Length; i++) mix[i] *= 0.98f / peak;
            using var w = new BinaryWriter(File.Create(file));
            int bytes = mix.Length * 2;
            w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray()); w.Write("fmt ".ToCharArray());
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(bytes);
            foreach (var v in mix) w.Write((short)(Mathf.Clamp(v, -1, 1) * 32767));
        }
    }
}

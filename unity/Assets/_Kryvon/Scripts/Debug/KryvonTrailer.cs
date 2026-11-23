// Cinematic trailer, recorded by the game itself: start the player with -trailer.
// Time runs at a fixed 30 frames per second of game time (Time.captureFramerate), so every frame is perfect no matter
// how slow the PC is. Frames → <persistentDataPath>/trailer/f_00000.jpg, the game's own sound → trailer/audio.wav,
// shot list → trailer/shots.txt. tools/make_trailer.py turns them into an MP4 with titles.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public class KryvonTrailer : MonoBehaviour
    {
        public static bool On, HideHud;
        static string Dir => Path.Combine(Application.persistentDataPath, "trailer");
        const int W = 1920, H = 1080;
        RenderTexture rt; Texture2D grab;
        bool rec; int frame; readonly List<float> audio = new(); readonly StringBuilder shots = new();
        Camera cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-trailer") < 0) return;
            On = true; Application.runInBackground = true;
            var g = new GameObject("Trailer"); DontDestroyOnLoad(g); g.AddComponent<KryvonTrailer>();
        }

        void Shot(string name) { shots.AppendLine($"{frame} {name}"); File.WriteAllText(Path.Combine(Dir, "shots.txt"), shots.ToString()); rec = true; }
        FileStream raw;
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.deltaTime) yield return null; }

        IEnumerator Recorder()
        {
            var eof = new WaitForEndOfFrame();
            int ch = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            while (true)
            {
                yield return eof;
                int n = AudioRenderer.GetSampleCountForCaptureFrame();
                var buf = new NativeArray<float>(n * ch, Allocator.Temp);
                AudioRenderer.Render(buf);
                if (rec) { var bytes = new byte[buf.Length * 4]; System.Buffer.BlockCopy(buf.ToArray(), 0, bytes, 0, bytes.Length); raw.Write(bytes, 0, bytes.Length); raw.Flush(); }
                buf.Dispose();
                // every camera renders into a 1920x1080 picture, whatever the window size is
                var c = Camera.main;
                if (c && c.targetTexture != rt) c.targetTexture = rt;
                foreach (var cv in FindObjectsByType<Canvas>())
                    if (cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay && c) { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = c; cv.planeDistance = Mathf.Max(c.nearClipPlane + 0.05f, 0.5f); }
                if (rec)
                {
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    grab.ReadPixels(new Rect(0, 0, W, H), 0, 0, false); grab.Apply(false);
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(Dir, $"f_{frame:00000}.jpg"), grab.EncodeToJPG(93));
                    frame++;
                }
            }
        }

        IEnumerator Start()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            Directory.CreateDirectory(Dir);
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 }; rt.Create();
            grab = new Texture2D(W, H, TextureFormat.RGB24, false);
            raw = File.Create(Path.Combine(Dir, "audio.f32"));
            File.WriteAllText(Path.Combine(Dir, "audio.txt"), $"{AudioSettings.outputSampleRate} {(AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2)}");
            Time.captureFramerate = 30; AudioRenderer.Start();
            StartCoroutine(Recorder());
            yield return Wait(4.5f);                                   // the splash, not recorded

            // ---- 1. lobby: slow push in on the squad, switch hero halfway ----
            cam = Camera.main; var lobby = FindAnyObjectByType<KryvonLobby>();
            var c0 = cam.transform.position;
            Shot("lobby");
            for (float t = 0; t < 8f; t += Time.deltaTime)
            {
                cam.transform.position = c0 + cam.transform.forward * Mathf.SmoothStep(0, 1.2f, t / 8f);
                if (t >= 4f && t - Time.deltaTime < 4f && lobby) typeof(KryvonLobby).GetMethod("ShowHero", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(lobby, new object[] { 1, false });
                yield return null;
            }
            rec = false;
            if (lobby) lobby.SendMessage("StartMatch");
            while (SceneManager.GetActiveScene().name != "Play") yield return null;
            yield return Wait(1f);

            var player = GameObject.FindGameObjectWithTag("Player");
            var hp = player.GetComponent<Health>(); hp.OnDamaged += (h, _) => h.Heal(100f, 100f);       // the hero survives the trailer
            var input = player.GetComponent<PlayerInputReader>();
            cam = Camera.main; var rig = cam.GetComponent<ThirdPersonCamera>();
            var drop = KryvonDrop.Instance;
            HideHud = true;

            // ---- 2. waiting area: orbit ----
            if (drop && drop.S == KryvonDrop.Stage.Waiting)
            {
                rig.enabled = false; Shot("waiting");
                for (float t = 0; t < 4f && drop.S == KryvonDrop.Stage.Waiting; t += Time.deltaTime)
                {
                    var p = player.transform.position;
                    cam.transform.position = p + Quaternion.Euler(0, 160 + t * 22, 0) * new Vector3(0, 0, 6.5f) + Vector3.up * 2f;
                    cam.transform.LookAt(p + Vector3.up * 1.3f); yield return null;
                }
                rec = false;
            }

            // ---- 3. the jet over the map ----
            if (drop)
            {
                while (drop.S == KryvonDrop.Stage.Waiting) yield return null;
                GameObject jet = null; while (drop.S == KryvonDrop.Stage.Plane && !(jet = GameObject.Find("DropJet"))) yield return null;
                if (jet)
                {
                    rig.enabled = false; Shot("plane");
                    for (float t = 0; t < 5f && drop.S == KryvonDrop.Stage.Plane; t += Time.deltaTime)
                    {
                        var j = jet.transform;
                        cam.transform.position = j.position - j.forward * (46f - t * 5f) + j.right * (18f - t * 2f) + Vector3.up * (6f + t);
                        cam.transform.LookAt(j.position + j.forward * 8f); yield return null;
                    }
                    rec = false;
                }
                // jump now
                rig.enabled = true;
                var jump = typeof(KryvonDrop).GetMethod("Jump", BindingFlags.NonPublic | BindingFlags.Instance);
                if (drop.S == KryvonDrop.Stage.Plane) jump?.Invoke(drop, null);

                // ---- 4. freefall, the chute opening ----
                yield return Wait(0.3f);
                Shot("freefall");
                for (float t = 0; t < 7f && drop.S == KryvonDrop.Stage.Freefall; t += Time.deltaTime) { input.TouchMove(new Vector2(0, 1)); yield return null; }
                input.TouchMove(Vector2.zero);
                yield return Wait(4f); rec = false;
                while (drop.S == KryvonDrop.Stage.Chute && drop.Height > 16f) yield return null;      // skip the long glide
                Shot("landing");
                while (drop.S != KryvonDrop.Stage.Landed) yield return null;
                yield return Wait(1.2f); rec = false;
            }

            // ---- 5. sprint (gameplay view) ----
            HideHud = false; Shot("run");
            for (float t = 0; t < 3.5f; t += Time.deltaTime) { input.TouchMove(new Vector2(0, 1)); input.TouchSprint(true); yield return null; }
            input.TouchMove(Vector2.zero); input.TouchSprint(false); rec = false;

            // ---- 6. drive ----
            KryvonVehicle car = null; float best = float.MaxValue;
            foreach (var v in KryvonVehicle.All) { if (!v || v.Wrecked) continue; float d = (v.transform.position - player.transform.position).sqrMagnitude; if (d < best) { best = d; car = v; } }
            if (car)
            {
                car.Enter(player); HideHud = true; rig.enabled = false;
                var ct = car.transform; var travel = ct.forward; var last = ct.position;
                cam.transform.position = ct.position - travel * 11f + Vector3.up * 4.5f;
                Shot("drive");
                for (float t = 0; t < 7f; t += Time.deltaTime)
                {
                    input.TouchMove(new Vector2(Mathf.Sin(t * 0.9f) * 0.4f, 1f)); input.TouchSprint(t > 2f);
                    var mv = ct.position - last; mv.y = 0; last = ct.position;
                    if (mv.sqrMagnitude > 0.0004f) travel = Vector3.Slerp(travel, mv.normalized, 0.2f);
                    var want = ct.position - travel * 11f + Vector3.Cross(Vector3.up, travel) * 2f + Vector3.up * 4.5f;
                    cam.transform.position = Vector3.Lerp(cam.transform.position, want, 0.15f);
                    cam.transform.LookAt(ct.position + travel * 5f + Vector3.up * 1.2f); yield return null;
                }
                input.TouchMove(Vector2.zero); input.TouchSprint(false); rec = false;
                yield return Wait(2f); car.Exit();
                rig.enabled = true; rig.SetTarget(player.transform);
            }

            // ---- 7. fight two bots ----
            var foes = new List<KryvonBot>();
            foreach (var b in KryvonBot.All) if (b && b.S != KryvonBot.State.Dead && b.S != KryvonBot.State.Plane && b.S != KryvonBot.State.Falling) { foes.Add(b); if (foes.Count == 2) break; }
            if (foes.Count > 0)
            {
                var f = player.transform.forward; f.y = 0; f.Normalize();
                for (int i = 0; i < foes.Count; i++)
                {
                    var p = player.transform.position + f * (24f + i * 6f) + Vector3.Cross(Vector3.up, f) * (i == 0 ? -3f : 4f);
                    if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var g, 80f, ~0, QueryTriggerInteraction.Ignore)) p = g.point + Vector3.up * 0.05f;
                    var cc = foes[i].GetComponent<CharacterController>(); if (cc) cc.enabled = false;
                    foes[i].transform.SetPositionAndRotation(p, Quaternion.LookRotation(-f)); if (cc) cc.enabled = true;
                    foes[i].Engage(hp);
                }
                HideHud = false; Shot("fight");
                var yawF = typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.NonPublic | BindingFlags.Instance);
                var pitchF = typeof(ThirdPersonCamera).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance);
                for (float t = 0; t < 9f; t += Time.deltaTime)
                {
                    KryvonBot tgt = null; foreach (var b in foes) if (b && b.S != KryvonBot.State.Dead) { tgt = b; break; }
                    if (tgt)
                    {
                        var dir = tgt.transform.position + Vector3.up * 1.25f - cam.transform.position;
                        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, pitch = Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1, 1)) * Mathf.Rad2Deg;
                        float cy = (float)yawF.GetValue(rig), cp = (float)pitchF.GetValue(rig);
                        yawF.SetValue(rig, Mathf.LerpAngle(cy, yaw, 0.35f)); pitchF.SetValue(rig, Mathf.Lerp(cp, pitch, 0.35f));
                        input.TouchFire((t % 1.1f) < 0.65f);
                    }
                    else input.TouchFire(false);
                    yield return null;
                }
                input.TouchFire(false); rec = false;

                // ---- 8. frag grenade at whoever is left ----
                var thr = player.GetComponent<KryvonThrowables>();
                if (thr && thr.Count(Throwable.Frag) > 0)
                {
                    Shot("grenade"); thr.TouchThrowDown(); yield return Wait(0.9f); thr.TouchThrowUp(); yield return Wait(4.5f); rec = false;
                }
            }

            // ---- 9. Petra ----
            HideHud = true; rig.enabled = false;
            var petra = GameObject.Find("Petra");
            if (petra)
            {
                var pt = petra.transform; Shot("petra");
                for (float t = 0; t < 6f; t += Time.deltaTime)
                {
                    var sp = GameObject.Find("Spawn_1"); var o = sp ? sp.transform.position : pt.TransformPoint(new Vector3(0, 0, -32));
                    var fwd = sp ? sp.transform.forward : pt.forward;
                    cam.transform.position = o - fwd * (14f - t * 2.2f) + Vector3.up * (1.8f + t * 0.9f);
                    cam.transform.LookAt(o + fwd * 34f + Vector3.up * (9f + t)); yield return null;
                }
                rec = false;
            }

            // ---- 10. aerial over the towns at the end ----
            Shot("aerial");
            for (float t = 0; t < 6f; t += Time.deltaTime)
            {
                cam.transform.position = new Vector3(-180f + t * 22f, 110f - t * 4f, -60f + t * 30f);
                cam.transform.rotation = Quaternion.Euler(22f, 35f + t * 3f, 0); yield return null;
            }
            rec = false;

            // ---- write the sound + shot list, quit ----
            raw.Close();
            File.WriteAllText(Path.Combine(Dir, "shots.txt"), shots.ToString());
            AudioRenderer.Stop(); Time.captureFramerate = 0;
            Application.Quit();
        }

        static void WriteWav(string path, List<float> s, int rate, int ch)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = s.Count * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (var v in s) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }
}

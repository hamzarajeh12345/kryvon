// Self-test that runs inside the game when started with -autotest (no OS mouse/keyboard involved):
// lobby → START → match; walks forward, turns, fires; logs every error, the frame rate and the player's
// position to autotest.log and saves screenshots next to it (Application.persistentDataPath), then quits.
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public class KryvonAutoTest : MonoBehaviour
    {
        static readonly StringBuilder log = new();
        static string Dir => Application.persistentDataPath;
        static void W(string s) { log.AppendLine($"[{Time.realtimeSinceStartup:0.0}] {s}"); File.WriteAllText(Path.Combine(Dir, "autotest.log"), log.ToString()); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") < 0) return;
            Application.runInBackground = true;
            { var av = System.Environment.GetCommandLineArgs(); int si = System.Array.IndexOf(av, "-skin"); if (si >= 0) { foreach (var id in av[si + 1].Split(',')) { var c = KryvonSkins.Get(id); if (c != null) { KryvonSkins.Grant(id); KryvonSkins.Equip(c); } } } }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fresh") >= 0) { PlayerPrefs.DeleteKey("kryvon.name"); PlayerPrefs.Save(); }   // first launch: sign-in screen
            // isolation switches for hunting GPU problems: -off Shaft,Dust,NeonLight,Neon,Post
            var args = System.Environment.GetCommandLineArgs(); int oi = System.Array.IndexOf(args, "-off");
            void Off()
            {
                if (oi >= 0 && oi + 1 < args.Length)
                    foreach (var name in args[oi + 1].Split(','))
                        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
                            if (t.name == name) t.gameObject.SetActive(false);
            }
            Off(); SceneManager.sceneLoaded += (_, _) => Off();     // the lobby comes after the Boot scene
            Application.logMessageReceived += (msg, st, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) W($"{type}: {msg}\n{st}"); };
            var go = new GameObject("AutoTest"); DontDestroyOnLoad(go); go.AddComponent<KryvonAutoTest>();
            W($"autotest start, scene {SceneManager.GetActiveScene().name}, {Screen.width}x{Screen.height}");
        }

        IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(Dir, n + ".png")); W("shot " + n); }

        // -autotest -levant: fly over the new Levant map and photograph it
        IEnumerator Levant()
        {
            SceneManager.LoadScene("Levant"); yield return null; yield return new WaitForSeconds(3f);
            var cam = Camera.main; var t = cam.transform;
            yield return null; cam.farClipPlane = 4000;      // the graphics preset limits view distance on weak GPUs; the fly-over needs it all
            int frames = 0; float s0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - s0 < 3) { frames++; yield return null; }
            W($"levant fps {frames / 3f:0.0}");
            var shots = new (string n, Vector3 pos, Vector3 look)[]
            {
                ("v_overview", new Vector3(0, 1500, -1150), new Vector3(0, 0, 60)),
                ("v_beirut", new Vector3(-840, 160, 330), new Vector3(-660, 0, 470)),
                ("v_damascus", new Vector3(-80, 140, 120), new Vector3(70, 30, 260)),
                ("v_petra", new Vector3(780, 220, -460), new Vector3(650, 60, -282)),
                ("v_deadsea", new Vector3(-700, 160, -480), new Vector3(-850, 0, -290)),
                ("v_road", new Vector3(-420, 40, 150), new Vector3(-250, 25, 200)),
            };
            foreach (var (n, pos0, look0) in shots)
            {
                var pos = pos0; var look = look0;
                var ter = Terrain.activeTerrain;
                if (ter) { pos.y += ter.SampleHeight(pos); look.y += ter.SampleHeight(look); }      // heights are above the ground there
                t.position = pos; t.LookAt(look); yield return new WaitForSeconds(1.5f); yield return Shot(n);
            }
            W("levant done"); yield return new WaitForSeconds(1f); Application.Quit();
        }

        IEnumerator Start()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-levant") >= 0) { yield return Levant(); yield break; }
            W("first frame");
            yield return new WaitForSecondsRealtime(0.8f); yield return Shot("a_splash");
            // real loading screen (Boot scene): photograph it mid-way and wait until it has handed over to the lobby
            { float b0 = Time.realtimeSinceStartup; bool mid = false;
              while (FindAnyObjectByType<KryvonBoot>() && Time.realtimeSinceStartup - b0 < 30)
              { if (!mid && Time.realtimeSinceStartup - b0 > 1.5f) { mid = true; yield return Shot("a_splash2");
                    var bt = FindAnyObjectByType<KryvonBoot>(); var cs = new Vector3[4];
                    if (bt) foreach (var gr in bt.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) { gr.rectTransform.GetWorldCorners(cs); W($"boot ui {gr.name} on={gr.isActiveAndEnabled} a={gr.color.a:0.00} canvasAlpha={bt.GetComponent<CanvasGroup>().alpha:0.00} rect=({cs[0].x:0},{cs[0].y:0})-({cs[2].x:0},{cs[2].y:0}) tex={(gr.mainTexture ? gr.mainTexture.width + "x" + gr.mainTexture.height : "-")}"); } }
                yield return null; }
              W($"loading screen done after {Time.realtimeSinceStartup - b0 + 0.8f:0.0}s, scene {SceneManager.GetActiveScene().name}"); }
            for (int i = 0; i < 5; i++) { yield return new WaitForSecondsRealtime(1f); W($"tick {i} fps {1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime):0}"); }
            if (!KryvonLobby.HasProfile)
            {
                var lb0 = FindAnyObjectByType<KryvonLobby>();
                yield return Shot("a0_login"); lb0.SendMessage("AutoLogin"); yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("a1_profile_empty"); lb0.SendMessage("AutoProfile"); yield return new WaitForSecondsRealtime(0.4f); yield return Shot("a2_profile");
                lb0.SendMessage("AutoConfirm"); yield return new WaitForSecondsRealtime(3f); W($"profile created: {KryvonLobby.PlayerName}");
                FindAnyObjectByType<KryvonLobby>()?.SendMessage("OpenAccount"); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("a3_account");
                FindAnyObjectByType<KryvonLobby>()?.SendMessage("AutoCloseAccount"); yield return new WaitForSecondsRealtime(0.4f);
            }
            { var lbc = FindAnyObjectByType<KryvonLobby>(); if (lbc) { yield return Shot("a0_login_returning"); lbc.SendMessage("AutoContinue"); yield return new WaitForSecondsRealtime(0.8f); } }
            { var lbp = FindAnyObjectByType<KryvonLobby>(); if (lbp) { lbp.SendMessage("OpenAccount"); yield return new WaitForSecondsRealtime(0.8f); yield return Shot("a3_card"); lbp.SendMessage("AutoCloseAccount"); yield return new WaitForSecondsRealtime(0.3f); } }
            yield return Shot("a_lobby"); yield return new WaitForSecondsRealtime(0.5f);
            { var lb1 = FindAnyObjectByType<KryvonLobby>(); if (lb1) { lb1.SendMessage("AutoLayout"); yield return new WaitForSecondsRealtime(0.6f); yield return Shot("a4_layout"); lb1.SendMessage("AutoLayoutClose"); yield return new WaitForSecondsRealtime(0.3f); } }
            var lobby = FindAnyObjectByType<KryvonLobby>();
            bool quick = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-quick") >= 0;   // straight to the match
            if (lobby && !quick)
                for (int tab = 0; tab < 3; tab++)
                {
                    lobby.SendMessage("Tab", tab, SendMessageOptions.DontRequireReceiver); yield return new WaitForSecondsRealtime(0.6f);
                    yield return Shot("p_tab" + tab); yield return new WaitForSecondsRealtime(0.4f);
                }
            if (lobby && !quick) { lobby.SendMessage("Tab", -1, SendMessageOptions.DontRequireReceiver); yield return new WaitForSecondsRealtime(0.3f); lobby.SendMessage("SetEmote", 3, SendMessageOptions.DontRequireReceiver); yield return new WaitForSecondsRealtime(1.5f); yield return Shot("r_dance"); lobby.SendMessage("SetEmote", 0, SendMessageOptions.DontRequireReceiver); }
            if (lobby && !quick)
            {   // every store category
                var cat = typeof(KryvonLobby).GetField("storeCat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fill = typeof(KryvonLobby).GetMethod("FillSheet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                lobby.SendMessage("Tab", 1, SendMessageOptions.DontRequireReceiver);
                for (int c = 0; c < 9 && cat != null && fill != null; c++) { cat.SetValue(lobby, c); fill.Invoke(lobby, new object[] { 1 }); yield return new WaitForSecondsRealtime(0.5f); yield return Shot("q_store" + c); }
                lobby.SendMessage("Tab", -1, SendMessageOptions.DontRequireReceiver); yield return new WaitForSecondsRealtime(0.5f);
            }
            if (lobby)
            {
                // a real click through the UI (virtual mouse inside the game, not the OS cursor)
                var startText = System.Array.Find(FindObjectsByType<UnityEngine.UI.Text>(), t => t.text == "READY FOR BATTLE" || t.text.Contains("START") && t.text.Length < 14);
                var btn = startText ? startText.GetComponentInParent<UnityEngine.UI.Button>() : null;
                if (btn)
                {
                    var rt = (RectTransform)btn.transform; Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    W($"START at {sp}, eventsystem {(es ? es.name + " / " + es.currentInputModule : "NONE")}");
                    if (es)
                    {
                        var res = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = sp }, res);
                        W("under the START button: " + string.Join(" > ", res.ConvertAll(r => r.gameObject.name)));
                    }
                    var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("AutoTestMouse");
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = sp }); yield return null; yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = sp }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left)); yield return null; yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = sp }); yield return new WaitForSecondsRealtime(4.5f);
                    bool loading = GameObject.Find("Loading") != null || SceneManager.GetActiveScene().name == "Play";
                    W(loading ? "CLICK on START works" : "CLICK on START DID NOTHING");
                    if (!loading) lobby.SendMessage("StartMatch", SendMessageOptions.DontRequireReceiver);
                }
                else { W("no START button found"); lobby.SendMessage("StartMatch", SendMessageOptions.DontRequireReceiver); }
            }
            else W("no lobby found");
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != "Play" && Time.realtimeSinceStartup - t0 < 300) yield return null;
            W($"Play scene loaded after {Time.realtimeSinceStartup - t0:0.0}s");
            yield return new WaitForSeconds(2f);
            if (KryvonDrop.Instance)
            {
                var d = KryvonDrop.Instance; var seen = new System.Collections.Generic.HashSet<KryvonDrop.Stage>();
                float d0 = Time.realtimeSinceStartup;
                while (d.S != KryvonDrop.Stage.Landed && Time.realtimeSinceStartup - d0 < 150)
                {
                    if (d.S == KryvonDrop.Stage.Freefall && seen.Add((KryvonDrop.Stage)98)) { yield return new WaitForSeconds(1.2f); var hm = d.transform.GetComponentInChildren<Animator>(); W($"freefall body tilt: {(hm ? Vector3.Angle(hm.transform.up, Vector3.up) : -1):0} deg from upright (180 = head down)"); yield return Shot("s_dive"); }
                    if (seen.Add(d.S)) { W($"drop stage {d.S} at {d.transform.position}"); yield return new WaitForSeconds(d.S == KryvonDrop.Stage.Plane ? 2f : 0.6f); yield return Shot("s_" + d.S.ToString().ToLower()); }
                    if (d.S == KryvonDrop.Stage.Chute && d.Height < 40 && seen.Add((KryvonDrop.Stage)99)) yield return Shot("s_chute_low");
                    yield return null;
                }
                W($"drop {(d.S == KryvonDrop.Stage.Landed ? "landed" : "STUCK in " + d.S)} after {Time.realtimeSinceStartup - d0:0}s at {d.transform.position}");
                yield return new WaitForSeconds(2f);
            }
            var player = GameObject.FindGameObjectWithTag("Player");
            W(player ? $"player at {player.transform.position}" : "NO PLAYER");
            yield return Shot("b_spawn");
            // -focus: only landing, crawling in the open and iron-sight aiming (quick checks of the things players complain about)
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-focus") >= 0 && player)
            {
                var fin = player.GetComponent<PlayerInputReader>(); var fpc = player.GetComponent<PlayerController>(); var fwc = player.GetComponent<WeaponController>();
                var sea = GameObject.Find("Sea");
                W($"landing: y {player.transform.position.y:0.00}, sea {(sea ? sea.transform.position.y : -1):0.00}, grounded {fpc.IsGrounded}, swimming {fpc.IsSwimming}");
                yield return new WaitForSeconds(1f); yield return Shot("y_land");
                // iron sights: an assault rifle with no scope
                var ar = KryvonLoot.All.Find(l => l && l.kind == LootKind.Weapon && l.weapon && l.weapon.weaponClass == WeaponClass.AssaultRifle);
                if (ar && fwc) { fwc.Take(ar.weapon, ar.weapon.magazineSize); yield return new WaitForSeconds(1.2f); yield return Shot("y_hip");
                    fin.TouchAim(true); yield return new WaitForSeconds(1.0f); W($"iron sight: {fwc.HeldName} ads={fwc.IsAds} scope={fwc.CurrentScope} fov={Camera.main.fieldOfView:0}"); yield return Shot("y_ironsight");
                    fin.TouchAim(false); yield return new WaitForSeconds(0.6f); }
                // prone + crawl in the open
                fin.TouchProne(); yield return new WaitForSeconds(1.2f); yield return Shot("y_prone");
                var c0 = player.transform.position;
                for (float t = 0; t < 3f; t += Time.deltaTime) { fin.TouchMove(new Vector2(0, 1)); yield return null; if (t > 1.4f && t < 1.45f) yield return Shot("y_crawl"); }
                fin.TouchMove(Vector2.zero);
                W($"crawl: stance {fpc.Stance}, moved {Vector3.Distance(c0, player.transform.position):0.00} m in 3 s, speed now {fpc.PlanarVelocity.magnitude:0.00}");
                var cam = Camera.main.transform; var tpc = cam.GetComponent<ThirdPersonCamera>(); if (tpc) tpc.enabled = false;
                cam.position = player.transform.position + player.transform.right * 3f + Vector3.up * 1.2f; cam.LookAt(player.transform.position + Vector3.up * 0.3f);
                yield return Shot("y_crawl_side");
                W("autotest done"); yield return new WaitForSeconds(1f); Application.Quit(); yield break;
            }
            if (player)
            {   // effects: molotov fire, explosion, smoke wall in front of the camera
                var cam = Camera.main.transform; var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                Vector3 G0(Vector3 p) { if (Physics.Raycast(p + Vector3.up * 30, Vector3.down, out var gh, 60, ~0, QueryTriggerInteraction.Ignore)) p.y = gh.point.y; return p; }
                var a = G0(player.transform.position + fwd * 9f - cam.right * 3f); var b = G0(player.transform.position + fwd * 12f + cam.right * 4f);
                KryvonFX.GroundFire(a, 2.4f, 6f); yield return new WaitForSeconds(1.5f); yield return Shot("x_fire");
                KryvonFX.Explosion(b, 1.1f); yield return new WaitForSeconds(0.12f); yield return Shot("x_boom"); yield return new WaitForSeconds(0.9f); yield return Shot("x_boom_smoke");
                KryvonFX.SmokeScreen(G0(player.transform.position + fwd * 10f), 6f); yield return new WaitForSeconds(3f); yield return Shot("x_smoke");
                yield return new WaitForSeconds(4f);
            }
            if (player && player.GetComponent<PlayerController>() is var spc && spc && spc.WaterY > -1e5f && Terrain.activeTerrain)
            {   // swimming: drop into the sea off the coast, crawl forward, then tread water; then back to where we were
                var ter = Terrain.activeTerrain; Vector3 c0 = ter.transform.position + ter.terrainData.size * 0.5f; c0.y = 0;
                Vector3 back = player.transform.position, dir = Vector3.ProjectOnPlane(back - c0, Vector3.up).normalized; if (dir.sqrMagnitude < 0.1f) dir = Vector3.forward;
                Vector3 sea = Vector3.zero; bool found = false;
                for (float r = 100f; r < 2000f && !found; r += 10f)
                {
                    var q = c0 + dir * r; float h = ter.SampleHeight(q) + ter.transform.position.y;
                    if (h < spc.WaterY - 4f) { sea = q; sea.y = spc.WaterY - 1.2f; found = true; }
                }
                var cc = player.GetComponent<CharacterController>(); var inp = player.GetComponent<PlayerInputReader>();
                if (found && cc && inp)
                {
                    cc.enabled = false; player.transform.position = sea; player.transform.rotation = Quaternion.LookRotation(-dir); cc.enabled = true;
                    yield return new WaitForSeconds(1.5f);
                    inp.TouchMove(Vector2.up); yield return new WaitForSeconds(2.5f);
                    W($"swim: swimming {spc.IsSwimming}, y {player.transform.position.y:0.00} (water {spc.WaterY:0.00}), speed {spc.PlanarVelocity.magnitude:0.0} m/s");
                    yield return Shot("s_swim"); yield return new WaitForSeconds(0.7f); yield return Shot("s_swim2");
                    var sw = player.GetComponent<KryvonSwim>(); W("swim bones: " + (sw ? sw.Debug : "no KryvonSwim"));
                    inp.TouchFreeLook(true); for (int f = 0; f < 20; f++) { inp.TouchLook(new Vector2(40f, 0f)); yield return null; }
                    yield return new WaitForSeconds(0.3f); yield return Shot("s_swim_side"); inp.TouchFreeLook(false);
                    inp.TouchMove(Vector2.zero); yield return new WaitForSeconds(2f); yield return Shot("s_tread");
                    cc.enabled = false; player.transform.position = back + Vector3.up * 0.5f; cc.enabled = true; yield return new WaitForSeconds(1f);
                }
                else W("swim: no sea found off the coast");
            }
            if (player)
            {   // feet: how far the lower sole is from the floor under it (should be ~0)
                var an = player.GetComponentInChildren<Animator>(); var br = player.GetComponent<PlayerAnimatorBridge>();
                var fl = an ? an.GetBoneTransform(HumanBodyBones.LeftFoot) : null; var fr = an ? an.GetBoneTransform(HumanBodyBones.RightFoot) : null;
                if (fl && fr)
                {
                    float G(Vector3 p) { float b = float.NaN; foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.6f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)) if (!h.collider.transform.IsChildOf(player.transform) && (float.IsNaN(b) || h.point.y > b)) b = h.point.y; return b; }
                    W($"feet: left ankle {fl.position.y - G(fl.position):0.000} m, right ankle {fr.position.y - G(fr.position):0.000} m above floor, model shifted {(br ? br.GroundOffset : 0):0.000} m");
                }
            }
            { var tc = FindAnyObjectByType<KryvonTouchControls>(); if (tc && tc.enabled) { tc.SendMessage("AutoOpenMed"); yield return new WaitForSeconds(0.3f); yield return Shot("b_medlist"); tc.SendMessage("CloseLists"); } }
            var input = player ? player.GetComponent<PlayerInputReader>() : null;
            // ---- stairs: walk up one switchback flight pair in the nearest house ----
            if (player && input)
            {
                Transform best = null; float bd = float.MaxValue;
                foreach (var t in FindObjectsByType<Transform>())
                    if (t.name == "StairStart" && t.localPosition.y < 0.5f) { float d = (t.position - player.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = t; } }
                if (best)
                {
                    var pcc = player.GetComponent<CharacterController>(); pcc.enabled = false;
                    var fwd = best.parent.forward; fwd.y = 0; fwd.Normalize();
                    player.transform.SetPositionAndRotation(best.position + Vector3.up * 0.1f, Quaternion.LookRotation(fwd)); pcc.enabled = true;
                    var rig = Camera.main ? Camera.main.GetComponent<ThirdPersonCamera>() : null; if (rig) rig.SetTarget(player.transform);
                    var house = best.parent;
                    string L(Vector3 w) { var l = house.InverseTransformPoint(w); return $"({l.x:0.0},{l.y:0.0},{l.z:0.0})"; }
                    W($"stairs: house {house.name} at {house.position}, mark local {L(best.position)}");
                    yield return new WaitForSeconds(0.5f);
                    W($"stairs: player after 0.5 s at local {L(player.transform.position)}");
                    if (Physics.Raycast(player.transform.position + Vector3.up * 1.0f, fwd, out var blk, 3f, ~0, QueryTriggerInteraction.Ignore))
                        W($"stairs: ahead {blk.distance:0.00} m: {blk.collider.name} ({blk.collider.GetType().Name}) on {blk.collider.transform.parent?.name}/{blk.collider.transform.name}, normal {blk.normal}");
                    else W("stairs: nothing ahead within 3 m");
                    float y0 = player.transform.position.y;
                    // walk it like a player: steer toward points on the stairs (house-local x/z), one after the other
                    var m = house.InverseTransformPoint(best.position); float xa = m.x, xb = m.x + 1.3f, zF = m.z + 0.8f, zL = zF + 3.38f;
                    var wps = new[] { new Vector2(xa, zF + 0.3f), new Vector2(xa, zL + 0.5f), new Vector2(xb, zL + 0.5f), new Vector2(xb, zF - 0.7f) };
                    float yMid = y0;
                    for (int wi = 0; wi < wps.Length; wi++)
                    {
                        for (float t = 0; t < 5f; t += Time.deltaTime)
                        {
                            var target = house.TransformPoint(new Vector3(wps[wi].x, 0, wps[wi].y)); var dir = target - player.transform.position; dir.y = 0;
                            if (dir.magnitude < 0.35f) break;
                            float yaw = rig ? rig.MoveReferenceYaw : player.transform.eulerAngles.y;
                            var local = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * dir.normalized;
                            input.TouchMove(new Vector2(local.x, local.z)); yield return null;
                        }
                        if (wi == 1) { yMid = player.transform.position.y; W($"stairs: at the landing, local {L(player.transform.position)}"); }
                    }
                    input.TouchMove(Vector2.zero); yield return new WaitForSeconds(0.4f);
                    W($"stairs: end at local {L(player.transform.position)}");
                    W($"stairs: landing +{yMid - y0:0.0} m, next floor +{player.transform.position.y - y0:0.0} m (one floor = 3.2 m)");
                    yield return Shot("f_stairs");
                }
                else W("stairs: no StairStart found");
            }
            // ---- wall test: sprint into the back wall of three houses / shacks / warehouses for 2.5 s each – nobody may end up inside ----
            if (player && input)
            {
                var cc = player.GetComponent<CharacterController>(); int through = 0, tested = 0;
                var kinds = new[] { "House", "Shack", "Warehouse" };
                foreach (var kind in kinds)
                {
                    GameObject hs = null; float best = float.MaxValue;
                    foreach (var t in FindObjectsByType<Transform>()) if (t.name == kind) { float d = (t.position - player.transform.position).sqrMagnitude; if (d < best) { best = d; hs = t.gameObject; } }
                    if (!hs) { W($"wall test: no {kind}"); continue; }
                    var rs = hs.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                    var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                    var back = hs.transform.forward; float ext = Mathf.Abs(Vector3.Dot(bb.extents, new Vector3(Mathf.Abs(back.x), 0, Mathf.Abs(back.z)))) ;
                    var start = new Vector3(hs.transform.position.x, hs.transform.position.y + 0.2f, hs.transform.position.z) + back * (ext + 2.5f);
                    if (cc) cc.enabled = false; player.transform.SetPositionAndRotation(start, Quaternion.LookRotation(-back)); if (cc) cc.enabled = true;
                    var rig = FindAnyObjectByType<ThirdPersonCamera>(); if (rig) rig.SendMessage("SnapBehind", SendMessageOptions.DontRequireReceiver);
                    yield return new WaitForSeconds(0.4f);
                    float tw0 = Time.time; while (Time.time - tw0 < 2.5f) { input.TouchMove(Vector2.up); input.TouchSprint(true); yield return null; }
                    input.TouchMove(Vector2.zero); input.TouchSprint(false); yield return new WaitForSeconds(0.3f);
                    var lp = hs.transform.InverseTransformPoint(player.transform.position);
                    var lb = hs.transform.InverseTransformVector(bb.extents); bool inside = Mathf.Abs(lp.x) < Mathf.Abs(lb.x) - 0.6f && Mathf.Abs(lp.z) < Mathf.Abs(lb.z) - 0.6f && lp.y < 3f;
                    tested++; if (inside) through++;
                    W($"wall test {kind}: {(inside ? "WENT THROUGH THE WALL" : "blocked")} (local {lp.x:0.0},{lp.y:0.0},{lp.z:0.0})");
                }
                W($"wall test: {through} of {tested} went through");
                yield return Shot("g_wall");
            }
            // ---- loadout rules: land empty-handed, 2 primaries + pistol + melee, a 3rd primary replaces one and drops it ----
            if (player && input)
            {
                var wc = player.GetComponent<WeaponController>();
                string Kit() { var parts = new System.Collections.Generic.List<string>(); for (int i = 0; i < 4; i++) { var sl = wc.Slot(i); parts.Add($"{WeaponController.SlotNames[i]}={(sl?.data ? sl.data.displayName : "-")}"); } return string.Join(", ", parts) + $" | holding {wc.HeldName}"; }
                W("loadout at landing: " + Kit());
                IEnumerator Pick(System.Func<KryvonLoot, bool> want, string what)
                {
                    KryvonLoot best = null; float bd = float.MaxValue;
                    foreach (var l in KryvonLoot.All) if (l && want(l)) { float d = (l.transform.position - player.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = l; } }
                    if (!best) { W("pickup: no " + what + " found"); yield break; }
                    var pcc = player.GetComponent<CharacterController>(); pcc.enabled = false; player.transform.position = best.transform.position + Vector3.up * 0.1f; pcc.enabled = true;
                    yield return new WaitForSeconds(0.3f); input.TouchInteract(); yield return new WaitForSeconds(0.4f);
                    W($"picked {what} ({(best ? "still on ground!" : "taken")}): " + Kit());
                }
                int dropsBefore = KryvonLoot.All.Count;
                yield return Pick(l => l.kind == LootKind.Weapon && l.weapon && l.weapon.weaponClass == WeaponClass.Melee, "melee");
                for (int k = 0; k < 3; k++)
                    yield return Pick(l => l.kind == LootKind.Weapon && l.weapon && l.weapon.weaponClass != WeaponClass.Melee && l.weapon.weaponClass != WeaponClass.Pistol, "primary " + (k + 1));
                yield return Pick(l => l.kind == LootKind.Weapon && l.weapon && l.weapon.weaponClass == WeaponClass.Pistol, "pistol");
                W("loot items on the map: before " + dropsBefore + ", after " + KryvonLoot.All.Count + " (a replaced weapon is dropped, so the count falls by fewer than the 5 pick-ups)");
            }
            // ---- crouch, peek, scope ----
            if (player && input)
            {
                var pc = player.GetComponent<PlayerController>(); var wc = player.GetComponent<WeaponController>(); var rig = Camera.main.GetComponent<ThirdPersonCamera>();
                input.TouchCrouch(); yield return new WaitForSeconds(0.8f); W($"crouch: stance {pc.Stance}, eye height {pc.EyeHeight:0.00} m, swimming {pc.IsSwimming}, y {player.transform.position.y:0.0}"); yield return Shot("h_crouch");
                input.TouchCrouch(); yield return new WaitForSeconds(0.6f);
                input.TouchLean(1f); yield return new WaitForSeconds(0.7f); W($"peek right: lean {pc.Lean:0.00}"); yield return Shot("i_peek"); input.TouchLean(0f); yield return new WaitForSeconds(0.4f);
                int scopedSlot = -1; for (int i = 0; i < 2; i++) { var sl = wc.Slot(i); if (sl?.data && Scopes.Zoom(sl.scope) >= 2f) scopedSlot = i; }
                if (scopedSlot < 0) { var sl = wc.Slot(0); if (sl?.data) { sl.scope = ScopeType.X4; scopedSlot = 0; } }
                if (scopedSlot >= 0)
                {
                    input.TouchSwitch(scopedSlot); yield return new WaitForSeconds(1f);
                    input.TouchAim(true); yield return null; yield return null; input.TouchAim(false); yield return new WaitForSeconds(0.8f);   // a quick tap = scope in
                    W($"scope: {wc.HeldName} {wc.CurrentScope} ads={wc.IsAds} scoped={rig.IsScoped} fov={Camera.main.fieldOfView:0}"); yield return Shot("j_scope");
                    input.TouchAim(true); yield return null; yield return null; input.TouchAim(false); yield return new WaitForSeconds(0.5f);   // tap again = scope out
                }
            }
            // ---- prone + crawl, climb onto a car ----
            if (player && input)
            {
                var pc = player.GetComponent<PlayerController>();
                input.TouchProne(); yield return new WaitForSeconds(1.2f);
                var p0 = player.transform.position;
                for (float t = 0; t < 1.5f; t += Time.deltaTime) { input.TouchMove(new Vector2(0, 1)); yield return null; }
                input.TouchMove(Vector2.zero);
                W($"prone: stance {pc.Stance}, eye {pc.EyeHeight:0.00} m, crawled {Vector3.Distance(p0, player.transform.position):0.0} m"); yield return Shot("k_prone");
                input.TouchProne(); yield return new WaitForSeconds(1.2f);
                KryvonVehicle car = null; float bd = float.MaxValue;
                foreach (var v in KryvonVehicle.All) if (v && !v.Wrecked) { float d = (v.transform.position - player.transform.position).sqrMagnitude; if (d < bd) { bd = d; car = v; } }
                if (car)
                {
                    var side = car.transform.right; var bx = car.GetComponent<BoxCollider>();
                    float half = bx ? Mathf.Abs(car.transform.TransformVector(new Vector3(bx.size.x / 2, 0, 0)).magnitude) : 1.2f;
                    var stand = (bx ? car.transform.TransformPoint(bx.center) : car.transform.position) + side * (half + 0.9f); stand.y = car.transform.position.y + 1f;
                    if (Physics.Raycast(stand + Vector3.up * 3f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) stand = g.point;
                    var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.SetPositionAndRotation(stand + Vector3.up * 0.05f, Quaternion.LookRotation(-side)); cc.enabled = true;
                    var rig = Camera.main.GetComponent<ThirdPersonCamera>(); rig.SetTarget(player.transform);
                    yield return new WaitForSeconds(0.5f);
                    var mv = KryvonClimb.FindLedge(player.transform, cc); Vector3? ledge = mv.HasValue ? mv.Value.over : (Vector3?)null;
                    float y0 = player.transform.position.y; int c0 = KryvonClimb.Climbs;
                    input.TouchJump(); yield return new WaitForSeconds(1.4f);
                    W($"climb onto {car.displayName}: ledge {(ledge.HasValue ? (ledge.Value.y - y0).ToString("0.0") + " m up" : "none")}, climbed {KryvonClimb.Climbs - c0}x, now +{player.transform.position.y - y0:0.0} m  [{KryvonClimb.LastReason}]");
                    yield return Shot("l_climb");
                }
                else W("climb: no vehicle found");
                // ---- vault through a broken window ----
                KryvonWindow win = null; float wd = float.MaxValue;
                foreach (var wnd in FindObjectsByType<KryvonWindow>()) { float d = (wnd.transform.position - player.transform.position).sqrMagnitude; if (d < wd) { wd = d; win = wnd; } }
                if (win)
                {
                    win.TakeDamage(new DamageInfo { Amount = 999, Source = player });
                    yield return new WaitForSeconds(0.3f);
                    var wb = win.GetComponentInChildren<Renderer>() ? win.GetComponentInChildren<Renderer>().bounds : new Bounds(win.transform.position, Vector3.one);
                    var n = wb.size.x < wb.size.z ? Vector3.right : Vector3.forward;
                    var cc2 = player.GetComponent<CharacterController>(); var rig2 = Camera.main.GetComponent<ThirdPersonCamera>();
                    string res = "";
                    foreach (var side in new[] { 1f, -1f })
                    {
                        var stand = wb.center + n * side * 1.0f; stand.y = wb.min.y + 1.5f;
                        if (Physics.Raycast(stand, Vector3.down, out var gg, 6f, ~0, QueryTriggerInteraction.Ignore)) stand = gg.point;
                        cc2.enabled = false; player.transform.SetPositionAndRotation(stand + Vector3.up * 0.05f, Quaternion.LookRotation(-n * side)); cc2.enabled = true; rig2.SetTarget(player.transform);
                        yield return new WaitForSeconds(0.4f);
                        int v0 = KryvonClimb.Vaults; var before = player.transform.position;
                        var chk = KryvonClimb.FindLedge(player.transform, cc2);
                        input.TouchJump(); yield return new WaitForSeconds(1.3f);
                        res += $" side{side:+0;-0}: {(KryvonClimb.Vaults > v0 ? "VAULTED " + Vector3.Distance(before, player.transform.position).ToString("0.0") + " m" : "no")} [{KryvonClimb.LastReason}]";
                        if (KryvonClimb.Vaults > v0) break;
                    }
                    W("window vault:" + res); yield return Shot("m_vault");
                }
                // ---- fall damage from 8 m ----
                {
                    var hpC = player.GetComponent<Health>(); var cc3 = player.GetComponent<CharacterController>();
                    // open ground: a terrain hit with nothing above it
                    var high = player.transform.position + Vector3.up * 8f;
                    for (int a = 0; a < 36; a++)
                    {
                        var p = player.transform.position + Quaternion.Euler(0, a * 40f, 0) * Vector3.forward * (15f + a * 3f);
                        if (Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out var gh, 200f, ~0, QueryTriggerInteraction.Ignore) && gh.collider is TerrainCollider) { high = gh.point + Vector3.up * 8f; break; }
                    }
                    float hp0 = hpC.Current; cc3.enabled = false; player.transform.position = high; cc3.enabled = true;
                    yield return new WaitForSeconds(2.5f);
                    W($"fall ~8 m: health {hp0:0} -> {hpC.Current:0}");
                    hpC.Heal(100f, 100f);
                }
            }
            // FPS over 5 s
            int frames = 0; float s0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - s0 < 5) { frames++; yield return null; }
            W($"fps {frames / 5f:0.0}");
            if (input)
            {
                var p0 = player.transform.position;
                for (float t = 0; t < 3f; t += Time.deltaTime) { input.TouchMove(new Vector2(0, 1)); yield return null; }
                input.TouchMove(Vector2.zero);
                W($"walked {Vector3.Distance(p0, player.transform.position):0.0} m");
                for (int i = 0; i < 30; i++) { input.TouchLook(new Vector2(12, 0)); yield return null; }
                yield return Shot("c_walk");
                input.TouchFire(true); yield return new WaitForSeconds(1.2f); input.TouchFire(false);
                var w = player.GetComponent<WeaponController>();
                W(w && w.Current ? $"weapon {w.Current.displayName} ammo {w.CurrentSlot.ammo}" : "no weapon");
                yield return Shot("d_fire");
            }
            yield return new WaitForSeconds(20f);
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var b in KryvonBot.All) { var k = b.S.ToString(); counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1; }
            W("bots: " + string.Join(", ", System.Linq.Enumerable.Select(counts, kv => kv.Key + " " + kv.Value)) + $"  ·  player hp {(player ? player.GetComponent<Health>().Current : -1):0}");
            { var dd = KryvonDrop.Instance; var kb = FindAnyObjectByType<KryvonBots>();
              W($"plane: drop {(dd ? dd.S.ToString() : "none")} pathT {(dd ? dd.PathT : -1):0.00} over {(dd ? dd.OverMap(dd.PlanePos) : false)} pos {(dd ? dd.PlanePos : Vector3.zero)} · KryvonBots {(kb ? (kb.isActiveAndEnabled ? "on" : "OFF") : "MISSING")} · timeScale {Time.timeScale}"); }
            yield return Shot("e_bots");
            // Petra: outside, the treasury hall, a tomb (camera only; the rig is paused)
            var petra = GameObject.Find("Petra");
            if (petra)
            {
                var rig2 = FindAnyObjectByType<ThirdPersonCamera>(); if (rig2) rig2.enabled = false;
                var ct = Camera.main.transform; var pt = petra.transform; Camera.main.farClipPlane = 1500;
                (string n, Vector3 at, Vector3 look)[] views = { ("v_petra", new Vector3(0, 14, -62), new Vector3(0, 10, 0)), ("v_petra_plaza", new Vector3(-18, 2, -46), new Vector3(10, 2, -10)), ("v_petra_hall", new Vector3(0, 2.2f, 4), new Vector3(0, 2, 15)), ("v_petra_tomb", new Vector3(-27.5f, 1.8f, -27), new Vector3(-32, 1.6f, -27)) };
                foreach (var (n, at, look) in views) { ct.position = pt.TransformPoint(at); ct.LookAt(pt.TransformPoint(look)); yield return new WaitForSeconds(1.2f); yield return Shot(n); }
                W("petra shots done");
            }
            else W("no Petra in this map");
            // the end-of-match summary (sample numbers), photographed once it has finished counting up
            KryvonMatchSummary.Show(new KryvonMatchSummary.Result { won = false, place = 4, players = 24, kills = 5, damage = 812, headshots = 2, seconds = 1094, coins = 1170,
                rankBefore = KryvonLobby.RankPoints(), rankAfter = KryvonLobby.RankPoints() + 95, killer = "Saqr_77", weapon = "AKM" }, null);
            yield return new WaitForSecondsRealtime(5f); yield return Shot("z_summary");
            W($"bots skill level {KryvonBots.Difficulty} (0 easy, 1 normal, 2 pro)");
            W("autotest done");
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}

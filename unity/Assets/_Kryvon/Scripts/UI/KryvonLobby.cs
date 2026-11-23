// KRAYVON front end: animated splash, then the lobby (3D hero on a stage, hero picker, modes, map, settings),
// then a loading screen into the Play scene. The whole UI is built in code (UGUI) so it needs no prefabs.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kryvon
{
    /// reports where on a UI image it was tapped (local coordinates, centre = 0,0)
    public class MapClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action<Vector2> onClick;
        public void OnPointerClick(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var lp)) onClick?.Invoke(lp);
        }
    }

    public partial class KryvonLobby : MonoBehaviour
    {
        [System.Serializable] public class Hero { public string id, name, role, bio; public GameObject model; }
        [SerializeField] Hero[] heroes;
        [SerializeField] Font display, body, bodyBold;
        [SerializeField] Transform turntable;
        [SerializeField] string playScene = "Play";
        [SerializeField] AudioClip clickSound, ambience;

        static readonly Color Gold = new(0.831f, 0.686f, 0.216f), GoldDeep = new(0.62f, 0.5f, 0.14f), Ink = new(0.04f, 0.045f, 0.055f, 0.78f), Line = new(1f, 1f, 1f, 0.09f), Dim = new(1f, 1f, 1f, 0.62f);
        RectTransform canvas, lobby, splash, loading, settings, comingSoon;
        Text heroName, heroRole, heroBio, modeLabel, loadText, tip; Image loadFill;
        int hero, mode = 2; readonly List<Image> modeChips = new(); readonly List<Image> navMarks = new();
        AudioSource sfx; float spin, spinVel; Sprite round, pill, glow, grad, gradH;
        static readonly string[] Modes = { "BATTLE ROYALE", "RANKED", "CUSTOM MATCH" };
        static readonly string[] Tips = { "Shoot out a window and climb through – doors can be opened and closed with F.", "Every grave holds the fallen player's gear. Press F at the crate.", "Towers have stairs to the roof – the best sniper nests in the map.", "Headshots deal 2.3× damage. The hit marker turns red.", "Petra's Siq is narrow: perfect for an ambush, deadly to cross." };

        // ---------- sprites built in code ----------
        static Sprite MakeRounded(int size, int radius, bool soft = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius))), dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = soft ? Mathf.Clamp01(1f - (d + radius) / radius) : Mathf.Clamp01(0.5f - d);
                t.SetPixel(x, y, new Color(1, 1, 1, soft ? a * a : a));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * (radius + 1));
        }
        static Sprite MakeGradient()
        {
            var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(0, 0, 0, Mathf.Pow(1 - y / 63f, 1.6f)));
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeGradientH()
        {
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(0, 0, 0, Mathf.Pow(1 - x / 63f, 1.4f)));
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f));
        }

        // ---------- tiny UI helpers ----------
        RectTransform Panel(Transform parent, string name, Color c, Sprite s = null)
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
            var i = g.GetComponent<Image>(); i.color = c; i.sprite = s; i.type = s ? Image.Type.Sliced : Image.Type.Simple; i.raycastTarget = false;
            return (RectTransform)g.transform;
        }
        Text Label(Transform parent, string text, Font f, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft)
        {
            var g = new GameObject("Text", typeof(RectTransform), typeof(Text)); g.transform.SetParent(parent, false);
            var t = g.GetComponent<Text>(); t.text = text; t.font = f; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            Fill(t.rectTransform); return t;
        }
        static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        { r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? anchor; r.anchoredPosition = pos; r.sizeDelta = size; return r; }
        static void Fill(RectTransform r, float pad = 0) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.one * pad; r.offsetMax = -Vector2.one * pad; }
        Button Btn(RectTransform r, System.Action onClick)
        {
            var img = r.GetComponent<Image>(); img.raycastTarget = true;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.highlightedColor = new Color(1.25f, 1.2f, 1.1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); cb.fadeDuration = 0.08f; b.colors = cb;
            b.onClick.AddListener(() => { if (sfx && clickSound) sfx.PlayOneShot(clickSound, 0.5f); onClick(); });
            return b;
        }

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false;
            if (ambience) { var a = gameObject.AddComponent<AudioSource>(); a.clip = ambience; a.loop = true; a.volume = 0.3f; a.Play(); }
            AudioListener.volume = PlayerPrefs.GetFloat("kryvon.volume", 0.8f);
            round = MakeRounded(64, 14); pill = MakeRounded(64, 31); glow = MakeRounded(128, 60, true); grad = MakeGradient(); gradH = MakeGradientH();
            if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var cg = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 10;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            canvas = (RectTransform)cg.transform;
            var saved = PlayerPrefs.GetString(KryvonHeroSwitch.Pref, heroes.Length > 0 ? heroes[0].id : "");
            for (int i = 0; i < heroes.Length; i++) if (heroes[i].id == saved) hero = i;
            // the neon lights + light shafts together overload integrated GPUs (driver resets on Intel UHD): weak GPUs skip them
            if (KryvonGraphics.WeakGpu) { var neon = GameObject.Find("Neon"); if (neon) neon.SetActive(false); }
            // the lobby depth of field + bloom reset the Intel UHD driver (event 4101, isolated with -off Post): weak GPUs skip them
            if (KryvonGraphics.WeakGpu)
                foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include))
                    if (v.profile)
                    {
                        if (v.profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var dof)) dof.active = false;
                        if (v.profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bl)) bl.active = false;
                    }
            // four skinned heroes with shadows reset the Intel UHD driver (event 4101): on weak GPUs the teammates are cheaper
            if (KryvonGraphics.WeakGpu && mates != null)
                foreach (var m in mates) if (m)
                {
                    foreach (var r in m.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
                    foreach (var sk in m.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { sk.quality = SkinQuality.Bone2; sk.updateWhenOffscreen = false; }
                    foreach (var an in m.GetComponentsInChildren<Animator>(true)) an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
            BuildLobby(); ApplyTheme(); BuildSettings(); BuildComingSoon(); BuildSheets(); BuildAccountScreens(); BuildLoading(); BuildSplash();
            ShowHero(hero, true); SetTeam(Team); ApplyAvatar();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        // the lobby is a menu: 30 fps is plenty on integrated / mobile GPUs (less heat, less battery, and the Intel UHD
        // driver resets under sustained full load). The match gets the player's frame-rate setting back on scene load.
        void Start() { if (KryvonGraphics.WeakGpu) Application.targetFrameRate = 30; }

        // ---------- splash ----------
        void BuildSplash()
        {
            splash = Panel(canvas, "Splash", Color.black); Fill(splash);
            var cgS = splash.gameObject.AddComponent<CanvasGroup>();
            var art = Resources.Load<Texture2D>("UI/splash");
            if (!art) { var lt = Label(splash, "KRAYVON", display, 230, Gold, TextAnchor.MiddleCenter); var f0 = Panel(splash, "Bar", new Color(1, 1, 1, 0.12f), pill); Place(f0, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(520, 6)); var ff = Panel(f0, "Fill", Gold, pill); ff.anchorMin = Vector2.zero; ff.anchorMax = new Vector2(0, 1); ff.offsetMin = ff.offsetMax = Vector2.zero; StartCoroutine(SplashRun(cgS, ff, null)); return; }
            // 1) the same art blurred and darkened, filling the whole screen (no black bars on any phone shape)
            var blur = new GameObject("Blur", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>(); blur.transform.SetParent(splash, false);
            blur.texture = Resources.Load<Texture2D>("UI/splash_blur") ?? art; blur.raycastTarget = false; if (blur.texture == art) blur.color = new Color(0.45f, 0.45f, 0.45f);
            var bf = blur.GetComponent<AspectRatioFitter>(); bf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; bf.aspectRatio = 1241f / 1268f;
            // 2) the key art itself, whole and undistorted, as big as the screen allows
            var ri = new GameObject("Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>(); ri.transform.SetParent(splash, false); ri.texture = art; ri.raycastTarget = false;
            var fit = ri.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1241f / 1268f;
            // 3) a live red loading bar and spinner laid exactly over the ones painted in the art (pixel rects of the 1241 x 1268 image)
            RectTransform OnArt(string n, float x0, float y0, float x1, float y1, Color c, Sprite sp = null)
            {
                var r = Panel(ri.rectTransform, n, c, sp); const float W = 1241f, H = 1268f;   // the original art size (the import may resize the texture)
                r.anchorMin = new Vector2(x0 / W, 1 - y1 / H); r.anchorMax = new Vector2(x1 / W, 1 - y0 / H); r.offsetMin = r.offsetMax = Vector2.zero; return r;
            }
            var track = OnArt("Track", 327, 1091, 913, 1120, new Color(0.07f, 0.07f, 0.08f, 1f), pill);
            var edge = Panel(track, "Edge", new Color(0.75f, 0.75f, 0.78f, 0.9f), pill); Fill(edge); edge.GetComponent<Image>().fillCenter = false;
            var fillHolder = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); fillHolder.SetParent(track, false); Fill(fillHolder); fillHolder.offsetMin = new Vector2(4, 4); fillHolder.offsetMax = new Vector2(-4, -4);
            var fill = Panel(fillHolder, "Fill", new Color(0.92f, 0.08f, 0.06f), pill); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var gl = Panel(fill, "Glow", new Color(1f, 0.25f, 0.15f, 0.55f), glow); gl.anchorMin = new Vector2(1, 0.5f); gl.anchorMax = new Vector2(1, 0.5f); gl.sizeDelta = new Vector2(60, 34);
            var hot = Panel(fill, "Shine", new Color(1f, 0.7f, 0.6f, 0.55f), pill); hot.anchorMin = new Vector2(0, 0.55f); hot.anchorMax = new Vector2(1, 0.9f); hot.offsetMin = new Vector2(3, 0); hot.offsetMax = new Vector2(-3, 0);
            var disc = OnArt("SpinnerBg", 588, 1138, 654, 1204, new Color(0.04f, 0.04f, 0.05f, 1f), MakeRounded(64, 31));
            var spin = new GameObject("Spinner", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); spin.transform.SetParent(disc, false); spin.sprite = Arc(); spin.raycastTarget = false; spin.color = new Color(1f, 0.2f, 0.15f);
            Fill(spin.rectTransform, 6);
            StartCoroutine(SplashRun(cgS, fill, spin.rectTransform));
        }
        static Sprite Arc()
        {
            int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = x - S / 2f, dy = y - S / 2f, d = Mathf.Sqrt(dx * dx + dy * dy) / (S / 2f), ang = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f;
                float ring = Mathf.Clamp01((0.98f - d) * 30f) * Mathf.Clamp01((d - 0.72f) * 30f); t.SetPixel(x, y, new Color(1, 1, 1, ring * Mathf.Pow(ang / 360f, 1.5f)));   // a tail that fades out
            }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        /// stays up until the lobby is really ready (≥ 2.5 s and the frame rate has settled after shaders and textures load), max 9 s
        IEnumerator SplashRun(CanvasGroup cg, RectTransform fill, RectTransform spinner)
        {
            lobby.GetComponent<CanvasGroup>().alpha = 0;
            bool quick = skipSplash; skipSplash = false;
            float shown = 0, t = 0; int smooth = 0;
            while (!quick)
            {
                float dt = Time.unscaledDeltaTime; t += dt;
                smooth = dt < 0.09f ? smooth + 1 : 0;                     // consecutive smooth frames
                float ready = Mathf.Clamp01(smooth / 20f);
                float target = Mathf.Min(Mathf.Clamp01(t / 2.5f) * 0.92f + ready * 0.08f, t >= 2.5f && smooth >= 20 ? 1f : 0.96f);
                shown = Mathf.MoveTowards(shown, target, dt * 0.9f);
                if (fill) fill.anchorMax = new Vector2(shown, 1);
                if (spinner) spinner.localEulerAngles = new Vector3(0, 0, -t * 360f);
                if ((shown >= 0.999f) || t > 9f) break;
                yield return null;
            }
            if (fill) fill.anchorMax = Vector2.one;
            var lg = lobby.GetComponent<CanvasGroup>(); bool gate = !HasProfile || LoginDue;     // sign-in screen once per launch, right after loading
            if (gate) ShowLogin();
            for (float f = 0; f < 0.7f; f += Time.unscaledDeltaTime) { cg.alpha = 1 - f / 0.7f; if (!gate) lg.alpha = f / 0.7f; yield return null; }
            cg.gameObject.SetActive(false); if (!gate) lg.alpha = 1;
        }

        // ---------- lobby (layout of the owner's mock-up: top tabs, left mode stack + map card, right region strip) ----------
        [SerializeField] Transform[] tagAnchors;          // heads of the squad members on the helipad
        [SerializeField] string[] tagNames;
        [SerializeField] Texture2D[] mapThumbs;
        [SerializeField] string[] mapNames;
        readonly List<(Transform t, RectTransform r)> tags = new();
        readonly List<Image> tabBgs = new(); readonly List<Image> regionFrames = new();
        Image mapCardImg; Text mapCardName; int region;

        Sprite S(Texture2D t) => t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)) : null;

        void BuildLobby()
        {
            lobby = new GameObject("Lobby", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>(); lobby.SetParent(canvas, false); Fill(lobby);
            var C = new Color(0.06f, 0.075f, 0.1f, 0.86f);            // card glass
            var top = Panel(lobby, "TopShade", new Color(0, 0, 0, 0.6f), grad); top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1); top.sizeDelta = new Vector2(0, 190); top.anchoredPosition = Vector2.zero;
            var bot = Panel(lobby, "BottomShade", new Color(0, 0, 0, 0.7f), grad); bot.anchorMin = Vector2.zero; bot.anchorMax = new Vector2(1, 0); bot.pivot = new Vector2(0.5f, 0); bot.sizeDelta = new Vector2(0, 220); bot.localScale = new Vector3(1, -1, 1); bot.anchoredPosition = new Vector2(0, 220);
            var left = Panel(lobby, "LeftShade", new Color(0, 0, 0, 0.5f), gradH); left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0, 1); left.pivot = new Vector2(0, 0.5f); left.sizeDelta = new Vector2(520, 0); left.anchoredPosition = Vector2.zero;
            RectTransform Card(Vector2 anchor, Vector2 pos, Vector2 size, Color? c = null) { var r = Panel(lobby, "Card", c ?? C, round); Place(r, anchor, pos, size, anchor); return r; }
            Text T(RectTransform p, string s, Vector2 anchor, Vector2 pos, Vector2 size, int fs, Color col, TextAnchor a = TextAnchor.MiddleLeft, Font f = null) { var l = Label(p, s, f ?? bodyBold, fs, col, a); Place(l.rectTransform, anchor, pos, size, anchor); return l; }

            // ---------------- TOP LEFT: player card ----------------
            var pcard = Card(new Vector2(0, 1), new Vector2(28, -24), new Vector2(430, 96));
            var pcFrame = Panel(pcard, "Gold", new Color(1f, 0.74f, 0.25f, 0.45f), round); Fill(pcFrame); pcFrame.GetComponent<Image>().fillCenter = false;
            var av = Panel(pcard, "Avatar", new Color(1, 1, 1, 0.08f), round); Place(av, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(74, 74), new Vector2(0, 0.5f));
            var avImg = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); avImg.transform.SetParent(av, false); avImg.raycastTarget = false;
            avImg.texture = Thumb(PlayerPrefs.GetString(KryvonHeroSwitch.Pref, "hamza")); avImg.uvRect = new Rect(0.3f, 0.66f, 0.4f, 0.28f); Fill(avImg.rectTransform, 3);
            lobbyAvatar = avImg;                                       // replaced by the player's own picture (ApplyAvatar)
            var lvTag = Panel(av, "Lv", Gold, round); Place(lvTag, new Vector2(1, 0), new Vector2(6, -6), new Vector2(40, 24), new Vector2(1, 0)); Label(lvTag, PassLevel().ToString(), display, 20, Dark, TextAnchor.MiddleCenter);
            T(pcard, PlayerName, new Vector2(0, 1), new Vector2(100, -10), new Vector2(320, 38), 32, Color.white, TextAnchor.MiddleLeft, display);
            T(pcard, "NASHAMA PASS  ·  SEASON 1", new Vector2(0, 1), new Vector2(100, -48), new Vector2(320, 22), 16, Gold);
            var xp = Panel(pcard, "Xp", new Color(1, 1, 1, 0.12f), pill); Place(xp, new Vector2(0, 1), new Vector2(100, -76), new Vector2(300, 7), new Vector2(0, 1));
            var xpf = Panel(xp, "Fill", Gold, pill); xpf.anchorMin = Vector2.zero; xpf.anchorMax = new Vector2((PassXp() % XpPerLevel) / (float)XpPerLevel, 1); xpf.offsetMin = xpf.offsetMax = Vector2.zero;
            Btn(pcard, OpenAccount);

            // ---------------- TOP CENTRE: logo + status ----------------
            var logo = Label(lobby, "KRAYVON", display, 76, Color.white, TextAnchor.MiddleCenter); Place(logo.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(520, 80), new Vector2(0.5f, 1));
            logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
            var tagm = Panel(lobby, "Tag", Gold, round); Place(tagm, new Vector2(0.5f, 1), new Vector2(206, -30), new Vector2(92, 28), new Vector2(0.5f, 1)); Label(tagm, "MOBILE", display, 20, Dark, TextAnchor.MiddleCenter);
            var stat = Card(new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(360, 32), new Color(0, 0, 0, 0.55f));
            T(stat, "●  OFFLINE  ·  BOTS MATCH  ·  MIDDLE EAST", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350, 30), 17, new Color(0.45f, 0.95f, 0.6f), TextAnchor.MiddleCenter);

            // ---------------- TOP RIGHT: currencies, sound, settings ----------------
            var cur = Card(new Vector2(1, 1), new Vector2(-150, -24), new Vector2(380, 60));
            coinsText = T(cur, "", new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(170, 50), 26, Gold);
            var sep = Panel(cur, "Sep", new Color(1, 1, 1, 0.15f)); Place(sep, new Vector2(0, 0.5f), new Vector2(188, 0), new Vector2(1, 30), new Vector2(0, 0.5f));
            gemsText = T(cur, "", new Vector2(0, 0.5f), new Vector2(204, 0), new Vector2(120, 50), 26, new Color(0.75f, 0.55f, 1f));
            var plus = Panel(cur, "Plus", Gold, pill); Place(plus, new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(34, 34), new Vector2(1, 0.5f)); Label(plus, "+", display, 30, Dark, TextAnchor.MiddleCenter);
            Btn(plus, () => { storeCat = 8; Tab(1); });
            Btn(cur, () => { storeCat = 0; Tab(1); });
            RefreshWallet();
            var tools = Card(new Vector2(1, 1), new Vector2(-28, -24), new Vector2(112, 60));
            var snd = T(tools, AudioListener.volume > 0.01f ? "♪" : "×", new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(48, 50), 30, Color.white, TextAnchor.MiddleCenter);
            var sndBtn = Panel(tools, "Sound", new Color(0, 0, 0, 0.001f)); Place(sndBtn, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(52, 52), new Vector2(0, 0.5f));
            Btn(sndBtn, () => { AudioListener.volume = AudioListener.volume > 0.01f ? 0f : PlayerPrefs.GetFloat("kryvon.volume", 0.8f); snd.text = AudioListener.volume > 0.01f ? "♪" : "×"; });
            var gear = Panel(tools, "Gear", new Color(0, 0, 0, 0.001f)); Place(gear, new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(52, 52), new Vector2(1, 0.5f));
            Label(gear, "⚙", bodyBold, 30, Color.white, TextAnchor.MiddleCenter); Btn(gear, () => Tab(4));

            // ---------------- LEFT: vertical menu (order on screen; ids are the Tab() indices) ----------------
            var navItems = new (string label, int tab)[] { ("☻   CHARACTERS", 5), ("◈   STORE", 1), ("▣   INVENTORY", 0), ("✦   NASHAMA PASS", 2), ("★   MISSIONS", 3), ("⚙   SETTINGS", 4) };
            var byTab = new Image[6];
            for (int i = 0; i < navItems.Length; i++)
            {
                int id = navItems[i].tab;
                var b = Card(new Vector2(0, 1), new Vector2(28, -150 - i * 66), new Vector2(300, 58));
                var lb = Label(b, navItems[i].label, display, 28, Color.white); lb.rectTransform.offsetMin = new Vector2(20, 0);
                byTab[id] = b.GetComponent<Image>(); Btn(b, () => Tab(id));
            }
            tabBgs.Clear(); tabBgs.AddRange(byTab);

            // emotes: stand, ready and the four dances, played on the hero
            var em = Card(new Vector2(0, 1), new Vector2(28, -556), new Vector2(300, 250));
            T(em, "EMOTES", new Vector2(0, 1), new Vector2(16, -8), new Vector2(200, 30), 22, Gold, TextAnchor.MiddleLeft, display);
            string[] emotes = { "STAND", "READY", "SAMBA", "HIP HOP", "BREAKDANCE", "POSE" };
            for (int k = 0; k < emotes.Length; k++)
            {
                int idx = k;
                var e = Panel(em, "Emote", new Color(1, 1, 1, 0.06f), round); Place(e, new Vector2(0, 1), new Vector2(14 + (k % 2) * 140, -46 - (k / 2) * 66), new Vector2(132, 58), new Vector2(0, 1));
                Label(e, emotes[k], bodyBold, 21, k >= 2 ? Gold : Color.white, TextAnchor.MiddleCenter);
                Btn(e, () => SetEmote(idx));
            }

            // ---------------- RIGHT: map card – opens the MODE & MAP screen ----------------
            var mc = Card(new Vector2(1, 0), new Vector2(-28, 196), new Vector2(480, 318));
            var mcf = Panel(mc, "Gold", new Color(1f, 0.74f, 0.25f, 0.45f), round); Fill(mcf); mcf.GetComponent<Image>().fillCenter = false;
            var mapImg = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); mapImg.transform.SetParent(mc, false); mapImg.raycastTarget = false;
            mapImg.texture = Resources.Load<Texture2D>("UI/levant_map"); Place(mapImg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(460, 251), new Vector2(0.5f, 1));
            var mshade = Panel(mapImg.rectTransform, "Shade", new Color(0, 0, 0, 0.8f), grad); mshade.anchorMin = Vector2.zero; mshade.anchorMax = new Vector2(1, 0.5f); mshade.offsetMin = mshade.offsetMax = Vector2.zero; mshade.localScale = new Vector3(1, -1, 1);
            var cls = Panel(mapImg.rectTransform, "Classic", new Color(0.85f, 0.2f, 0.18f), round); Place(cls, new Vector2(0, 0), new Vector2(12, 50), new Vector2(92, 24), new Vector2(0, 0)); Label(cls, "CLASSIC", display, 18, Color.white, TextAnchor.MiddleCenter);
            T(mapImg.rectTransform, "BILAD AL-SHAM", new Vector2(0, 0), new Vector2(12, 12), new Vector2(300, 38), 34, Color.white, TextAnchor.MiddleLeft, display);
            var chg = Panel(mapImg.rectTransform, "Change", Gold, round); Place(chg, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(150, 36), new Vector2(1, 1)); Label(chg, "MODE  &  MAP", display, 22, Dark, TextAnchor.MiddleCenter);
            modeLine = T(mc, "", new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(460, 44), 20, Color.white, TextAnchor.MiddleCenter);
            modeLine.supportRichText = true;
            Btn(mc, () => { BuildModes(); modes.gameObject.SetActive(true); });
            // START
            var start = Panel(lobby, "Start", Gold, round); Place(start, new Vector2(1, 0), new Vector2(-28, 56), new Vector2(480, 120), new Vector2(1, 0));
            var shine = Panel(start, "Shine", new Color(1, 1, 1, 0.25f), round); shine.anchorMin = new Vector2(0, 0.55f); shine.anchorMax = Vector2.one; shine.offsetMin = new Vector2(4, 0); shine.offsetMax = new Vector2(-4, -4);
            startText = Label(start, "▶  START", display, 64, new Color(0.1f, 0.06f, 0.01f), TextAnchor.MiddleCenter); startText.rectTransform.offsetMin = new Vector2(0, 22);
            startSub = Label(start, "MATCHMAKING  ·  CLASSIC  ·  SOLO", bodyBold, 17, new Color(0.25f, 0.15f, 0.04f), TextAnchor.LowerCenter); startSub.rectTransform.offsetMin = new Vector2(0, 12);
            Btn(start, () => { if (!searching) StartCoroutine(Search()); else searching = false; });
            var startGlow = Panel(lobby, "StartGlow", new Color(1f, 0.6f, 0.15f, 0.3f), glow); Place(startGlow, new Vector2(1, 0), new Vector2(0, 30), new Vector2(540, 180), new Vector2(1, 0)); startGlow.SetSiblingIndex(start.GetSiblingIndex());
            StartCoroutine(Pulse(startGlow.GetComponent<Image>()));

            // ---------------- BOTTOM LEFT: squad slots ----------------
            var me = Card(new Vector2(0, 0), new Vector2(28, 28), new Vector2(250, 70));
            var mf = Panel(me, "Gold", new Color(1f, 0.74f, 0.25f, 0.5f), round); Fill(mf); mf.GetComponent<Image>().fillCenter = false;
            T(me, PlayerName, new Vector2(0, 0.5f), new Vector2(16, 10), new Vector2(220, 30), 22, Color.white, TextAnchor.MiddleLeft, display);
            T(me, "LEADER  ·  READY", new Vector2(0, 0.5f), new Vector2(16, -16), new Vector2(220, 22), 15, Gold);
            slotTexts.Clear(); slotFrames.Clear();
            for (int s = 0; s < 3; s++)
            {
                int si = s;
                var slot = Card(new Vector2(0, 0), new Vector2(290 + s * 200, 28), new Vector2(188, 70), new Color(0.06f, 0.075f, 0.1f, 0.6f));
                var fr = Panel(slot, "Frame", new Color(1, 1, 1, 0.15f), round); Fill(fr); fr.GetComponent<Image>().fillCenter = false; slotFrames.Add(fr.GetComponent<Image>());
                var st = T(slot, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 64), 20, Color.white, TextAnchor.MiddleCenter); st.supportRichText = true; slotTexts.Add(st);
                Btn(slot, () => { int cur = TeamMates; SetTeam(si < cur ? (si == 0 ? 0 : 1) : (si == 0 ? 1 : 2)); });
            }
            // squad name tags over the heads in the 3D scene
            if (tagAnchors != null)
                for (int i = 0; i < tagAnchors.Length; i++)
                {
                    var tg = Panel(lobby, "Tag", new Color(0.05f, 0.06f, 0.08f, 0.75f), round); tg.sizeDelta = new Vector2(210, 40); tg.pivot = new Vector2(0.5f, 0);
                    var dot = Panel(tg, "Ready", new Color(0.35f, 0.9f, 0.45f), pill); Place(dot, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(12, 12), new Vector2(0, 0.5f));
                    var tl = Label(tg, i == 0 ? PlayerName : tagNames != null && i < tagNames.Length ? tagNames[i] : "PLAYER", bodyBold, 20, i == 0 ? Gold : Color.white); tl.rectTransform.offsetMin = new Vector2(32, 0);
                    tags.Add((tagAnchors[i], tg));
                }

            // centre: drag hint, hidden hero texts (ShowHero still fills them), tips
            var hint = Card(new Vector2(0.5f, 0), new Vector2(-40, 140), new Vector2(320, 40), new Color(0, 0, 0, 0.45f));
            T(hint, "⟲  DRAG TO TURN YOUR CHARACTER", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(310, 36), 17, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter);
            heroName = Label(lobby, "", display, 1, Color.clear); heroName.gameObject.SetActive(false);
            heroRole = Label(lobby, "", body, 1, Color.clear); heroRole.gameObject.SetActive(false);
            heroBio = Label(lobby, "", body, 1, Color.clear); heroBio.gameObject.SetActive(false);
            tip = Label(lobby, "", body, 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter); Place(tip.rectTransform, new Vector2(0.5f, 0), new Vector2(-40, 104), new Vector2(900, 26));
            StartCoroutine(Tips_());
        }

        // ---------- team (AI teammates) + perspective + landing zone ----------
        [SerializeField] GameObject[] mates;                   // teammate stands beside the player, shown for DUO / SQUAD
        Text modeLine; RectTransform modes, chars; readonly List<Text> slotTexts = new(); readonly List<Image> slotFrames = new();
        static readonly string[] TeamNames = { "SOLO", "DUO", "SQUAD" }, MateNames = { "ZAIN", "LAYLA", "OMAR" };
        // landing zones = Spawn_0.. in the match scene; positions on the map image (0..1, top-left origin)
        static readonly (string name, float u, float v)[] Zones =
        {
            ("OLD DAMASCUS", 0.483f, 0.406f), ("PETRA", 0.753f, 0.834f), ("HELIOPOLIS", 0.213f, 0.319f), ("AMMAN DOWNTOWN", 0.795f, 0.332f), ("THE OUTSKIRTS", 0.291f, 0.688f),
            ("BEIRUT PORT", 0.245f, 0.176f), ("OLD JERUSALEM", 0.107f, 0.602f), ("ALEPPO CITADEL", 0.547f, 0.080f), ("AL SALT", 0.682f, 0.443f), ("CEDAR HILLS", 0.767f, 0.104f),
        };
        static int Team => Mathf.Clamp(PlayerPrefs.GetInt("kryvon.team", 0), 0, 2);
        static int TeamMates => new[] { 0, 1, 3 }[Team];
        static bool Fpp => PlayerPrefs.GetInt("kryvon.fpp", 0) == 1;
        void SetTeam(int t)
        {
            PlayerPrefs.SetInt("kryvon.team", Mathf.Clamp(t, 0, 2)); PlayerPrefs.Save();
            int n = TeamMates;
            if (mates != null) for (int k = 0; k < mates.Length; k++) if (mates[k]) mates[k].SetActive(k < n);
            for (int k = 0; k < slotTexts.Count; k++)
            {
                bool on = k < n;
                slotTexts[k].text = on ? $"{MateNames[k]}\n<size=13><color=#FFC851>AI TEAMMATE · READY</color></size>" : "+  ADD\n<size=13>AI TEAMMATE</size>";
                slotTexts[k].color = on ? Color.white : new Color(1, 1, 1, 0.55f);
                slotFrames[k].color = on ? new Color(1f, 0.74f, 0.25f, 0.6f) : new Color(1, 1, 1, 0.15f);
            }
            RefreshMode();
        }
        void RefreshMode()
        {
            int z = Mathf.Clamp(PlayerPrefs.GetInt("kryvon.region", 0), 0, Zones.Length - 1);
            if (modeLine) modeLine.text = $"<color=#FFC851>LAND IN</color>  {Zones[z].name}   ·   {TeamNames[Team]}   ·   {(Fpp ? "FPP" : "TPP")}";
            if (startSub && !searching) startSub.text = $"CLASSIC  ·  {TeamNames[Team]}  ·  {(Fpp ? "FPP" : "TPP")}";
        }

        // ---------- MODE & MAP screen (like the big games: pick the map, the landing zone, team size and view) ----------
        void BuildModes()
        {
            if (modes) { Destroy(modes.gameObject); }
            modes = Panel(canvas, "Modes", new Color(0.02f, 0.025f, 0.035f, 0.97f)); Fill(modes); modes.GetComponent<Image>().raycastTarget = true;
            Text L(RectTransform p, string t, Vector2 anchor, Vector2 pos, Vector2 size, int fs, Color c, TextAnchor a = TextAnchor.MiddleLeft, Font f = null) { var l = Label(p, t, f ?? bodyBold, fs, c, a); Place(l.rectTransform, anchor, pos, size, anchor); l.supportRichText = true; return l; }
            L(modes, "SELECT MODE", new Vector2(0, 1), new Vector2(60, -30), new Vector2(600, 70), 60, Color.white, TextAnchor.MiddleLeft, display);
            var back = Panel(modes, "Back", new Color(1, 1, 1, 0.08f), round); Place(back, new Vector2(1, 1), new Vector2(-50, -36), new Vector2(170, 56), new Vector2(1, 1));
            Label(back, "✕  BACK", display, 28, Color.white, TextAnchor.MiddleCenter); Btn(back, () => modes.gameObject.SetActive(false));

            // left: the map list
            var m1 = Panel(modes, "MapCard", new Color(1f, 0.74f, 0.25f, 0.18f), round); Place(m1, new Vector2(0, 1), new Vector2(60, -130), new Vector2(360, 230), new Vector2(0, 1));
            var mf = Panel(m1, "Frame", Gold, round); Fill(mf); mf.GetComponent<Image>().fillCenter = false;
            var th = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); th.transform.SetParent(m1, false); th.texture = Resources.Load<Texture2D>("UI/levant_map"); th.raycastTarget = false;
            Place(th.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(344, 150), new Vector2(0.5f, 1)); th.uvRect = new Rect(0.05f, 0.05f, 0.9f, 0.9f);
            L(m1, "BILAD AL-SHAM", new Vector2(0, 0), new Vector2(14, 34), new Vector2(330, 36), 34, Gold, TextAnchor.MiddleLeft, display);
            L(m1, "2.5 × 1.4 KM  ·  UP TO 40 PLAYERS  ·  CLASSIC", new Vector2(0, 0), new Vector2(14, 10), new Vector2(330, 24), 16, Dim);
            var m2 = Panel(modes, "More", new Color(1, 1, 1, 0.04f), round); Place(m2, new Vector2(0, 1), new Vector2(60, -376), new Vector2(360, 90), new Vector2(0, 1));
            L(m2, "MORE MAPS\n<size=16>COMING WITH THE NEXT SEASON</size>", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 80), 28, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter, display);

            // centre: the big map with clickable landing zones
            const float MW = 900, MH = 491;
            var map = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); map.transform.SetParent(modes, false); map.texture = Resources.Load<Texture2D>("UI/levant_map"); map.raycastTarget = false;
            Place(map.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-40, 30), new Vector2(MW, MH));
            var mframe = Panel(map.rectTransform, "Frame", new Color(1, 1, 1, 0.25f), round); Fill(mframe, -3); mframe.GetComponent<Image>().fillCenter = false;
            var zoneTitle = L(modes, "", new Vector2(0.5f, 0.5f), new Vector2(-40, -MH / 2 - 10), new Vector2(MW, 50), 30, Color.white, TextAnchor.MiddleCenter, display);
            var dots = new List<(Image dot, Image ring)>(); var rows = new List<System.Action>(); Text info = null;
            // no dots on the map: tap anywhere, the nearest landing zone is picked and one marker shows it
            var mark = Panel(map.rectTransform, "Mark", Gold, pill); mark.anchorMin = mark.anchorMax = new Vector2(0, 1); mark.sizeDelta = new Vector2(44, 44); mark.GetComponent<Image>().fillCenter = false;
            var markDot = Panel(mark, "Dot", Gold, pill); Place(markDot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14));
            dots.Add((markDot.GetComponent<Image>(), mark.GetComponent<Image>()));
            map.raycastTarget = true;
            map.gameObject.AddComponent<MapClick>().onClick = local =>
            {
                float u = local.x / MW + 0.5f, v = 0.5f - local.y / MH; int best = 0; float bd = float.MaxValue;
                for (int z = 0; z < Zones.Length; z++) { float d = (Zones[z].u - u) * (Zones[z].u - u) * 3.3f + (Zones[z].v - v) * (Zones[z].v - v); if (d < bd) { bd = d; best = z; } }
                if (sfx && clickSound) sfx.PlayOneShot(clickSound, 0.5f);
                PlayerPrefs.SetInt("kryvon.region", best); Refresh();
            };

            // right: team, perspective, mode
            float y = -130;
            void Row(string title, string[] opts, System.Func<int> get, System.Action<int> set, bool[] soon = null)
            {
                L(modes, title, new Vector2(1, 1), new Vector2(-50, y), new Vector2(380, 30), 22, Dim, TextAnchor.MiddleLeft);
                float w = (380 - (opts.Length - 1) * 10) / opts.Length; var imgs = new List<(Image i, Text t)>();
                for (int o = 0; o < opts.Length; o++)
                {
                    int oi = o; bool sn = soon != null && soon[o];
                    var chip = Panel(modes, "Opt", new Color(1, 1, 1, 0.07f), round); Place(chip, new Vector2(1, 1), new Vector2(-50 - (opts.Length - 1 - o) * (w + 10), y - 36), new Vector2(w, 64), new Vector2(1, 1));
                    var t = Label(chip, sn ? opts[o] + "\n<size=12>ONLINE · SOON</size>" : opts[o], display, sn ? 22 : 30, Color.white, TextAnchor.MiddleCenter); t.supportRichText = true;
                    imgs.Add((chip.GetComponent<Image>(), t));
                    Btn(chip, () => { if (sn) { zoneTitle.text = "<size=22>RANKED NEEDS ONLINE PLAY – IT COMES WITH THE SERVER UPDATE</size>"; return; } set(oi); Refresh(); });
                }
                rows.Add(() => { int g = get(); for (int k = 0; k < imgs.Count; k++) { bool on = k == g; imgs[k].i.color = on ? Gold : new Color(1, 1, 1, 0.07f); imgs[k].t.color = on ? Dark : (soon != null && soon[k] ? new Color(1, 1, 1, 0.35f) : Color.white); } });
                y -= 130;
            }
            Row("TEAM", TeamNames, () => Team, t => SetTeam(t));
            Row("PERSPECTIVE", new[] { "TPP", "FPP" }, () => Fpp ? 1 : 0, v => { PlayerPrefs.SetInt("kryvon.fpp", v); PlayerPrefs.Save(); RefreshMode(); });
            Row("MODE", new[] { "CLASSIC", "RANKED" }, () => 0, _ => { }, new[] { false, true });
            info = L(modes, "", new Vector2(1, 1), new Vector2(-50, y + 10), new Vector2(380, 120), 18, Dim, TextAnchor.UpperLeft);

            var ok = Panel(modes, "Confirm", Gold, round); Place(ok, new Vector2(1, 0), new Vector2(-50, 50), new Vector2(380, 100), new Vector2(1, 0));
            Label(ok, "CONFIRM", display, 52, Dark, TextAnchor.MiddleCenter); Btn(ok, () => modes.gameObject.SetActive(false));

            void Refresh()
            {
                int z = Mathf.Clamp(PlayerPrefs.GetInt("kryvon.region", 0), 0, Zones.Length - 1);
                mark.anchoredPosition = new Vector2(Zones[z].u * MW, -Zones[z].v * MH);
                zoneTitle.text = $"<color=#FFC851>LANDING ZONE</color>   {Zones[z].name}   <size=20>· tap the map to choose where to land</size>";
                foreach (var r in rows) r();
                info.text = $"{TeamNames[Team]}: " + (Team == 0 ? "you alone against everyone." : Team == 1 ? "you + 1 AI teammate (ZAIN)." : "you + 3 AI teammates (ZAIN, LAYLA, OMAR).")
                          + "\n" + (Fpp ? "FPP: you see through your character's eyes." : "TPP: the camera is over your shoulder.");
                RefreshMode();
            }
            Refresh();
            modes.gameObject.SetActive(false);
        }

        // ---------- CHARACTERS screen: every hero and outfit, preview on the stage, pick or buy ----------
        void BuildChars()
        {
            if (chars) Destroy(chars.gameObject);
            chars = Panel(canvas, "Characters", new Color(0, 0, 0, 0.8f), gradH); chars.anchorMin = Vector2.zero; chars.anchorMax = new Vector2(0, 1); chars.pivot = new Vector2(0, 0.5f); chars.sizeDelta = new Vector2(820, 0); chars.anchoredPosition = Vector2.zero;
            chars.GetComponent<Image>().raycastTarget = true;
            var title = Label(chars, "CHARACTERS", display, 60, Color.white); Place(title.rectTransform, new Vector2(0, 1), new Vector2(50, -30), new Vector2(500, 70), new Vector2(0, 1));
            var back = Panel(chars, "Back", new Color(1, 1, 1, 0.08f), round); Place(back, new Vector2(0, 1), new Vector2(560, -36), new Vector2(170, 56), new Vector2(0, 1));
            Label(back, "✕  BACK", display, 28, Color.white, TextAnchor.MiddleCenter); Btn(back, () => { chars.gameObject.SetActive(false); ShowHero(EquippedIndex(), true); Tab(-1); });
            string eq = PlayerPrefs.GetString(KryvonHeroSwitch.Pref, "hamza");
            for (int k = 0; k < heroes.Length; k++)
            {
                int hi = k; var h = heroes[k];
                int oi = KryvonWallet.OutfitIndex(h.id); bool own = KryvonWallet.OutfitUnlocked(h.id);
                var card = Panel(chars, "Hero", new Color(0.06f, 0.075f, 0.1f, 0.92f), round);
                Place(card, new Vector2(0, 1), new Vector2(50 + (k % 3) * 236, -120 - (k / 3) * 258), new Vector2(224, 250), new Vector2(0, 1));
                var fr = Panel(card, "Frame", h.id == eq ? Gold : new Color(1, 1, 1, 0.12f), round); Fill(fr); fr.GetComponent<Image>().fillCenter = false;
                var pic = new GameObject("Pic", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); pic.transform.SetParent(card, false); pic.raycastTarget = false;
                pic.texture = Thumb(h.id); pic.uvRect = new Rect(0.1f, 0.34f, 0.8f, 0.66f); Place(pic.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(212, 166), new Vector2(0.5f, 1));
                var nm = Label(card, h.name, display, 26, Color.white, TextAnchor.MiddleCenter); Place(nm.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(216, 32), new Vector2(0.5f, 0));
                string state = h.id == eq ? "<color=#FFC851>EQUIPPED</color>" : own ? "OWNED · TAP TO USE" : $"<color=#FFC851>{(KryvonWallet.Outfits[oi].gems ? "◆ " : "● ")}{KryvonWallet.Outfits[oi].price:N0}</color> · STORE";
                var stt = Label(card, state, bodyBold, 16, Dim, TextAnchor.MiddleCenter); stt.supportRichText = true; Place(stt.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(216, 28), new Vector2(0.5f, 0));
                Btn(card, () =>
                {
                    ShowHero(hi);
                    if (!KryvonWallet.OutfitUnlocked(heroes[hi].id)) { chars.gameObject.SetActive(false); storeCat = 2; Tab(1); return; }
                    BuildChars(); chars.gameObject.SetActive(true);
                });
            }
            chars.gameObject.SetActive(false);
        }
        int EquippedIndex() { var id = PlayerPrefs.GetString(KryvonHeroSwitch.Pref, "hamza"); for (int k = 0; k < heroes.Length; k++) if (heroes[k].id == id) return k; return 0; }

        // ---------- emotes + matchmaking ----------
        Text startText, startSub; bool searching;
        void SetEmote(int k)
        {
            if (heroes.Length == 0 || !heroes[hero].model) return;
            foreach (var an in heroes[hero].model.GetComponentsInChildren<Animator>())
            {
                foreach (var p in an.parameters) if (p.name == "Emote") an.SetInteger("Emote", k);
                if (an.layerCount > 1) an.SetLayerWeight(1, k <= 1 ? 1f : 0f);          // rifle held at the ready, free arms to dance
            }
            // the rifle goes away while dancing
            foreach (var t in heroes[hero].model.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Gun_")) t.gameObject.SetActive(k <= 1);
        }
        IEnumerator Search()
        {
            searching = true; float t = 0;
            while (searching && t < 3.2f) { startText.text = $"CANCEL  0:{(int)t:00}"; startSub.text = "SEARCHING FOR PLAYERS IN BILAD AL-SHAM…"; t += Time.unscaledDeltaTime; yield return null; }
            if (!searching) { startText.text = StartLabel; RefreshMode(); yield break; }
            searching = false; StartMatch();
        }

        void Tab(int i)
        {
            for (int k = 0; k < tabBgs.Count; k++) { if (!tabBgs[k]) continue; tabBgs[k].color = k == i ? Gold : new Color(0.06f, 0.075f, 0.1f, 0.86f); tabBgs[k].GetComponentInChildren<Text>().color = k == i ? new Color(0.12f, 0.08f, 0.02f) : Color.white; }
            settings.gameObject.SetActive(i == 4);
            comingSoon.gameObject.SetActive(false);
            if (modes) modes.gameObject.SetActive(false);
            if (i == 5) BuildChars(); if (chars) chars.gameObject.SetActive(i == 5);
            for (int k = 0; k < sheets.Length; k++) sheets[k].gameObject.SetActive(k == i);
            if (i >= 0 && i <= 3) FillSheet(i);
            ThemeTabs(i);
        }
        void SetRegion(int i)
        {
            region = i; PlayerPrefs.SetInt("kryvon.region", i);
            RefreshMode();
            for (int k = 0; k < regionFrames.Count; k++) regionFrames[k].color = k == i ? Gold : new Color(1, 1, 1, 0.2f);
        }
        void LateUpdate()
        {
            var cam = Camera.main; if (!cam) return;
            foreach (var (t, r) in tags)
            {
                if (!t || !t.gameObject.activeInHierarchy) { r.gameObject.SetActive(false); continue; }
                var sp = cam.WorldToScreenPoint(t.position + Vector3.up * 0.35f);
                r.gameObject.SetActive(sp.z > 0);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(lobby, sp, null, out var lp);
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = lp;
            }
        }

        IEnumerator Pulse(Image i) { while (true) { i.color = new Color(1f, 0.6f, 0.15f, 0.22f + 0.16f * Mathf.Sin(Time.time * 2.4f)); yield return null; } }
        IEnumerator Tips_() { int k = Random.Range(0, Tips.Length); while (true) { tip.text = "TIP  ·  " + Tips[k++ % Tips.Length]; yield return new WaitForSeconds(6f); } }

        void SetNav(int i) { for (int k = 0; k < navMarks.Count; k++) { navMarks[k].enabled = k == i; navMarks[k].transform.parent.GetComponentInChildren<Text>().color = k == i ? Color.white : Dim; } }
        void Nav(int i)
        {
            SetNav(i);
            settings.gameObject.SetActive(i == 4);
            comingSoon.gameObject.SetActive(i == 2 || i == 3);
            if (i == 2 || i == 3) comingSoon.GetComponentInChildren<Text>().text = i == 2 ? "ARMORY" : "SHOP";
        }
        void SetMode(int i)
        {
            mode = Mathf.Clamp(i, 0, 2); PlayerPrefs.SetInt("kryvon.queue", mode);
            for (int k = 0; k < modeChips.Count; k++)
            {
                modeChips[k].color = new Color(0.05f, 0.06f, 0.08f, k == mode ? 0.92f : 0.6f);
                var bar = modeChips[k].transform.Find("Sel"); if (bar) bar.gameObject.SetActive(k == mode);
                var txt = modeChips[k].GetComponentInChildren<Text>(); if (txt) txt.color = k == mode ? Gold : Dim;
            }
        }
        void ShowHero(int i, bool instant = false)
        {
            hero = i;
            for (int k = 0; k < heroes.Length; k++) if (heroes[k].model) heroes[k].model.SetActive(k == i);
            if (heroes.Length == 0) return;
            heroName.text = heroes[i].name; heroRole.text = heroes[i].role; heroBio.text = heroes[i].bio;
            int oi = KryvonWallet.OutfitIndex(heroes[i].id);
            if (oi >= 0 && !KryvonWallet.OutfitUnlocked(heroes[i].id))
            {   // a skin you don't own yet: show it, but play as Hamza until it is bought
                var o = KryvonWallet.Outfits[oi];
                heroRole.text = $"LOCKED  ·  {(o.gems ? "◆ " : "● ")}{o.price:N0}  ·  BUY IN THE STORE";
                PlayerPrefs.SetString(KryvonHeroSwitch.Pref, "hamza");
            }
            else PlayerPrefs.SetString(KryvonHeroSwitch.Pref, heroes[i].id);
            if (!instant) spin = 0;
        }

        // ---------- settings ----------
        void BuildSettings()
        {
            settings = Panel(canvas, "Settings", new Color(0.03f, 0.035f, 0.045f, 0.95f), round); Place(settings, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240, 860));
            Label(settings, "SETTINGS", display, 64, Gold, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -18);
            BuildSettingsTabs(() =>
            {
                Row($"GRAPHICS  ·  this device ({KryvonGraphics.DeviceName}) supports up to {KryvonGraphics.Qualities[KryvonGraphics.MaxQuality]}", 250, KryvonGraphics.Qualities, () => KryvonGraphics.Quality, v => KryvonGraphics.Quality = v, KryvonGraphics.MaxQuality);
                Row($"FRAME RATE  ·  screen up to {KryvonGraphics.FrameRates[KryvonGraphics.MaxFps]} FPS", 160, System.Array.ConvertAll(KryvonGraphics.FrameRates, f => f + " FPS"), () => KryvonGraphics.Fps, v => KryvonGraphics.Fps = v, KryvonGraphics.MaxFps);
                Row("STYLE", 70, KryvonGraphics.Styles, () => KryvonGraphics.Style, v => KryvonGraphics.Style = v);
                Row("ZONE COLOUR (COLOUR-BLIND)", -20, KryvonGraphics.ColorBlind, () => KryvonGraphics.Blind, v => KryvonGraphics.Blind = v);
                Slider("MASTER VOLUME", -150, PlayerPrefs.GetFloat("kryvon.volume", 0.8f), v => { AudioListener.volume = v; PlayerPrefs.SetFloat("kryvon.volume", v); });
                Row("PERSPECTIVE  ·  in a match the eye button (V) switches any time", -250, new[] { "THIRD PERSON (TPP)", "FIRST PERSON (FPP)" }, () => ThirdPersonCamera.FPP ? 1 : 0, v => { PlayerPrefs.SetInt("kryvon.fpp", v); PlayerPrefs.Save(); RefreshMode(); });
            });
            var close = Panel(settings, "Close", Gold, round); Place(close, new Vector2(0.5f, 0), new Vector2(170, 30), new Vector2(260, 64));
            var layout = Panel(settings, "Layout", new Color(1, 1, 1, 0.1f), round); Place(layout, new Vector2(0.5f, 0), new Vector2(-170, 30), new Vector2(380, 64));
            Label(layout, "CUSTOMIZE CONTROLS", display, 32, Color.white, TextAnchor.MiddleCenter);
            Btn(layout, () => { settings.gameObject.SetActive(false); lobby.gameObject.SetActive(false); KryvonTouchControls.OpenEditor(bodyBold, () => { lobby.gameObject.SetActive(true); settings.gameObject.SetActive(true); }); });
            Label(close, "DONE", display, 44, new Color(0.14f, 0.08f, 0.01f), TextAnchor.MiddleCenter); Btn(close, () => { SensitivityService.Save(); PlayerPrefs.Save(); Tab(-1); Nav(0); });
            settings.gameObject.SetActive(false);
        }
        void Row(string title, float y, string[] options, System.Func<int> get, System.Action<int> set, int max = 99)
        {
            var l = Label(SP, title, bodyBold, 24, Dim); Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-560, y + 44), new Vector2(700, 30), new Vector2(0, 0.5f));
            var chips = new List<Image>();
            float w = 1120f / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var c = Panel(SP, "Opt", Ink, round); Place(c, new Vector2(0.5f, 0.5f), new Vector2(-560 + i * w, y), new Vector2(w - 10, 56), new Vector2(0, 0.5f));
                bool ok = i <= max;      // options the device can't hold are shown but locked
                Label(c, ok ? options[i] : options[i] + "  ✕", bodyBold, 22, ok ? Color.white : new Color(1, 1, 1, 0.28f), TextAnchor.MiddleCenter);
                chips.Add(c.GetComponent<Image>());
                if (ok) Btn(c, () => { set(idx); for (int k = 0; k < chips.Count; k++) chips[k].color = k == idx ? GoldDeep : Ink; });
            }
            int cur = get(); for (int k = 0; k < chips.Count; k++) chips[k].color = k == cur ? GoldDeep : Ink;
        }
        void Slider(string name, float y, float value, UnityEngine.Events.UnityAction<float> onChange)
        {
            var l = Label(SP, name, bodyBold, 24, Dim); Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-560, y + 34), new Vector2(700, 30), new Vector2(0, 0.5f));
            var bg = Panel(SP, "Track", Line, pill); Place(bg, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(1120, 10));
            var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); fillArea.SetParent(bg, false); Fill(fillArea);
            var fill = Panel(fillArea, "Fill", Gold, pill); Fill(fill);
            var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); handleArea.SetParent(bg, false); Fill(handleArea);
            var handle = Panel(handleArea, "Handle", Color.white, pill); handle.sizeDelta = new Vector2(30, 30);
            var s = bg.gameObject.AddComponent<UnityEngine.UI.Slider>(); s.fillRect = fill; s.handleRect = handle; s.targetGraphic = handle.GetComponent<Image>(); handle.GetComponent<Image>().raycastTarget = true; bg.GetComponent<Image>().raycastTarget = true;
            s.value = value; s.onValueChanged.AddListener(onChange);
        }
        void BuildComingSoon()
        {
            comingSoon = Panel(canvas, "Soon", new Color(0.03f, 0.035f, 0.045f, 0.9f), round); Place(comingSoon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 260));
            Label(comingSoon, "", display, 72, Gold, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -30);
            var s = Label(comingSoon, "Coming in the next update.", body, 26, Dim, TextAnchor.MiddleCenter); s.rectTransform.offsetMin = new Vector2(0, -20);
            var bgBtn = comingSoon.gameObject.AddComponent<Button>(); bgBtn.onClick.AddListener(() => Nav(0)); comingSoon.GetComponent<Image>().raycastTarget = true;
            comingSoon.gameObject.SetActive(false);
        }

        // ---------- inventory · store · missions ----------
        RectTransform[] sheets = new RectTransform[0]; RectTransform[] bodies; Text coinsText, gemsText, storeMsg;
        static readonly Color Dark = new(0.12f, 0.08f, 0.02f);
        void RefreshWallet() { if (coinsText) coinsText.text = "● " + KryvonWallet.Coins.ToString("N0"); if (gemsText) gemsText.text = "◆ " + KryvonWallet.Gems.ToString("N0"); }

        void BuildSheets()
        {
            string[] titles = { "INVENTORY", "STORE", "PASS", "MISSIONS" };
            sheets = new RectTransform[4]; bodies = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var p = Panel(canvas, "Sheet_" + titles[i], new Color(0.03f, 0.035f, 0.045f, 0.96f), round); Place(p, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 880));
                var edge = Panel(p, "Neon", new Color(0.62f, 0.3f, 1f, 0.9f), pill); edge.anchorMin = new Vector2(0.05f, 1); edge.anchorMax = new Vector2(0.95f, 1); edge.sizeDelta = new Vector2(0, 3); edge.anchoredPosition = new Vector2(0, -2);
                p.GetComponent<Image>().raycastTarget = true;
                Label(p, titles[i], display, 64, Gold, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -16);
                var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>(); body.SetParent(p, false); Fill(body);
                var close = Panel(p, "Close", Gold, round); Place(close, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(260, 64));
                Label(close, "DONE", display, 44, Dark, TextAnchor.MiddleCenter); Btn(close, () => { PlayerPrefs.Save(); Tab(-1); });
                if (i < 3)
                {   // store, inventory and the pass are full screen with their own header, categories and close button
                    Fill(p); p.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
                    foreach (var t in p.GetComponentsInChildren<Text>()) if (t.text == titles[i]) t.text = "";
                    close.gameObject.SetActive(false);
                }
                p.gameObject.SetActive(false); sheets[i] = p; bodies[i] = body;
            }
        }

        RectTransform Card(RectTransform parent, Vector2 pos, Vector2 size, bool on, System.Action click)
        {
            var c = Panel(parent, "Card", on ? GoldDeep : Ink, round); Place(c, new Vector2(0.5f, 0.5f), pos, size);
            if (click != null) Btn(c, () => { click(); RefreshWallet(); });
            return c;
        }
        void Txt(RectTransform parent, string t, int size, Color c, Vector2 pos, Vector2 box, TextAnchor a = TextAnchor.MiddleCenter, Font f = null)
        { var l = Label(parent, t, f ?? bodyBold, size, c, a); Place(l.rectTransform, new Vector2(0.5f, 0.5f), pos, box); }
        void Swatch(RectTransform card, int chute, Vector2 pos, float w)
        {
            var ch = KryvonWallet.Chutes[chute];
            for (int k = 0; k < 6; k++) { var s = Panel(card, "Cell", k % 2 == 0 ? ch.a : ch.b); Place(s, new Vector2(0.5f, 0.5f), pos + new Vector2((k - 2.5f) * w / 6f, 0), new Vector2(w / 6f + 0.5f, 30)); }
        }

        void FillSheet(int i)
        {
            var body = bodies[i];
            foreach (Transform t in body) Destroy(t.gameObject);
            RefreshWallet();
            if (i == 0) InventoryPage(body); else if (i == 1) StorePage(body); else if (i == 2) PassPage(body); else FillMissions(body);
        }

        void FillInventory(RectTransform b)
        {
            Txt(b, "CHARACTER", 26, Dim, new Vector2(-420, 300), new Vector2(400, 34), TextAnchor.MiddleLeft);
            int n = heroes.Length; float w = Mathf.Min(300, 1240f / Mathf.Max(1, n));
            for (int k = 0; k < n; k++)
            {
                int idx = k; float x = -620 + w * 0.5f + k * w;
                var c = Card(b, new Vector2(x, 220), new Vector2(w - 14, 110), k == hero, () => { ShowHero(idx); FillSheet(0); });
                Txt(c, heroes[k].name, 40, k == hero ? Dark : Color.white, new Vector2(0, 18), new Vector2(w - 30, 46), TextAnchor.MiddleCenter, display);
                Txt(c, heroes[k].role, 15, k == hero ? Dark : Dim, new Vector2(0, -26), new Vector2(w - 30, 40));
            }
            Txt(b, "PARACHUTE  ·  tap to equip (buy more in the STORE)", 26, Dim, new Vector2(-220, 120), new Vector2(800, 34), TextAnchor.MiddleLeft);
            int m = KryvonWallet.Chutes.Length; float cw = 1240f / m;
            for (int k = 0; k < m; k++)
            {
                int idx = k; bool own = KryvonWallet.Owns(k), on = KryvonWallet.Equipped == k;
                var c = Card(b, new Vector2(-620 + cw * 0.5f + k * cw, 30), new Vector2(cw - 14, 130), on, own ? () => { KryvonWallet.Equipped = idx; FillSheet(0); } : () => Tab(1));
                Swatch(c, k, new Vector2(0, 26), cw - 50);
                Txt(c, KryvonWallet.Chutes[k].name, 20, on ? Dark : own ? Color.white : Dim, new Vector2(0, -18), new Vector2(cw - 24, 30));
                Txt(c, on ? "EQUIPPED" : own ? "OWNED" : "LOCKED", 15, on ? Dark : own ? Gold : Dim, new Vector2(0, -46), new Vector2(cw - 24, 24));
            }
            Txt(b, "STARTING KIT", 26, Dim, new Vector2(-420, -110), new Vector2(400, 34), TextAnchor.MiddleLeft);
            Txt(b, "M4A1 assault rifle (red dot)  ·  L96A1 sniper (4x)  ·  600 x 5.56  ·  60 x .300\nEverything else – armour, backpacks, healing, grenades, 22 weapons – is looted on the map.", 22, Color.white, new Vector2(0, -175), new Vector2(1240, 80), TextAnchor.UpperLeft);
            Txt(b, $"MATCHES {KryvonWallet.Stat("matches")}   ·   LANDINGS {KryvonWallet.Stat("landings")}   ·   KILLS {KryvonWallet.Stat("kills")}   ·   ITEMS LOOTED {KryvonWallet.Stat("loot")}", 22, Gold, new Vector2(0, -280), new Vector2(1240, 34));
        }

        int storePage;
        void FillStore(RectTransform b)
        {
            for (int t = 0; t < 2; t++)
            {
                int idx = t; var tab = Card(b, new Vector2(-150 + t * 300, 300), new Vector2(280, 52), storePage == t, () => { storePage = idx; FillSheet(1); });
                Txt(tab, t == 0 ? "OUTFITS" : "PARACHUTES", 26, storePage == t ? Dark : Color.white, Vector2.zero, new Vector2(270, 44), TextAnchor.MiddleCenter, display);
            }
            if (storePage == 0) { FillOutfits(b); return; }
            for (int k = 0; k < KryvonWallet.Chutes.Length; k++)
            {
                int idx = k; var ch = KryvonWallet.Chutes[k]; bool own = KryvonWallet.Owns(k), on = KryvonWallet.Equipped == k;
                float x = -420 + (k % 3) * 420, y = 160 - (k / 3) * 250;
                var c = Card(b, new Vector2(x, y), new Vector2(400, 230), false, null);
                Swatch(c, k, new Vector2(0, 66), 340);
                Txt(c, ch.name, 34, Color.white, new Vector2(0, 14), new Vector2(380, 44), TextAnchor.MiddleCenter, display);
                Txt(c, ch.price == 0 ? "FREE" : (ch.gems ? "◆ " : "● ") + ch.price.ToString("N0"), 22, ch.gems ? new Color(0.75f, 0.55f, 1f) : Gold, new Vector2(0, -24), new Vector2(380, 30));
                var btn = Panel(c, "Buy", on ? new Color(1, 1, 1, 0.12f) : own ? new Color(1, 1, 1, 0.22f) : Gold, round); Place(btn, new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(240, 52));
                Label(btn, on ? "EQUIPPED" : own ? "EQUIP" : "BUY", display, 32, own ? Color.white : Dark, TextAnchor.MiddleCenter);
                Btn(btn, () =>
                {
                    if (KryvonWallet.Owns(idx)) KryvonWallet.Equipped = idx;
                    else if (!KryvonWallet.Buy(idx)) { FillSheet(1); storeMsg.text = KryvonWallet.Chutes[idx].gems ? "NOT ENOUGH GEMS – finish missions to earn more" : "NOT ENOUGH COINS – finish missions to earn more"; return; }
                    FillSheet(1); storeMsg.text = KryvonWallet.Chutes[idx].name + " EQUIPPED – you will see it on your next drop";
                    RefreshWallet();
                });
            }
            var msg = Label(b, "", bodyBold, 24, Gold, TextAnchor.MiddleCenter); Place(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1240, 36)); storeMsg = msg;
        }

        void FillOutfits(RectTransform b)
        {
            for (int k = 0; k < KryvonWallet.Outfits.Length; k++)
            {
                int idx = k; var o = KryvonWallet.Outfits[k]; bool own = KryvonWallet.OutfitUnlocked(o.id);
                var c = Card(b, new Vector2(-420 + k * 420, 20), new Vector2(400, 440), false, null);
                var col = o.rarity == "LEGENDARY" ? Gold : o.rarity == "EPIC" ? new Color(0.72f, 0.42f, 1f) : new Color(0.3f, 0.62f, 1f);
                var band = Panel(c, "Rarity", col, pill); Place(band, new Vector2(0.5f, 0.5f), new Vector2(0, 196), new Vector2(380, 6));
                Txt(c, o.rarity, 20, col, new Vector2(0, 168), new Vector2(380, 30));
                Txt(c, o.name, 46, Color.white, new Vector2(0, 40), new Vector2(380, 60), TextAnchor.MiddleCenter, display);
                Txt(c, "Outfit for HAMZA", 20, Dim, new Vector2(0, 0), new Vector2(380, 30));
                Txt(c, (o.gems ? "◆ " : "● ") + o.price.ToString("N0"), 30, o.gems ? new Color(0.75f, 0.55f, 1f) : Gold, new Vector2(0, -70), new Vector2(380, 40));
                var btn = Panel(c, "Buy", own ? new Color(1, 1, 1, 0.18f) : Gold, round); Place(btn, new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(260, 60));
                Label(btn, own ? "OWNED · WEAR" : "BUY", display, 34, own ? Color.white : Dark, TextAnchor.MiddleCenter);
                Btn(btn, () =>
                {
                    if (!KryvonWallet.BuyOutfit(idx)) { FillSheet(1); storeMsg.text = KryvonWallet.Outfits[idx].gems ? "NOT ENOUGH GEMS" : "NOT ENOUGH COINS"; return; }
                    int hi = System.Array.FindIndex(heroes, h => h.id == KryvonWallet.Outfits[idx].id); if (hi >= 0) ShowHero(hi);
                    FillSheet(1); storeMsg.text = KryvonWallet.Outfits[idx].name + " — you are wearing it now"; RefreshWallet();
                });
            }
            var msg = Label(b, "", bodyBold, 24, Gold, TextAnchor.MiddleCenter); Place(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(1240, 36)); storeMsg = msg;
        }

        void FillMissions(RectTransform b)
        {
            Txt(b, "Finish missions in matches, then claim the reward here", 22, Dim, new Vector2(0, 300), new Vector2(1240, 34));
            for (int k = 0; k < KryvonWallet.Missions.Length; k++)
            {
                int idx = k; var m = KryvonWallet.Missions[k];
                int have = Mathf.Min(KryvonWallet.Stat(m.stat), m.goal); bool done = have >= m.goal, claimed = KryvonWallet.Claimed(k);
                float y = 220 - k * 92;
                var row = Card(b, new Vector2(0, y), new Vector2(1240, 80), false, null);
                Txt(row, m.title, 26, claimed ? Dim : Color.white, new Vector2(-360, 12), new Vector2(480, 34), TextAnchor.MiddleLeft);
                var track = Panel(row, "Track", Line, pill); Place(track, new Vector2(0.5f, 0.5f), new Vector2(-360, -20), new Vector2(480, 10));
                var fill = Panel(track, "Fill", done ? new Color(0.4f, 0.9f, 0.45f) : Gold, pill); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2((float)have / m.goal, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
                Txt(row, $"{have} / {m.goal}", 22, Dim, new Vector2(40, 0), new Vector2(160, 34));
                Txt(row, $"● {m.coins:N0}" + (m.gems > 0 ? $"   ◆ {m.gems}" : ""), 24, Gold, new Vector2(250, 0), new Vector2(240, 34));
                var btn = Panel(row, "Claim", claimed ? new Color(1, 1, 1, 0.08f) : done ? Gold : new Color(1, 1, 1, 0.14f), round); Place(btn, new Vector2(0.5f, 0.5f), new Vector2(510, 0), new Vector2(190, 56));
                Label(btn, claimed ? "CLAIMED" : done ? "CLAIM" : "IN PROGRESS", display, claimed || done ? 32 : 24, done && !claimed ? Dark : Dim, TextAnchor.MiddleCenter);
                if (done && !claimed) Btn(btn, () => { KryvonWallet.Claim(idx); FillSheet(3); });
            }
        }

        // ================= STORE + INVENTORY (full screen): categories left · item grid centre · preview right =================
        static readonly string[] StoreCats = { "FEATURED", "WEAPON SKINS", "OUTFITS", "PARACHUTES", "BACKPACKS", "HELMETS", "VEHICLES", "CRATES", "TOP-UP" };
        static readonly string[] InvCats = { "CHARACTERS", "OUTFITS", "WEAPON SKINS", "PARACHUTES", "BACKPACKS", "HELMETS", "VEHICLES" };
        static readonly Color Bg = new(0.071f, 0.071f, 0.071f, 1f), CardBg = new(0.118f, 0.118f, 0.118f, 1f), CardSel = new(0.17f, 0.175f, 0.195f, 1f), Hair = new(1f, 1f, 1f, 0.07f);
        static Color RarityCol(string r) => r == "LEGENDARY" ? new Color(1f, 0.74f, 0.25f) : r == "EPIC" ? new Color(0.68f, 0.46f, 1f) : r == "RARE" ? new Color(0.36f, 0.62f, 1f) : new Color(0.72f, 0.74f, 0.78f);
        int storeCat, invCat, selItem; string toast = "";

        class Item
        {
            public string id, name, rarity, desc, kind; public Cos cos; public int price; public bool gems, owned, equipped; public Texture2D thumb; public Color a, b;
            public System.Func<bool> buy; public System.Action equip;
        }
        static readonly Dictionary<string, Texture2D> thumbs = new();
        static Texture2D Thumb(string id) { if (!thumbs.TryGetValue(id, out var t)) thumbs[id] = t = Resources.Load<Texture2D>("UI/Outfits/" + id); return t; }

        List<Item> OutfitItems(bool ownedOnly)
        {
            var list = new List<Item>();
            string eq = PlayerPrefs.GetString(KryvonHeroSwitch.Pref, "sara");
            foreach (var hh in heroes)
            {
                var hid = hh.id; if (KryvonWallet.OutfitIndex(hid) >= 0) continue;
                int hi = System.Array.FindIndex(heroes, h => h.id == hid); if (hi < 0) continue;
                list.Add(new Item { id = hid, kind = "outfit", name = heroes[hi].name, rarity = "CLASSIC", desc = heroes[hi].bio, owned = true, equipped = eq == hid, thumb = Thumb(hid), equip = () => ShowHero(hi) });
            }
            for (int k = 0; k < KryvonWallet.Outfits.Length; k++)
            {
                int idx = k; var o = KryvonWallet.Outfits[k]; bool own = KryvonWallet.OutfitUnlocked(o.id);
                if (ownedOnly && !own) continue;
                int hi = System.Array.FindIndex(heroes, h => h.id == o.id);
                list.Add(new Item { id = o.id, kind = "outfit", name = o.name, rarity = o.rarity, desc = hi >= 0 ? heroes[hi].bio : "", price = o.price, gems = o.gems, owned = own, equipped = eq == o.id, thumb = Thumb(o.id),
                    buy = () => KryvonWallet.BuyOutfit(idx), equip = () => { if (hi >= 0) ShowHero(hi); } });
            }
            return list;
        }
        List<Item> ChuteItems(bool ownedOnly)
        {
            var list = new List<Item>();
            for (int k = 0; k < KryvonWallet.Chutes.Length; k++)
            {
                int idx = k; var c = KryvonWallet.Chutes[k]; bool own = KryvonWallet.Owns(k);
                if (ownedOnly && !own) continue;
                string r = c.price == 0 ? "CLASSIC" : c.gems ? (c.price >= 150 ? "LEGENDARY" : "EPIC") : c.price >= 1500 ? "EPIC" : "RARE";
                list.Add(new Item { id = "chute" + k, kind = "chute", name = c.name, rarity = r, desc = "Parachute canopy. You will see it on your next drop.", price = c.price, gems = c.gems, owned = own, equipped = KryvonWallet.Equipped == k, a = c.a, b = c.b,
                    buy = () => KryvonWallet.Buy(idx), equip = () => KryvonWallet.Equipped = idx });
            }
            return list;
        }

        // ---------- building blocks ----------
        RectTransform Box(RectTransform parent, float x, float y, float w, float h, Color c, Sprite s = null)
        { var r = Panel(parent, "Box", c, s ?? round); Place(r, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), new Vector2(0, 1)); return r; }
        Text TextAt(RectTransform parent, string t, float x, float y, float w, float h, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, Font f = null)
        { var l = Label(parent, t, f ?? bodyBold, size, c, a); Place(l.rectTransform, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), new Vector2(0, 1)); return l; }
        void Price(RectTransform parent, Item it, float x, float y, float w, int size)
        {
            if (it.owned) { TextAt(parent, it.equipped ? "EQUIPPED" : "OWNED", x, y, w, size + 8, size, it.equipped ? Gold : Dim, TextAnchor.MiddleCenter); return; }
            if (it.price == 0) { TextAt(parent, "FREE", x, y, w, size + 8, size, Color.white, TextAnchor.MiddleCenter); return; }
            TextAt(parent, (it.gems ? "◆ " : "● ") + it.price.ToString("N0"), x, y, w, size + 8, size, it.gems ? new Color(0.72f, 0.56f, 1f) : Gold, TextAnchor.MiddleCenter);
        }
        void ItemArt(RectTransform parent, Item it, float x, float y, float w, float h)
        {
            if (it.kind == "chute")
            {   // a canopy drawn from its two colours
                var dome = Box(parent, x + w * 0.1f, y + h * 0.18f, w * 0.8f, h * 0.34f, new Color(0, 0, 0, 0), pill);
                for (int k = 0; k < 7; k++) { var cell = Panel(dome, "Cell", k % 2 == 0 ? it.a : it.b); Place(cell, new Vector2(0, 1), new Vector2(k * w * 0.8f / 7f, 0), new Vector2(w * 0.8f / 7f + 0.5f, h * 0.34f - Mathf.Abs(k - 3) * h * 0.035f), new Vector2(0, 1)); }
                for (int k = 0; k < 4; k++) { var cord = Panel(parent, "Cord", new Color(1, 1, 1, 0.25f)); Place(cord, new Vector2(0, 1), new Vector2(x + w * (0.18f + k * 0.21f), -(y + h * 0.5f)), new Vector2(1.5f, h * 0.3f), new Vector2(0, 1)); }
                return;
            }
            if (!it.thumb) return;
            var img = new GameObject("Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); img.transform.SetParent(parent, false);
            img.texture = it.thumb; img.raycastTarget = false;
            float aspect = (float)it.thumb.width / it.thumb.height, ww = Mathf.Min(w, h * aspect), hh = ww / aspect;
            Place(img.rectTransform, new Vector2(0, 1), new Vector2(x + (w - ww) / 2, -(y + (h - hh) / 2)), new Vector2(ww, hh), new Vector2(0, 1));
        }

        void Frame(RectTransform b, string title, string[] cats, int cat, System.Action<int> pick)
        {
            var bg = Panel(b, "Bg", Bg); Fill(bg); bg.GetComponent<Image>().raycastTarget = true;
            TextAt(b, title, 60, 34, 600, 70, 64, Color.white, TextAnchor.MiddleLeft, display);
            var line = Box(b, 60, 112, 1800, 1, Hair, null);
            // currencies + top-up + close
            var coins = Box(b, 1350, 40, 190, 54, CardBg); TextAt(coins, "● " + KryvonWallet.Coins.ToString("N0"), 18, 0, 160, 54, 26, Gold);
            var gems = Box(b, 1556, 40, 190, 54, CardBg); TextAt(gems, "◆ " + KryvonWallet.Gems.ToString("N0"), 18, 0, 120, 54, 26, new Color(0.72f, 0.56f, 1f));
            var plus = Box(gems, 138, 7, 42, 40, Gold); TextAt(plus, "+", 0, 0, 42, 40, 30, Dark, TextAnchor.MiddleCenter, display);
            Btn(plus, () => { Tab(1); storeCat = 8; FillSheet(1); });
            var close = Box(b, 1764, 40, 96, 54, CardBg); TextAt(close, "✕", 0, 0, 96, 54, 30, Color.white, TextAnchor.MiddleCenter);
            Btn(close, () => { PlayerPrefs.Save(); Tab(-1); });
            // categories
            for (int k = 0; k < cats.Length; k++)
            {
                int idx = k; bool on = k == cat;
                var row = Box(b, 60, 150 + k * 70, 280, 60, on ? CardSel : new Color(0, 0, 0, 0.001f));
                if (on) Box(row, 0, 12, 4, 36, Gold, null);
                TextAt(row, cats[k], 26, 0, 250, 60, 26, on ? Color.white : Dim, TextAnchor.MiddleLeft, display);
                Btn(row, () => { pick(idx); selItem = 0; FillSheet(cats == StoreCats ? 1 : 0); });
            }
            if (toast.Length > 0) { TextAt(b, toast, 380, 1010, 1000, 40, 24, Gold, TextAnchor.MiddleLeft); toast = ""; }
        }

        void Grid(RectTransform b, List<Item> items, bool store, float y0 = 150)
        {
            if (items.Count == 0) { TextAt(b, store ? "Nothing here yet." : "You don't own anything in this category yet – visit the STORE.", 380, y0, 1000, 40, 26, Dim); return; }
            selItem = Mathf.Clamp(selItem, 0, items.Count - 1);
            const float cw = 232, ch = 318, gap = 18; const int cols = 4;
            for (int k = 0; k < items.Count; k++)
            {
                int idx = k; var it = items[k]; bool on = k == selItem;
                float x = 380 + (k % cols) * (cw + gap), y = y0 + (k / cols) * (ch + gap);
                var c = Box(b, x, y, cw, ch, on ? CardSel : CardBg);
                // glass card, sharp corners, edge in the rarity colour (bright + glow when selected)
                var rc = RarityCol(it.rarity);
                if (sharp) { var ci = c.GetComponent<Image>(); ci.sprite = sharp; ci.color = on ? new Color(0.15f, 0.15f, 0.15f, 0.95f) : TacSolid; Edge(c, new Color(rc.r, rc.g, rc.b, on ? 1f : 0.45f)); if (on) GlowBehind(c, new Color(rc.r, rc.g, rc.b, 0.28f), 34); }
                if (it.cos != null) CosArt(c, it.cos, 10, 10, cw - 20, ch - 96); else ItemArt(c, it, 10, 10, cw - 20, ch - 96);
                Box(c, 0, ch - 84, cw, 3, RarityCol(it.rarity), null);
                TextAt(c, it.name, 14, ch - 76, cw - 28, 34, 24, Color.white, TextAnchor.MiddleLeft, display);
                Price(c, it, 0, ch - 40, cw, 20);
                Btn(c, () => { selItem = idx; FillSheet(store ? 1 : 0); });
            }
            Preview(b, items[selItem], store);
        }

        void Preview(RectTransform b, Item it, bool store)
        {
            var p = Box(b, 1400, 150, 460, 860, CardBg);
            if (sharp) { var rc = RarityCol(it.rarity); var pi = p.GetComponent<Image>(); pi.sprite = sharp; pi.color = TacSolid; Edge(p, new Color(rc.r, rc.g, rc.b, 0.8f)); GlowBehind(p, new Color(rc.r, rc.g, rc.b, 0.16f), 40); }
            if (it.cos != null) CosPreview(p, it, store); else ItemArt(p, it, 30, 20, 400, 470);
            TextAt(p, it.rarity, 30, 510, 400, 28, 20, RarityCol(it.rarity));
            TextAt(p, it.name, 30, 540, 400, 56, 48, Color.white, TextAnchor.MiddleLeft, display);
            var d = TextAt(p, it.desc, 30, 604, 400, 90, 20, Dim, TextAnchor.UpperLeft);
            Price(p, it, 30, 700, 400, 30);
            bool passOnly = it.cos != null && it.cos.source == "pass" && !it.owned;
            string label = passOnly ? "NASHAMA PASS" : it.owned ? (it.equipped ? "EQUIPPED" : "EQUIP") : "BUY";
            var btn = Box(p, 30, 770, 400, 66, it.owned && it.equipped ? new Color(1, 1, 1, 0.08f) : Gold);
            TextAt(btn, label, 0, 0, 400, 66, 34, it.owned && it.equipped ? Dim : Dark, TextAnchor.MiddleCenter, display);
            Btn(btn, () =>
            {
                if (passOnly) { Tab(2); return; }
                if (!it.owned)
                {
                    bool nowOwned = it.buy != null && it.buy();
                    toast = nowOwned ? it.name + " is yours. Equip it any time from INVENTORY." : (it.gems ? "Not enough gems – tap + to top up." : "Not enough coins – finish missions to earn more.");
                    if (nowOwned) it.equip?.Invoke();
                }
                else it.equip?.Invoke();
                RefreshWallet(); FillSheet(store ? 1 : 0);
            });
        }

        // ---------- crates (odds are always shown) ----------
        static readonly (string name, int price, bool gems, (string what, int pct)[] odds)[] Crates =
        {
            ("SUPPLY CRATE", 900, false, new[] { ("Skin (gun, parachute, bag, helmet, car)", 60), ("● 400–1,200 coins", 40) }),
            ("PREMIUM CRATE", 60, true, new[] { ("Skin (gun, parachute, bag, helmet, car)", 90), ("Outfit: Mythic Rune", 3), ("◆ 20 gems back", 7) }),
        };
        string OpenCrate(int c)
        {
            var cr = Crates[c];
            if (cr.gems) { if (KryvonWallet.Gems < cr.price) return null; KryvonWallet.Gems -= cr.price; } else { if (KryvonWallet.Coins < cr.price) return null; KryvonWallet.Coins -= cr.price; }
            int roll = Random.Range(0, 100), acc = 0; string won = cr.odds[cr.odds.Length - 1].what;
            foreach (var o in cr.odds) { acc += o.pct; if (roll < acc) { won = o.what; break; } }
            string Outfit(string id, string name) { int i = KryvonWallet.OutfitIndex(id); if (KryvonWallet.OutfitUnlocked(id)) { KryvonWallet.Coins += 500; return name + " (duplicate → ● 500)"; } PlayerPrefs.SetInt("w.outfit." + id, 1); return name; }
            if (won.StartsWith("Outfit: Mythic")) return Outfit("mythic", "MYTHIC RUNE");
            if (won.Contains("coins")) { int n = Random.Range(4, 13) * 100; KryvonWallet.Coins += n; return $"● {n} coins"; }
            if (won.Contains("gems")) { KryvonWallet.Gems += 20; return "◆ 20 gems"; }
            var pool = KryvonSkins.All.FindAll(x => x.source == "store" && (x.kind != CosKind.Weapon || x.rarity == "RARE"));
            var got = pool[Random.Range(0, pool.Count)]; if (KryvonSkins.Owned(got)) { KryvonWallet.Coins += 300; return got.name + " (duplicate → ● 300)"; }
            KryvonSkins.Grant(got.id); return got.name;
        }
        void CratesPage(RectTransform b)
        {
            for (int c = 0; c < Crates.Length; c++)
            {
                int idx = c; var cr = Crates[c]; float x = 380 + c * 500;
                var card = Box(b, x, 150, 480, 700, CardBg);
                var lid = Box(card, 90, 40, 300, 200, cr.gems ? new Color(0.3f, 0.2f, 0.45f) : new Color(0.32f, 0.27f, 0.18f));
                Box(lid, 0, 90, 300, 18, cr.gems ? new Color(0.72f, 0.56f, 1f) : Gold, null); Box(lid, 135, 0, 30, 200, new Color(0, 0, 0, 0.35f), null);
                TextAt(card, cr.name, 30, 270, 420, 56, 46, Color.white, TextAnchor.MiddleLeft, display);
                TextAt(card, "ODDS", 30, 334, 420, 26, 20, Dim);
                for (int k = 0; k < cr.odds.Length; k++) { TextAt(card, cr.odds[k].what, 30, 364 + k * 32, 320, 30, 21, Color.white); TextAt(card, cr.odds[k].pct + "%", 350, 364 + k * 32, 100, 30, 21, Gold, TextAnchor.MiddleRight); }
                var btn = Box(card, 30, 610, 420, 66, Gold);
                TextAt(btn, "OPEN  ·  " + (cr.gems ? "◆ " : "● ") + cr.price.ToString("N0"), 0, 0, 420, 66, 32, Dark, TextAnchor.MiddleCenter, display);
                Btn(btn, () => { var got = OpenCrate(idx); toast = got == null ? (Crates[idx].gems ? "Not enough gems – tap + to top up." : "Not enough coins.") : "YOU GOT:  " + got; RefreshWallet(); FillSheet(1); });
            }
        }

        // ---------- top-up (real money only once the game is in the stores) ----------
        static readonly (int gems, int bonus, string price)[] Packs = { (60, 0, "$0.99"), (300, 25, "$4.99"), (980, 110, "$14.99"), (1980, 260, "$29.99"), (3280, 600, "$49.99"), (6480, 1600, "$99.99") };
        void TopUpPage(RectTransform b)
        {
            for (int k = 0; k < Packs.Length; k++)
            {
                var pk = Packs[k]; float x = 380 + (k % 3) * 330, y = 150 + (k / 3) * 330;
                var c = Box(b, x, y, 310, 310, CardBg);
                for (int g = 0; g <= Mathf.Min(k, 4); g++) { var gem = Box(c, 110 + (g - Mathf.Min(k, 4) / 2f) * 22, 40 + (g % 2) * 10, 70, 70, new Color(0.62f, 0.45f, 1f, 0.85f), pill); }
                TextAt(c, "◆ " + pk.gems.ToString("N0"), 0, 140, 310, 50, 40, Color.white, TextAnchor.MiddleCenter, display);
                if (pk.bonus > 0) TextAt(c, "+" + pk.bonus + " BONUS", 0, 190, 310, 30, 20, new Color(0.72f, 0.56f, 1f), TextAnchor.MiddleCenter);
                var btn = Box(c, 30, 236, 250, 54, Gold); TextAt(btn, pk.price, 0, 0, 250, 54, 30, Dark, TextAnchor.MiddleCenter, display);
                Btn(btn, () => { toast = "Gem purchases open when KRAYVON is on Google Play and the App Store. Earn gems from MISSIONS now."; FillSheet(1); });
            }
        }

        void FeaturedPage(RectTransform b)
        {
            // headline: the evolving M4A1 skin, shown on the real gun
            var evo = KryvonSkins.Get("evo_m4");
            if (evo != null)
            {
                var big = Box(b, 380, 150, 600, 860, CardBg);
                CosArt(big, evo, 40, 20, 520, 440);
                var model = new Item { id = evo.id, kind = "cos", cos = evo };
                Box(big, 0, 640, 600, 3, RarityCol("LEGENDARY"), null);
                TextAt(big, "EVOLVING WEAPON SKIN  ·  4 LEVELS", 40, 656, 520, 28, 20, new Color(0.45f, 0.75f, 1f));
                TextAt(big, evo.name, 40, 686, 520, 64, 52, Color.white, TextAnchor.MiddleLeft, display);
                Price(big, new Item { owned = KryvonSkins.Owned(evo), price = evo.price, gems = evo.gems, equipped = KryvonSkins.Equipped(CosKind.Weapon, "saqr4") == evo }, 40, 760, 520, 30);
                Btn(big, () => { storeCat = 1; gunPick = 0; selItem = CosItems(CosKind.Weapon, "saqr4", false).FindIndex(i => i.id == "evo_m4"); FillSheet(1); });
                TextAt(big, "Also: AKM · DAMASCUS SULTAN in the Nashama Pass (level 30)", 40, 470, 520, 60, 20, Gold, TextAnchor.UpperLeft);
            }
            var suit = OutfitItems(false).Find(i => i.id == "mythic");
            if (suit != null)
            {
                var s = Box(b, 1000, 150, 380, 420, CardBg); ItemArt(s, suit, 20, 10, 340, 290);
                Box(s, 0, 312, 380, 3, RarityCol(suit.rarity), null);
                TextAt(s, suit.name, 20, 324, 340, 44, 36, Color.white, TextAnchor.MiddleLeft, display); Price(s, suit, 20, 372, 340, 22);
                Btn(s, () => { storeCat = 2; selItem = OutfitItems(false).FindIndex(i => i.id == "mythic"); FillSheet(1); });
            }
            var cr = Box(b, 1000, 590, 380, 420, CardBg);
            var lid = Box(cr, 90, 50, 200, 150, new Color(0.3f, 0.2f, 0.45f)); Box(lid, 0, 66, 200, 14, new Color(0.72f, 0.56f, 1f), null);
            TextAt(cr, "PREMIUM CRATE", 20, 230, 340, 44, 36, Color.white, TextAnchor.MiddleLeft, display);
            TextAt(cr, "Mythic Rune inside · odds shown", 20, 276, 340, 30, 20, Dim);
            TextAt(cr, "◆ 60", 20, 320, 340, 34, 26, new Color(0.72f, 0.56f, 1f), TextAnchor.MiddleCenter);
            Btn(cr, () => { storeCat = 7; FillSheet(1); });
        }

        void StorePage(RectTransform b)
        {
            Frame(b, "STORE", StoreCats, storeCat, k => storeCat = k);
            switch (storeCat)
            {
                case 0: FeaturedPage(b); break;
                case 1: CosPage(b, CosKind.Weapon, true); break;
                case 2: Grid(b, OutfitItems(false), true); break;
                case 3: CosPage(b, CosKind.Parachute, true); break;
                case 4: CosPage(b, CosKind.Backpack, true); break;
                case 5: CosPage(b, CosKind.Helmet, true); break;
                case 6: CosPage(b, CosKind.Vehicle, true); break;
                case 7: CratesPage(b); break;
                default: TopUpPage(b); break;
            }
        }
        void InventoryPage(RectTransform b)
        {
            Frame(b, "INVENTORY", InvCats, invCat, k => invCat = k);
            switch (invCat)
            {
                case 0: Grid(b, OutfitItems(true).FindAll(x => x.rarity == "CLASSIC"), false); break;
                case 1: Grid(b, OutfitItems(true).FindAll(x => x.rarity != "CLASSIC"), false); break;
                case 2: CosPage(b, CosKind.Weapon, false); break;
                case 3: CosPage(b, CosKind.Parachute, false); break;
                case 4: CosPage(b, CosKind.Backpack, false); break;
                case 5: CosPage(b, CosKind.Helmet, false); break;
                default: CosPage(b, CosKind.Vehicle, false); break;
            }
        }

        // ================= NASHAMA PASS: free track + premium track, 100 levels, claim with a burst, rotating preview =================
        int passPage, passSel = 1; bool passSelPremium;
        static int PassXp() => KryvonWallet.Stat("matches") * 120 + KryvonWallet.Stat("kills") * 60 + KryvonWallet.Stat("landings") * 30 + KryvonWallet.Stat("loot") * 3 + KryvonWallet.Stat("wins") * 400;
        const int XpPerLevel = 300, PassLevels = 100, PremiumPrice = 600;
        static int PassLevel() => Mathf.Clamp(PassXp() / XpPerLevel + 1, 1, PassLevels);
        static bool PassPremium { get => PlayerPrefs.GetInt("bp.premium", 0) == 1; set { PlayerPrefs.SetInt("bp.premium", value ? 1 : 0); PlayerPrefs.Save(); } }

        // reward: kind (coins, gems, chute, outfit), amount / index / id, label
        static (string kind, int n, string id, string label) PassReward(int lv, bool premium)
        {
            if (!premium)
            {
                if (lv == 5) return ("cos", 0, "chute_desert", "DESERT PARACHUTE");
                if (lv == 12) return ("cos", 0, "emote_victory", "NASHAMA CHEER");
                if (lv == 15) return ("cos", 0, "bag_desert", "DESERT BACKPACK");
                if (lv == 35) return ("cos", 0, "jet_desert", "SHAHEEN · DESERT FALCON");
                if (lv == 55) return ("cos", 0, "dune_zalzal45", "UMP45 · WADI RUM DUNE");
                if (lv == 75) return ("cos", 0, "emote_dallah", "ARABIC COFFEE");
                if (lv == 100) return ("cos", 0, "dune_saqr4", "M4A1 · WADI RUM DUNE");
                if (lv % 10 == 0) return ("gems", 20, "", "◆ 20");
                return ("coins", lv % 3 == 0 ? 400 : 200, "", lv % 3 == 0 ? "● 400" : "● 200");
            }
            switch (lv)
            {
                case 1: return ("cos", 0, "dune_barq762", "AKM · WADI RUM DUNE");
                case 10: return ("cos", 0, "emote_dabke", "DABKE");
                case 18: return ("cos", 0, "dune_nimr47", "AK-47 · WADI RUM DUNE");
                case 20: return ("cos", 0, "chute_skull", "JOLLY ROGER PARACHUTE");
                case 25: return ("cos", 0, "jet_night", "SHAHEEN · NIGHT HAWK");
                case 30: return ("wskin", 0, "pass_akm", "AKM · DAMASCUS SULTAN");
                case 38: return ("cos", 0, "dune_shahin", "HK416 · WADI RUM DUNE");
                case 40: return ("cos", 0, "chute_gold", "ROYAL GOLD PARACHUTE");
                case 45: return ("cos", 0, "helmet_skull", "HELMET · JOLLY ROGER");
                case 50: return ("outfit", 0, "mythic", "MYTHIC RUNE");
                case 58: return ("cos", 0, "dune_qannas98", "KAR98K · WADI RUM DUNE");
                case 60: return ("cos", 0, "emote_ardah", "SWORD DANCE");
                case 65: return ("cos", 0, "jet_petra", "SHAHEEN · PETRA ROSE");
                case 68: return ("cos", 0, "dune_rami14", "M14 EBR · WADI RUM DUNE");
                case 78: return ("cos", 0, "dune_jabal249", "M249 · WADI RUM DUNE");
                case 85: return ("cos", 0, "dune_raads12", "SAIGA-12 · WADI RUM DUNE");
                case 92: return ("cos", 0, "dune_malik300", "AWM · WADI RUM DUNE");
                case 100: return ("cos", 0, "jet_gold", "SHAHEEN · ROYAL GOLD");
            }
            if (lv % 5 == 0) return ("gems", 40, "", "◆ 40");
            return ("coins", 500, "", "● 500");
        }
        /// where a pass cosmetic is unlocked ("Unlock: Nashama Pass premium · level 30")
        static string PassWhere(string id)
        {
            for (int lv = 1; lv <= PassLevels; lv++) foreach (var pr in new[] { false, true }) if (PassReward(lv, pr).id == id) return $"Unlock: Nashama Pass {(pr ? "premium" : "free")} · level {lv}";
            return "Unlock: Nashama Pass";
        }
        static string PassKey(int lv, bool premium) => (premium ? "bp.p." : "bp.f.") + lv;
        static bool PassClaimed(int lv, bool premium) => PlayerPrefs.GetInt(PassKey(lv, premium), 0) == 1;
        static bool PassClaimable(int lv, bool premium) => lv <= PassLevel() && !PassClaimed(lv, premium) && (!premium || PassPremium);
        static void PassGive(int lv, bool premium)
        {
            var r = PassReward(lv, premium);
            switch (r.kind)
            {
                case "coins": KryvonWallet.Coins += r.n; break;
                case "gems": KryvonWallet.Gems += r.n; break;
                case "chute": if (KryvonWallet.Owns(r.n)) KryvonWallet.Coins += 300; else PlayerPrefs.SetInt("w.chute." + r.n, 1); break;
                case "outfit": if (KryvonWallet.OutfitUnlocked(r.id)) KryvonWallet.Coins += 500; else PlayerPrefs.SetInt("w.outfit." + r.id, 1); break;
                case "wskin": case "cos": { var c = KryvonSkins.Get(r.id); if (c != null && KryvonSkins.Owned(c)) KryvonWallet.Coins += 500; else { KryvonSkins.Grant(r.id); if (c != null && (c.kind == CosKind.Jet || c.kind == CosKind.Emote)) KryvonSkins.Equip(c); } break; }
            }
            PlayerPrefs.SetInt(PassKey(lv, premium), 1); PlayerPrefs.Save();
        }

        Color RewardRarity((string kind, int n, string id, string label) r) => r.kind == "wskin" || r.kind == "cos" ? RarityCol(KryvonSkins.Get(r.id)?.rarity ?? "RARE") : r.kind == "outfit" ? (r.id == "mythic" ? RarityCol("LEGENDARY") : RarityCol("RARE")) : r.kind == "chute" ? RarityCol(r.n >= 5 ? "LEGENDARY" : r.n >= 3 ? "EPIC" : "RARE") : r.kind == "gems" ? new Color(0.72f, 0.56f, 1f) : new Color(0.72f, 0.74f, 0.78f);

        void RewardArt(RectTransform parent, (string kind, int n, string id, string label) r, float x, float y, float w, float h, bool spin)
        {
            if (r.kind == "outfit") { if (spin) Spinner(parent, r.id, x, y, w, h); else ItemArt(parent, new Item { kind = "outfit", thumb = Thumb(r.id) }, x, y, w, h); return; }
            if (r.kind == "chute") { var c = KryvonWallet.Chutes[r.n]; ItemArt(parent, new Item { kind = "chute", a = c.a, b = c.b }, x, y, w, h); return; }
            if (r.kind == "wskin" || r.kind == "cos") { var c = KryvonSkins.Get(r.id); if (c != null) { CosArt(parent, c, x, y, w, h); TextAt(parent, r.label, x, y + h * 0.78f, w, h * 0.2f, Mathf.RoundToInt(h * 0.09f), Color.white, TextAnchor.MiddleCenter, display); } return; }
            bool gem = r.kind == "gems";
            var dot = Box(parent, x + w / 2 - h * 0.22f, y + h * 0.18f, h * 0.44f, h * 0.44f, gem ? new Color(0.62f, 0.45f, 1f, 0.9f) : new Color(1f, 0.74f, 0.25f, 0.9f), pill);
            TextAt(parent, r.label, x, y + h * 0.66f, w, h * 0.3f, Mathf.RoundToInt(h * 0.16f), Color.white, TextAnchor.MiddleCenter, display);
        }

        // slow turntable from 24 pre-rendered frames (rendered in Blender with studio light)
        static readonly Dictionary<string, Texture2D[]> spins = new();
        void Spinner(RectTransform parent, string id, float x, float y, float w, float h)
        {
            if (!spins.TryGetValue(id, out var frames))
            {
                frames = new Texture2D[24]; for (int i = 0; i < 24; i++) frames[i] = Resources.Load<Texture2D>($"UI/Spin/{id}/f_{i:00}");
                spins[id] = frames;
            }
            var glowR = Box(parent, x + w * 0.1f, y + h * 0.1f, w * 0.8f, h * 0.8f, new Color(1f, 0.8f, 0.5f, 0.18f), glow);   // studio light behind
            var img = new GameObject("Spin", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); img.transform.SetParent(parent, false); img.raycastTarget = false;
            float ww = Mathf.Min(w, h * 300f / 440f), hh = ww * 440f / 300f;
            Place(img.rectTransform, new Vector2(0, 1), new Vector2(x + (w - ww) / 2, -(y + (h - hh) / 2)), new Vector2(ww, hh), new Vector2(0, 1));
            if (frames[0] == null) { img.texture = Thumb(id); return; }
            StartCoroutine(Spin(img, frames));
        }
        IEnumerator Spin(RawImage img, Texture2D[] frames)
        {
            int i = 0;
            while (img) { if (frames[i]) img.texture = frames[i]; i = (i + 1) % frames.Length; yield return new WaitForSecondsRealtime(0.11f); }
        }

        // a burst of light and sparks over a claimed reward
        IEnumerator Burst(RectTransform at)
        {
            var root = new GameObject("Burst", typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(canvas, false);
            root.position = at.position;
            var flash = Panel(root, "Flash", new Color(1f, 0.85f, 0.45f, 0.9f), glow); flash.sizeDelta = new Vector2(80, 80);
            var bits = new List<(RectTransform r, Vector2 v)>();
            for (int i = 0; i < 26; i++)
            {
                var b = Panel(root, "Spark", i % 3 == 0 ? Color.white : Gold, pill); b.sizeDelta = Vector2.one * Random.Range(5f, 11f);
                bits.Add((b, Random.insideUnitCircle.normalized * Random.Range(220f, 520f)));
            }
            for (float t = 0; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.9f;
                flash.sizeDelta = Vector2.one * Mathf.Lerp(80, 520, Mathf.Sqrt(k)); flash.GetComponent<Image>().color = new Color(1f, 0.85f, 0.45f, 0.9f * (1 - k));
                foreach (var (r, v) in bits) { r.anchoredPosition = v * k + Vector2.down * 260f * k * k; r.GetComponent<Image>().color = new Color(1, 0.85f, 0.4f, 1 - k); }
                yield return null;
            }
            Destroy(root.gameObject);
        }

        void PassPage(RectTransform b)
        {
            var bg = Panel(b, "Bg", Bg); Fill(bg); bg.GetComponent<Image>().raycastTarget = true;
            TextAt(b, "NASHAMA PASS", 60, 30, 700, 70, 64, Color.white, TextAnchor.MiddleLeft, display);
            TextAt(b, "SEASON 1  ·  BILAD AL-SHAM", 62, 92, 600, 28, 20, Gold);
            var close = Box(b, 1764, 40, 96, 54, CardBg); TextAt(close, "✕", 0, 0, 96, 54, 30, Color.white, TextAnchor.MiddleCenter); Btn(close, () => Tab(-1));
            // level + xp
            int lv = PassLevel(), xp = PassXp() % XpPerLevel;
            var lvl = Box(b, 760, 34, 120, 84, CardBg); TextAt(lvl, "LEVEL", 0, 6, 120, 22, 16, Dim, TextAnchor.MiddleCenter); TextAt(lvl, lv.ToString(), 0, 24, 120, 56, 50, Gold, TextAnchor.MiddleCenter, display);
            var track = Box(b, 900, 70, 420, 12, new Color(1, 1, 1, 0.1f), pill);
            Box(track, 0, 0, lv >= PassLevels ? 420 : 420f * xp / XpPerLevel, 12, Gold, pill);
            TextAt(b, lv >= PassLevels ? "MAX LEVEL" : $"{xp} / {XpPerLevel} XP  ·  play, loot, land and win to level up", 900, 86, 600, 26, 18, Dim);
            // premium
            if (!PassPremium)
            {
                var buy = Box(b, 1350, 40, 390, 64, Gold);
                TextAt(buy, $"UNLOCK PREMIUM  ·  ◆ {PremiumPrice}", 0, 0, 390, 64, 30, Dark, TextAnchor.MiddleCenter, display);
                Btn(buy, () => { if (KryvonWallet.Gems >= PremiumPrice) { KryvonWallet.Gems -= PremiumPrice; PassPremium = true; toast = "PREMIUM UNLOCKED – every premium reward up to your level can be claimed."; } else toast = "Not enough gems."; RefreshWallet(); FillSheet(2); });
            }
            else TextAt(b, "★ PREMIUM ACTIVE", 1350, 50, 390, 44, 30, Gold, TextAnchor.MiddleCenter, display);

            // tracks: 8 levels per page
            const int perPage = 8; int first = passPage * perPage + 1;
            TextAt(b, "FREE", 60, 210, 120, 30, 22, Dim, TextAnchor.MiddleLeft, display);
            TextAt(b, "PREMIUM", 60, 470, 140, 30, 22, Gold, TextAnchor.MiddleLeft, display);
            for (int k = 0; k < perPage; k++)
            {
                int L = first + k; if (L > PassLevels) break;
                float x = 200 + k * 140;
                bool reached = L <= lv;
                TextAt(b, L.ToString(), x, 150, 128, 40, 34, reached ? Gold : Dim, TextAnchor.MiddleCenter, display);
                Box(b, x, 192, 128, 4, reached ? Gold : new Color(1, 1, 1, 0.12f), null);
                foreach (bool prem in new[] { false, true })
                {
                    var r = PassReward(L, prem); bool claimed = PassClaimed(L, prem), can = PassClaimable(L, prem), sel = passSel == L && passSelPremium == prem;
                    float y = prem ? 460 : 206;
                    var card = Box(b, x, y + 16, 128, 220, sel ? CardSel : CardBg);
                    if (prem) { var fr = Panel(card, "Gold", new Color(1f, 0.74f, 0.25f, PassPremium ? 0.9f : 0.45f), round); Fill(fr); fr.GetComponent<Image>().fillCenter = false; }
                    if (sel) { var o = Panel(card, "Sel", new Color(1, 1, 1, 0.6f), round); Fill(o, -3); o.GetComponent<Image>().fillCenter = false; }
                    RewardArt(card, r, 8, 8, 112, 150, false);
                    Box(card, 0, 168, 128, 3, RewardRarity(r), null);
                    TextAt(card, claimed ? "✓ CLAIMED" : can ? "CLAIM" : prem && !PassPremium ? "🔒 PREMIUM" : "LV " + L, 0, 176, 128, 40, 18, claimed ? Dim : can ? Gold : Dim, TextAnchor.MiddleCenter, display);
                    int lvl2 = L; bool p2 = prem; var cardRef = card;
                    Btn(card, () =>
                    {
                        passSel = lvl2; passSelPremium = p2;
                        if (PassClaimable(lvl2, p2)) { PassGive(lvl2, p2); toast = "UNLOCKED:  " + PassReward(lvl2, p2).label; StartCoroutine(Burst(cardRef)); RefreshWallet(); }
                        FillSheet(2);
                    });
                }
            }
            // page arrows
            foreach (var (glyph, dir, x) in new[] { ("‹", -1, 150f), ("›", 1, 1330f) })
            {
                var a = Box(b, x, 400, 44, 120, CardBg); TextAt(a, glyph, 0, 0, 44, 120, 46, Color.white, TextAnchor.MiddleCenter);
                Btn(a, () => { passPage = Mathf.Clamp(passPage + dir, 0, (PassLevels - 1) / perPage); FillSheet(2); });
            }
            // preview of the selected reward: rotating outfit under studio light
            var sr = PassReward(passSel, passSelPremium);
            var pv = Box(b, 1400, 150, 460, 860, CardBg);
            RewardArt(pv, sr, 30, 30, 400, 560, true);
            TextAt(pv, (passSelPremium ? "PREMIUM" : "FREE") + "  ·  LEVEL " + passSel, 30, 610, 400, 28, 20, passSelPremium ? Gold : Dim);
            TextAt(pv, sr.label, 30, 640, 400, 60, 50, Color.white, TextAnchor.MiddleLeft, display);
            TextAt(pv, sr.kind == "outfit" ? "Outfit for HAMZA" : sr.kind == "chute" ? "Parachute canopy" : sr.kind == "gems" ? "Gems for the STORE" : "Coins for the STORE", 30, 702, 400, 30, 20, Dim);
            if (toast.Length > 0) { TextAt(b, toast, 200, 760, 1100, 40, 26, Gold); toast = ""; }
            TextAt(b, "Rewards up to your level can be claimed. Premium unlocks every gold reward on the bottom row.", 200, 980, 1150, 30, 20, Dim);
        }

        // ---------- loading into the match ----------
        void BuildLoading()
        {
            loading = Panel(canvas, "Loading", Color.black); Fill(loading);
            var logo = Label(loading, "KRAYVON", display, 140, Gold, TextAnchor.MiddleCenter); logo.rectTransform.offsetMin = new Vector2(0, 120);
            loadText = Label(loading, "", bodyBold, 26, Dim, TextAnchor.MiddleCenter); Place(loadText.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(0, 40), new Vector2(900, 40));
            var bar = Panel(loading, "Bar", new Color(1, 1, 1, 0.12f), pill); Place(bar, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(760, 10));
            var f = Panel(bar, "Fill", Gold, pill); f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(0, 1); f.offsetMin = f.offsetMax = Vector2.zero; loadFill = f.GetComponent<Image>();
            loading.gameObject.SetActive(false);
        }
        void StartMatch() { KryvonWallet.Add("matches"); PlayerPrefs.Save(); StartCoroutine(Load()); }
        IEnumerator Load()
        {
            loading.gameObject.SetActive(true);
            loadText.text = $"{Modes[mode]}  ·  BILAD AL-SHAM  ·  {heroes[hero].name}";
            var op = SceneManager.LoadSceneAsync(playScene);
            if (op == null) { loadText.text = "Add the Play scene to Build Settings"; yield break; }
            while (!op.isDone) { loadFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(op.progress / 0.9f), 1); yield return null; }
        }

        // ---------- turntable ----------
        void Update()
        {
            Update2();
            if (!turntable) return;
            var m = UnityEngine.InputSystem.Mouse.current;
            bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (m != null && m.leftButton.isPressed && !overUI) spinVel = -m.delta.ReadValue().x * 0.35f;
            else spinVel = Mathf.Lerp(spinVel, 0f, Time.deltaTime * 3f);   // slow idle spin
            spin += spinVel; turntable.localRotation = Quaternion.Euler(0, 180 + Mathf.Sin(Time.time * 0.25f) * 12f + spin, 0);
        }
    }
}

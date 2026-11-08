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
    public class KryvonLobby : MonoBehaviour
    {
        [System.Serializable] public class Hero { public string id, name, role, bio; public GameObject model; }
        [SerializeField] Hero[] heroes;
        [SerializeField] Font display, body, bodyBold;
        [SerializeField] Transform turntable;
        [SerializeField] string playScene = "Play";
        [SerializeField] AudioClip clickSound, ambience;

        static readonly Color Gold = new(1f, 0.78f, 0.32f), GoldDeep = new(0.86f, 0.55f, 0.16f), Ink = new(0.04f, 0.045f, 0.055f, 0.78f), Line = new(1f, 1f, 1f, 0.09f), Dim = new(1f, 1f, 1f, 0.62f);
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
            BuildLobby(); BuildSettings(); BuildComingSoon(); BuildLoading(); BuildSplash();
            ShowHero(hero, true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        // ---------- splash ----------
        void BuildSplash()
        {
            splash = Panel(canvas, "Splash", Color.black); Fill(splash);
            var vign = Panel(splash, "Glow", new Color(1f, 0.55f, 0.18f, 0.22f), glow); Place(vign, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 900));
            var shadow = Label(splash, "KRAYVON", display, 230, new Color(0, 0, 0, 0.8f), TextAnchor.MiddleCenter); shadow.rectTransform.offsetMin += new Vector2(8, -8); shadow.rectTransform.offsetMax += new Vector2(8, -8);
            var logo = Label(splash, "KRAYVON", display, 230, Gold, TextAnchor.MiddleCenter);
            logo.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.25f, 0.05f, 0.9f);
            var tag = Label(splash, "BILAD AL-SHAM  ·  BATTLE ROYALE", bodyBold, 30, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter);
            tag.rectTransform.offsetMin = new Vector2(0, -330); tag.rectTransform.offsetMax = new Vector2(0, -330);
            var bar = Panel(splash, "Bar", new Color(1, 1, 1, 0.12f), pill); Place(bar, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(520, 6));
            var fill = Panel(bar, "Fill", Gold, pill); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            StartCoroutine(SplashRun(splash.gameObject.AddComponent<CanvasGroup>(), logo.rectTransform, fill));
        }
        IEnumerator SplashRun(CanvasGroup cg, RectTransform logo, RectTransform fill)
        {
            lobby.GetComponent<CanvasGroup>().alpha = 0;
            for (float t = 0; t < 3.0f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 1.2f));
                logo.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, k); logo.GetComponent<Text>().color = new Color(Gold.r, Gold.g, Gold.b, k);
                fill.anchorMax = new Vector2(Mathf.Clamp01(t / 2.8f), 1);
                if ((UnityEngine.InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame ?? false) || (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame ?? false)) break;
                yield return null;
            }
            var lg = lobby.GetComponent<CanvasGroup>();
            for (float t = 0; t < 0.7f; t += Time.unscaledDeltaTime) { cg.alpha = 1 - t / 0.7f; lg.alpha = t / 0.7f; yield return null; }
            cg.gameObject.SetActive(false); lg.alpha = 1;
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
            var top = Panel(lobby, "TopShade", new Color(0, 0, 0, 0.55f), grad); top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1); top.sizeDelta = new Vector2(0, 160); top.anchoredPosition = Vector2.zero;
            var left = Panel(lobby, "LeftShade", new Color(0, 0, 0, 0.55f), gradH); left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0, 1); left.pivot = new Vector2(0, 0.5f); left.sizeDelta = new Vector2(620, 0); left.anchoredPosition = Vector2.zero;

            // logo: gold mountain-and-skyline mark over the wordmark
            var mark = Label(lobby, "▲", display, 120, Gold, TextAnchor.MiddleCenter); Place(mark.rectTransform, new Vector2(0, 1), new Vector2(70, -10), new Vector2(380, 110), new Vector2(0, 1));
            var mark2 = Label(lobby, "▲  ▲", display, 64, GoldDeep, TextAnchor.MiddleCenter); Place(mark2.rectTransform, new Vector2(0, 1), new Vector2(70, -46), new Vector2(380, 70), new Vector2(0, 1));
            var logo = Label(lobby, "KRAYVON", display, 104, new Color(0.95f, 0.95f, 0.97f), TextAnchor.MiddleCenter); Place(logo.rectTransform, new Vector2(0, 1), new Vector2(70, -112), new Vector2(380, 110), new Vector2(0, 1));
            logo.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f); logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -3);
            var sub = Label(lobby, "— BATTLE ROYALE —", bodyBold, 24, Gold, TextAnchor.MiddleCenter); Place(sub.rectTransform, new Vector2(0, 1), new Vector2(70, -214), new Vector2(380, 30), new Vector2(0, 1));

            // top tab bar
            string[] tabs = { "▶  PLAY", "▣  INVENTORY", "🛒  STORE", "★  MISSIONS", "⚙  SETTINGS" };
            var bar = Panel(lobby, "TabBar", new Color(0.05f, 0.06f, 0.08f, 0.82f), round); Place(bar, new Vector2(0.5f, 1), new Vector2(-40, -26), new Vector2(980, 58), new Vector2(0.5f, 1));
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                var t = Panel(bar, "Tab", i == 0 ? Gold : new Color(0, 0, 0, 0.001f), round); Place(t, new Vector2(0, 0.5f), new Vector2(6 + i * 194, 0), new Vector2(188, 48), new Vector2(0, 0.5f));
                Label(t, tabs[i], bodyBold, 24, i == 0 ? new Color(0.12f, 0.08f, 0.02f) : Color.white, TextAnchor.MiddleCenter);
                tabBgs.Add(t.GetComponent<Image>()); Btn(t, () => Tab(idx));
            }
            // currencies + profile
            foreach (var (txt, x, col) in new[] { ("● 12,450", -360f, Gold), ("◆ 620", -210f, new Color(0.75f, 0.55f, 1f)) })
            {
                var chip = Panel(lobby, "Currency", new Color(0.05f, 0.06f, 0.08f, 0.82f), round); Place(chip, new Vector2(1, 1), new Vector2(x, -26), new Vector2(144, 50), new Vector2(1, 1));
                Label(chip, txt, bodyBold, 26, col, TextAnchor.MiddleCenter);
            }
            var prof = Panel(lobby, "Profile", new Color(0.05f, 0.06f, 0.08f, 0.82f), round); Place(prof, new Vector2(1, 1), new Vector2(-150, -26), new Vector2(54, 50), new Vector2(1, 1));
            Label(prof, "👤", bodyBold, 26, Color.white, TextAnchor.MiddleCenter);

            // left stack: START GAME + modes
            var start = Panel(lobby, "Start", Gold, round); Place(start, new Vector2(0, 0.5f), new Vector2(60, 110), new Vector2(400, 78), new Vector2(0, 0.5f));
            var shine = Panel(start, "Shine", new Color(1, 1, 1, 0.22f), round); shine.anchorMin = new Vector2(0, 0.5f); shine.anchorMax = Vector2.one; shine.offsetMin = new Vector2(4, 0); shine.offsetMax = new Vector2(-4, -4);
            var st = Label(start, "▶   START GAME", display, 50, new Color(0.12f, 0.07f, 0.01f), TextAnchor.MiddleLeft); st.rectTransform.offsetMin = new Vector2(28, 0);
            Btn(start, StartMatch);
            var startGlow = Panel(lobby, "StartGlow", new Color(1f, 0.6f, 0.15f, 0.3f), glow); Place(startGlow, new Vector2(0, 0.5f), new Vector2(20, 110), new Vector2(480, 150), new Vector2(0, 0.5f)); startGlow.SetSiblingIndex(start.GetSiblingIndex());
            StartCoroutine(Pulse(startGlow.GetComponent<Image>()));
            string[] modes = { "⛬  BATTLE ROYALE", "🏆  RANKED", "⊕  CUSTOM MATCH" };
            for (int i = 0; i < modes.Length; i++)
            {
                int idx = i;
                var m = Panel(lobby, "Mode", new Color(0.05f, 0.06f, 0.08f, 0.82f), round); Place(m, new Vector2(0, 0.5f), new Vector2(60, 26 - i * 62), new Vector2(400, 54), new Vector2(0, 0.5f));
                var l = Label(m, modes[i], bodyBold, 26, Color.white); l.rectTransform.offsetMin = new Vector2(22, 0);
                modeChips.Add(m.GetComponent<Image>()); Btn(m, () => SetMode(idx));
            }
            SetMode(Mathf.Clamp(PlayerPrefs.GetInt("kryvon.queue", 0), 0, 2));
            // map card
            var card = Panel(lobby, "MapCard", new Color(0.05f, 0.06f, 0.08f, 0.85f), round); Place(card, new Vector2(0, 0), new Vector2(60, 60), new Vector2(400, 210), new Vector2(0, 0));
            mapCardImg = Panel(card, "Thumb", Color.white).GetComponent<Image>(); Fill(mapCardImg.rectTransform, 6); mapCardImg.preserveAspect = false;
            var cshade = Panel(card, "Shade", new Color(0, 0, 0, 0.7f), grad); cshade.anchorMin = Vector2.zero; cshade.anchorMax = new Vector2(1, 0.4f); cshade.offsetMin = new Vector2(6, 6); cshade.offsetMax = new Vector2(-6, 0); cshade.localScale = new Vector3(1, -1, 1);
            mapCardName = Label(card, "", bodyBold, 26, Color.white); Place(mapCardName.rectTransform, new Vector2(0, 0), new Vector2(20, 14), new Vector2(360, 34), new Vector2(0, 0));
            var arrow = Label(card, "›", bodyBold, 44, Color.white, TextAnchor.MiddleRight); Place(arrow.rectTransform, new Vector2(1, 0), new Vector2(-18, 10), new Vector2(40, 40), new Vector2(1, 0));
            Btn(card, () => SetRegion((region + 1) % Mathf.Max(1, mapNames?.Length ?? 1)));

            // right: region strip
            int n = mapThumbs?.Length ?? 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var f = Panel(lobby, "Region", new Color(1, 1, 1, 0.2f), round); Place(f, new Vector2(1, 1), new Vector2(-24, -100 - i * 184), new Vector2(300, 172), new Vector2(1, 1));
                var img = Panel(f, "Img", Color.white).GetComponent<Image>(); img.sprite = S(mapThumbs[i]); Fill(img.rectTransform, 4);
                var nm = Label(f, mapNames[i].ToUpper(), bodyBold, 18, Color.white); Place(nm.rectTransform, new Vector2(0, 0), new Vector2(14, 6), new Vector2(270, 24), new Vector2(0, 0));
                nm.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
                regionFrames.Add(f.GetComponent<Image>()); Btn(f, () => SetRegion(idx));
            }
            SetRegion(Mathf.Clamp(PlayerPrefs.GetInt("kryvon.region", 0), 0, Mathf.Max(0, n - 1)));

            // squad name tags (positioned every frame over the heads)
            if (tagAnchors != null)
                for (int i = 0; i < tagAnchors.Length; i++)
                {
                    var tg = Panel(lobby, "Tag", new Color(0.05f, 0.06f, 0.08f, 0.75f), round); tg.sizeDelta = new Vector2(210, 40); tg.pivot = new Vector2(0.5f, 0);
                    var dot = Panel(tg, "Ready", new Color(0.35f, 0.9f, 0.45f), pill); Place(dot, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(12, 12), new Vector2(0, 0.5f));
                    var tl = Label(tg, tagNames != null && i < tagNames.Length ? tagNames[i] : "PLAYER", bodyBold, 20, i == 0 ? Gold : Color.white); tl.rectTransform.offsetMin = new Vector2(32, 0);
                    tags.Add((tagAnchors[i], tg));
                }

            // hero switch under the player (keeps the earlier picker)
            heroName = Label(lobby, "", display, 46, Color.white, TextAnchor.MiddleCenter); Place(heroName.rectTransform, new Vector2(0.5f, 0), new Vector2(-40, 70), new Vector2(500, 56));
            heroRole = Label(lobby, "", bodyBold, 18, Gold, TextAnchor.MiddleCenter); Place(heroRole.rectTransform, new Vector2(0.5f, 0), new Vector2(-40, 40), new Vector2(600, 26));
            heroBio = Label(lobby, "", body, 1, Color.clear); heroBio.gameObject.SetActive(false);
            foreach (var (glyph, dir) in new[] { ("‹", -1), ("›", 1) })
            {
                var a = Panel(lobby, "Arrow", new Color(0.05f, 0.06f, 0.08f, 0.8f), pill); Place(a, new Vector2(0.5f, 0), new Vector2(-40 + dir * 300, 60), new Vector2(58, 58));
                Label(a, glyph, bodyBold, 46, Gold, TextAnchor.MiddleCenter);
                Btn(a, () => ShowHero((hero + dir + heroes.Length) % heroes.Length));
            }
            tip = Label(lobby, "", body, 18, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter); Place(tip.rectTransform, new Vector2(0.5f, 0), new Vector2(-40, 12), new Vector2(1000, 26));
            StartCoroutine(Tips_());
        }

        void Tab(int i)
        {
            for (int k = 0; k < tabBgs.Count; k++) { tabBgs[k].color = k == i ? Gold : new Color(0, 0, 0, 0.001f); tabBgs[k].GetComponentInChildren<Text>().color = k == i ? new Color(0.12f, 0.08f, 0.02f) : Color.white; }
            settings.gameObject.SetActive(i == 4);
            comingSoon.gameObject.SetActive(i == 1 || i == 2 || i == 3);
            if (i >= 1 && i <= 3) comingSoon.GetComponentInChildren<Text>().text = i == 1 ? "INVENTORY" : i == 2 ? "STORE" : "MISSIONS";
        }
        void SetRegion(int i)
        {
            region = i; PlayerPrefs.SetInt("kryvon.region", i);
            if (mapThumbs != null && i < mapThumbs.Length) { mapCardImg.sprite = S(mapThumbs[i]); mapCardName.text = "⌖  " + mapNames[i]; }
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
            for (int k = 0; k < modeChips.Count; k++) { modeChips[k].color = k == mode ? new Color(0.86f, 0.55f, 0.16f, 0.9f) : new Color(0.05f, 0.06f, 0.08f, 0.82f); }
        }
        void ShowHero(int i, bool instant = false)
        {
            hero = i;
            for (int k = 0; k < heroes.Length; k++) if (heroes[k].model) heroes[k].model.SetActive(k == i);
            if (heroes.Length == 0) return;
            heroName.text = heroes[i].name; heroRole.text = heroes[i].role; heroBio.text = heroes[i].bio;
            PlayerPrefs.SetString(KryvonHeroSwitch.Pref, heroes[i].id);
            if (!instant) spin = 0;
        }

        // ---------- settings ----------
        void BuildSettings()
        {
            settings = Panel(canvas, "Settings", new Color(0.03f, 0.035f, 0.045f, 0.94f), round); Place(settings, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 560));
            Label(settings, "SETTINGS", display, 64, Gold, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -24);
            Slider("MASTER VOLUME", 140, PlayerPrefs.GetFloat("kryvon.volume", 0.8f), v => { AudioListener.volume = v; PlayerPrefs.SetFloat("kryvon.volume", v); });
            Slider("MOUSE SENSITIVITY", 30, PlayerPrefs.GetFloat("kryvon.sens", 0.5f), v => PlayerPrefs.SetFloat("kryvon.sens", v));
            Slider("GRAPHICS QUALITY", -80, QualitySettings.GetQualityLevel() / Mathf.Max(1f, QualitySettings.names.Length - 1), v => QualitySettings.SetQualityLevel(Mathf.RoundToInt(v * (QualitySettings.names.Length - 1)), true));
            var close = Panel(settings, "Close", Gold, round); Place(close, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(260, 64));
            Label(close, "DONE", display, 44, new Color(0.14f, 0.08f, 0.01f), TextAnchor.MiddleCenter); Btn(close, () => Nav(0));
            settings.gameObject.SetActive(false);
        }
        void Slider(string name, float y, float value, UnityEngine.Events.UnityAction<float> onChange)
        {
            var l = Label(settings, name, bodyBold, 24, Color.white); Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-300, y + 30), new Vector2(600, 30), new Vector2(0, 0.5f));
            var bg = Panel(settings, "Track", Line, pill); Place(bg, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(600, 10));
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
        void StartMatch() { PlayerPrefs.Save(); StartCoroutine(Load()); }
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
            if (!turntable) return;
            var m = UnityEngine.InputSystem.Mouse.current;
            bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (m != null && m.leftButton.isPressed && !overUI) spinVel = -m.delta.ReadValue().x * 0.35f;
            else spinVel = Mathf.Lerp(spinVel, 0f, Time.deltaTime * 3f);   // slow idle spin
            spin += spinVel; turntable.localRotation = Quaternion.Euler(0, 180 + Mathf.Sin(Time.time * 0.25f) * 12f + spin, 0);
        }
    }
}

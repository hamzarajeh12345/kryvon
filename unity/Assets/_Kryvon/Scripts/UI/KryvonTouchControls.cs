// On-screen touch controls (phones, tablets, emulators). Left: floating joystick (push to the edge to sprint).
// Right half: drag to look. Icon buttons; heals, throwables and deployables are ONE button each with a chevron
// above it that opens the list. Weapon cards above the health bar stay empty until a gun is picked up.
// Every button can be moved and resized in the layout editor (lobby → Settings → Customize controls).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kryvon
{
    public class KryvonTouchControls : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] bool forceOn;               // show on desktop too (testing)
        bool editMode; System.Action onClose;
        PlayerInputReader input; KryvonThrowables thr; KryvonGear gear; KryvonStealth st; WeaponController wc;
        RectTransform root, stickBase, stickKnob; Sprite circle, rounded;

        public static bool Wanted => Application.isMobilePlatform || PlayerPrefs.GetInt("kryvon.touch", 0) == 1 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-touch") >= 0;

        // ---------- the layout: id, anchor (0 bottom-right, 1 bottom-left, 2 bottom-centre), position, size ----------
        public static readonly (string id, int anchor, Vector2 pos, float size)[] Layout =
        {
            ("FIRE", 0, new Vector2(-270, 320), 170), ("FIRE_L", 1, new Vector2(150, 640), 120), ("AIM", 0, new Vector2(-120, 520), 110),
            ("JUMP", 0, new Vector2(-110, 330), 100), ("CROUCH", 0, new Vector2(-420, 120), 92), ("PRONE", 0, new Vector2(-300, 110), 86),
            ("RELOAD", 0, new Vector2(-460, 300), 92), ("F", 0, new Vector2(-610, 360), 100),
            ("THROW", 0, new Vector2(-140, 700), 100), ("DEPLOY", 0, new Vector2(-300, 790), 84), ("MED", 0, new Vector2(-440, 790), 84),
            ("MUD", 0, new Vector2(-580, 820), 72), ("CLOAK", 0, new Vector2(-690, 820), 72), ("NVG", 0, new Vector2(-800, 820), 72), ("EMOTE", 1, new Vector2(90, 600), 70),
            ("BREATH", 0, new Vector2(-270, 560), 92),
            ("PEEK_L", 1, new Vector2(90, 480), 78), ("PEEK_R", 1, new Vector2(200, 480), 78),
            ("VIEW", 1, new Vector2(200, 600), 70), ("SCOPE", 0, new Vector2(-30, 640), 64),
            ("W1", 2, new Vector2(-270, 160), 1), ("W2", 2, new Vector2(0, 160), 1), ("W3", 2, new Vector2(240, 160), 0.8f),
        };
        public static bool AutoSprintOn
        {
            get => PlayerPrefs.GetInt("kryvon.autosprint", 1) == 1;
            set { PlayerPrefs.SetInt("kryvon.autosprint", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        bool sprintLock; float sprintHold;
        static string Key(string id) => "kryvon.ui." + id;
        static (Vector2 pos, float size) Saved(string id, Vector2 pos, float size)
        {
            var s = PlayerPrefs.GetString(Key(id), ""); var p = s.Split(';');
            if (p.Length == 3 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
                && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)) return (new Vector2(x, y), z);
            return (pos, size);
        }
        static Vector2 AnchorOf(int a) => a == 0 ? new Vector2(1, 0) : a == 1 ? Vector2.zero : new Vector2(0.5f, 0);
        readonly Dictionary<string, RectTransform> items = new();
        readonly Dictionary<string, float> baseSize = new();

        /// opens the layout editor over whatever is on screen (the lobby)
        public static void OpenEditor(Font f, System.Action closed)
        {
            var go = new GameObject("TouchLayoutEditor"); var t = go.AddComponent<KryvonTouchControls>();
            t.font = f; t.editMode = true; t.onClose = closed;
        }

        void Start()
        {
            if (!editMode && !forceOn && !Wanted) { enabled = false; return; }
            if (!editMode) { input = GetComponent<PlayerInputReader>(); thr = GetComponent<KryvonThrowables>(); gear = GetComponent<KryvonGear>(); st = GetComponent<KryvonStealth>(); wc = GetComponent<WeaponController>(); }
            if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            circle = MakeCircle(); rounded = MakeRounded();
            var cg = new GameObject(editMode ? "TouchLayoutUI" : "TouchUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (editMode) cg.transform.SetParent(transform, false);
            var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = editMode ? 200 : 50;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
            root = (RectTransform)cg.transform;

            if (editMode) { var bg = Area("Backdrop", Vector2.zero, Vector2.one); bg.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.92f); }
            else
            {
                var look = Area("Look", new Vector2(0.4f, 0), new Vector2(1, 1));
                var lt = look.gameObject.AddComponent<TouchPad>(); lt.onDrag = d => input.TouchLook(d * 1.2f);
            }
            var joy = editMode ? null : Area("Joystick", Vector2.zero, new Vector2(0.4f, 0.75f));
            stickBase = Circle(root, "StickBase", new Vector2(260, 260), new Color(1, 1, 1, 0.12f)); stickBase.anchorMin = stickBase.anchorMax = Vector2.zero; stickBase.anchoredPosition = new Vector2(300, 280);
            stickKnob = Circle(stickBase, "Knob", new Vector2(110, 110), new Color(1, 1, 1, 0.35f));
            var sp = Icon("sprint"); if (sp) { var h = Circle(stickBase, "SprintHint", new Vector2(64, 64), Dark); h.anchoredPosition = new Vector2(0, 175); IconImg(h, sp, 40); }
            if (joy)
            {
                var jt = joy.gameObject.AddComponent<TouchPad>();
                jt.onDown = p => { stickBase.position = p; stickKnob.anchoredPosition = Vector2.zero; if (sprintLock) { sprintLock = false; input.TouchMove(Vector2.zero); input.TouchSprint(false); } };
                jt.onMove = p =>
                {
                    Vector2 d = (Vector2)(p - (Vector2)stickBase.position) / (stickBase.lossyScale.x * 130f);
                    var v = Vector2.ClampMagnitude(d, 1.25f);
                    stickKnob.anchoredPosition = Vector2.ClampMagnitude(d, 1f) * 130f;
                    bool sprintZone = v.magnitude > 1.15f && v.y > 0.6f;
                    input.TouchMove(Vector2.ClampMagnitude(v, 1f)); input.TouchSprint(sprintZone);
                    // auto-sprint: keep the stick in the sprint zone ~0.6 s and let go – you keep running until you touch the stick again
                    sprintHold = sprintZone ? sprintHold + Time.unscaledDeltaTime : 0f;
                    if (AutoSprintOn && sprintHold > 0.6f && !sprintLock) { sprintLock = true; KryvonKillFeed.Notice("AUTO-SPRINT ON  ·  touch the stick to stop"); }
                    if (sprintLock && !sprintZone && v.magnitude > 0.2f) sprintLock = false;     // steering away ends it
                };
                jt.onUp = () =>
                {
                    stickKnob.anchoredPosition = Vector2.zero; sprintHold = 0f;
                    if (sprintLock) { input.TouchMove(Vector2.up); input.TouchSprint(true); return; }
                    input.TouchMove(Vector2.zero); input.TouchSprint(false);
                };
            }

            var red = new Color(0.55f, 0.1f, 0.08f, 0.5f);
            Hold("FIRE", "fire", red, b => input.TouchFire(b), true);
            Hold("FIRE_L", "fire", red, b => input.TouchFire(b), true);
            Hold("AIM", "aim", Dark, b => input.TouchAim(b));
            Tap("JUMP", "jump", () => input.TouchJump());
            Tap("CROUCH", "crouch", () => input.TouchCrouch());
            Tap("PRONE", "prone", () => input.TouchProne());
            Tap("RELOAD", "reload", () => input.TouchReload());
            Tap("F", "interact", () => input.TouchInteract(), new Color(0.75f, 0.52f, 0.12f, 0.45f));
            // hold breath (only while looking through a 2x+ scope): steadies the sway (KryvonVitals reads it as the sprint key)
            var br = Hold("BREATH", "breath", Dark, b => { if (input) input.TouchSprint(b); });
            { var tx = br.GetComponentInChildren<Text>(); if (tx) { tx.text = "BREATH"; tx.fontSize = 18; } }
            // peek: tap to lean out left / right, tap again (or the other side) to come back – like the big BR games
            var pl = Tap("PEEK_L", "peek_l", () => { leanState = leanState == -1 ? 0 : -1; if (input) input.TouchLean(leanState); });
            var pr = Tap("PEEK_R", "peek_r", () => { leanState = leanState == 1 ? 0 : 1; if (input) input.TouchLean(leanState); });
            foreach (var (r, t) in new[] { (pl, "◄ PEEK"), (pr, "PEEK ►") }) { var tx = r.GetComponentInChildren<Text>(); if (tx) { tx.text = t; tx.fontSize = 18; } }
            peekL = pl.GetComponent<Image>(); peekR = pr.GetComponent<Image>();
            // eye: TPP ⇄ FPP in one tap · sight: swap to the next scope you carry (iron → red dot → 4x …)
            var vw = Tap("VIEW", "view", () => { if (input) input.TouchView(); });
            { var tx = vw.GetComponentInChildren<Text>(); if (tx) { tx.text = "TPP/FPP"; tx.fontSize = 16; } }
            var scb = Tap("SCOPE", "scope", () => { if (input) input.TouchScope(); });
            { var tx = scb.GetComponentInChildren<Text>(); if (tx) { tx.text = "⊕ SIGHT"; tx.fontSize = 14; } }
            if (!editMode)
            {   // the drop: one big gold button that says what it does – JUMP in the jet, OPEN PARACHUTE in free fall
                var d = new GameObject("DropButton", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); d.SetParent(root, false);
                d.anchorMin = d.anchorMax = d.pivot = new Vector2(1, 0.5f); d.anchoredPosition = new Vector2(-190, 40); d.sizeDelta = new Vector2(170, 170);
                var di = d.GetComponent<Image>(); di.sprite = circle; di.color = new Color(0.831f, 0.686f, 0.216f, 0.95f);   // #D4AF37
                dropBg = di; dropIcons = new[] { Icon("drop_jump"), Icon("drop_open"), Icon("drop_close") };
                dropImg = IconImg(d, dropIcons[0], 130); dropImg.color = new Color(0.07f, 0.07f, 0.07f);
                d.gameObject.AddComponent<Button>().onClick.AddListener(() => input.TouchInteract());
                dropBtn = d; d.gameObject.SetActive(false);
            }
            if (editMode || thr)
            {
                // throwables: hold the button to cook and aim, release to throw; the chevron picks which one
                var tb = Hold("THROW", "throw", Dark, b => { if (b) thr.TouchThrowDown(); else thr.TouchThrowUp(); });
                throwUi = Group(tb, "THROW", ThrowIcons, KryvonThrowables.Names, i => thr != null ? thr.Count((Throwable)i) : 1, i => thr.Select((Throwable)i), false);
                var db = Tap("DEPLOY", "spike", () => { if (deploySel == 0) thr.TouchSpike(); else thr.TouchShield(); });
                deployUi = Group(db, "DEPLOY", new[] { "spike", "shield" }, new[] { "Spike Strip", "Shield" }, i => thr != null ? (i == 0 ? thr.spikes : thr.shields) : 1, i => deploySel = i, false);
            }
            if (editMode || gear)
            {
                // heals and boosts: tap uses the shown item; the chevron opens the list (tapping an item uses it right away)
                var mb = Tap("MED", "med", () => { var c = medSel >= 0 && gear.counts[medSel] > 0 ? (Consumable)medSel : BestHeal(); gear.StartUse(c); });
                medUi = Group(mb, "MED", MedIcons, KryvonGear.Names, i => gear != null ? gear.counts[i] : 1, i => { medSel = i; gear.StartUse((Consumable)i); }, true);
            }
            if (editMode || st)
            {
                var m = Tap("MUD", "mud", () => st.TouchMud()); Caption(m, "Mud", 16, Vector2.zero, new Vector2(1, 0), new Vector2(-20, -24), new Vector2(20, -2));
                var c = Tap("CLOAK", "cloak", () => st.TouchCloak()); Caption(c, "Cloak", 16, Vector2.zero, new Vector2(1, 0), new Vector2(-20, -24), new Vector2(20, -2));
                var em = Tap("EMOTE", "sprint", () => { if (KryvonEmote.Mine) KryvonEmote.Mine.Touch(); }); Caption(em, "Emote", 16, Vector2.zero, new Vector2(1, 0), new Vector2(-20, -24), new Vector2(20, -2));
                var nv = Tap("NVG", "scope", () => { if (KryvonNightVision.Mine) KryvonNightVision.Mine.Touch(); }); Caption(nv, "NVG", 16, Vector2.zero, new Vector2(1, 0), new Vector2(-20, -24), new Vector2(20, -2));
            }
            for (int s = 0; s < 3; s++) WeaponCard(s);
            if (editMode) BuildEditorBar();
        }

        // ---------- helpers ----------
        static readonly Color Dark = new(0.06f, 0.07f, 0.09f, 0.42f);
        static readonly string[] MedIcons = { "bandage", "firstaid", "med", "drink", "pills", "syringe" };
        static readonly string[] ThrowIcons = { "throw", "molotov", "smoke", "flash", "flare" };
        static Sprite MakeCircle()
        {
            int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f); float a = Mathf.Clamp01((1f - d) * 40f); float ring = d > 0.9f ? 1f : 0.55f; t.SetPixel(x, y, new Color(1, 1, 1, a * ring)); }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        static Sprite MakeRounded()
        {
            int S = 64, R = 14; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(R - x, x - (S - 1 - R))), dy = Mathf.Max(0, Mathf.Max(R - y, y - (S - 1 - R)));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - R; bool edge = d > -2.5f; t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d) * (edge ? 1f : 0.6f)));
            }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * (R + 1));
        }
        static readonly Dictionary<string, Sprite> icons = new();
        static Sprite Icon(string key)
        {
            if (icons.TryGetValue(key, out var s)) return s;
            var t = Resources.Load<Texture2D>("UI/Icons/" + key);
            s = t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)) : null; icons[key] = s; return s;
        }
        static Image IconImg(RectTransform parent, Sprite s, float size)
        {
            var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(parent, false);
            var im = ig.GetComponent<Image>(); im.sprite = s; im.preserveAspect = true; im.raycastTarget = false; im.color = new Color(1, 1, 1, 0.92f);
            var ir = (RectTransform)ig.transform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f); ir.sizeDelta = Vector2.one * size; return im;
        }
        Text Caption(RectTransform parent, string s, int size, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var tg = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow)); tg.transform.SetParent(parent, false);
            var t = tg.GetComponent<Text>(); t.text = s; t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; tg.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            var tr = (RectTransform)tg.transform; tr.anchorMin = anchorMin; tr.anchorMax = anchorMax; tr.offsetMin = offMin; tr.offsetMax = offMax; return t;
        }
        RectTransform Area(string n, Vector2 min, Vector2 max)
        {
            var g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(root, false);
            g.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
            var r = (RectTransform)g.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }
        RectTransform Circle(Transform parent, string n, Vector2 size, Color c)
        {
            var g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
            var i = g.GetComponent<Image>(); i.sprite = circle; i.color = c; i.raycastTarget = false;
            var r = (RectTransform)g.transform; r.sizeDelta = size; return r;
        }
        RectTransform Placed(string id, RectTransform r, out float size)
        {
            var def = System.Array.Find(Layout, l => l.id == id);
            var (pos, sz) = Saved(id, def.pos, def.size); size = sz;
            r.anchorMin = r.anchorMax = AnchorOf(def.anchor); r.anchoredPosition = pos;
            items[id] = r; baseSize[id] = def.size;
            if (editMode) { var d = r.gameObject.AddComponent<Dragger>(); d.owner = this; d.id = id; }
            return r;
        }
        RectTransform Button(string id, string icon, Color c)
        {
            var r = Circle(root, id, Vector2.one, c); Placed(id, r, out var size); r.sizeDelta = Vector2.one * size;
            r.GetComponent<Image>().raycastTarget = true;
            var ic = Icon(icon); if (ic) IconImg(r, ic, size * 0.6f).name = "Icon"; else Caption(r, id, 22, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return r;
        }
        RectTransform Tap(string id, string icon, System.Action a, Color? c = null)
        {
            var r = Button(id, icon, c ?? Dark);
            if (!editMode) r.gameObject.AddComponent<TouchPad>().onDown = _ => { CloseLists(); a(); };
            return r;
        }
        RectTransform Hold(string id, string icon, Color c, System.Action<bool> a, bool aimWhileHeld = false)
        {
            var r = Button(id, icon, c); if (editMode) return r;
            var tp = r.gameObject.AddComponent<TouchPad>();
            tp.onDown = _ => { CloseLists(); a(true); }; tp.onUp = () => a(false);
            if (aimWhileHeld) tp.onDrag = d => input.TouchLook(d * 1.2f);
            return r;
        }

        // ---------- grouped buttons: icon + count, chevron above, a list that opens upwards ----------
        class GroupUi { public RectTransform list; public Image icon; public Text count; public string[] icons; public System.Func<int, int> countOf; public List<(Image bg, Text n)> rows = new(); }
        GroupUi medUi, throwUi, deployUi; int medSel = -1, deploySel;
        GroupUi Group(RectTransform btn, string id, string[] iconKeys, string[] names, System.Func<int, int> countOf, System.Action<int> pick, bool closeOnPick)
        {
            var g = new GroupUi { icons = iconKeys, countOf = countOf };
            g.icon = btn.Find("Icon")?.GetComponent<Image>();
            g.count = Caption(btn, "", 18, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-26, 2), new Vector2(4, 24), TextAnchor.MiddleRight);
            var chev = Circle(btn, "Chevron", new Vector2(46, 46), Dark); chev.anchorMin = chev.anchorMax = new Vector2(0.5f, 1); chev.anchoredPosition = new Vector2(0, 30);
            chev.GetComponent<Image>().raycastTarget = !editMode; IconImg(chev, Icon("chevron"), 28);
            var list = new GameObject("List", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); list.SetParent(btn, false);
            list.GetComponent<Image>().sprite = rounded; list.GetComponent<Image>().type = Image.Type.Sliced; list.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.88f);
            list.anchorMin = list.anchorMax = new Vector2(0.5f, 1); list.pivot = new Vector2(0.5f, 0); list.anchoredPosition = new Vector2(0, 60);
            g.list = list;
            list.sizeDelta = new Vector2(280, iconKeys.Length * 66 + 12);
            for (int i = 0; i < iconKeys.Length; i++)
            {
                int idx = i;
                var row = new GameObject("Row", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); row.SetParent(list, false);
                var bg = row.GetComponent<Image>(); bg.sprite = rounded; bg.type = Image.Type.Sliced; bg.color = new Color(1, 1, 1, 0.06f);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1); row.pivot = new Vector2(0.5f, 1); row.anchoredPosition = new Vector2(0, -6 - i * 66); row.sizeDelta = new Vector2(268, 60);
                var ic = IconImg(row, Icon(iconKeys[i]), 44); var ir = (RectTransform)ic.transform; ir.anchorMin = ir.anchorMax = new Vector2(0, 0.5f); ir.anchoredPosition = new Vector2(34, 0);
                Caption(row, names[i], 20, Vector2.zero, Vector2.one, new Vector2(66, 0), new Vector2(-50, 0), TextAnchor.MiddleLeft);
                var n = Caption(row, "", 22, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-14, 0), TextAnchor.MiddleRight);
                g.rows.Add((bg, n));
                if (!editMode) row.gameObject.AddComponent<TouchPad>().onDown = _ => { if (g.countOf(idx) <= 0) return; pick(idx); Show(g, idx); if (closeOnPick) list.gameObject.SetActive(false); };
            }
            list.gameObject.SetActive(false);
            if (!editMode) chev.gameObject.AddComponent<TouchPad>().onDown = _ => { bool open = !list.gameObject.activeSelf; CloseLists(); list.gameObject.SetActive(open); };
            return g;
        }
        void Show(GroupUi g, int i) { if (g.icon) g.icon.sprite = Icon(g.icons[i]); }
        void AutoOpenMed() { CloseLists(); if (medUi != null) medUi.list.gameObject.SetActive(true); }
        void CloseLists() { foreach (var g in new[] { medUi, throwUi, deployUi }) if (g != null && g.list) g.list.gameObject.SetActive(false); }
        Consumable BestHeal()
        {
            foreach (var c in new[] { Consumable.MedKit, Consumable.FirstAid, Consumable.Bandage, Consumable.Adrenaline, Consumable.Painkillers, Consumable.EnergyDrink }) if (gear.CanUse(c)) return c;
            return Consumable.Bandage;
        }

        // ---------- weapon cards (empty until a gun is picked up, then its silhouette, name and ammo) ----------
        class Card { public Image bg; public RawImage gun; public Text name, ammo, num; }
        readonly Card[] cards = new Card[3];
        void WeaponCard(int s)
        {
            string id = "W" + (s + 1);
            var r = new GameObject(id, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); r.SetParent(root, false);
            Placed(id, r, out var scale); r.sizeDelta = new Vector2(250, 92); r.localScale = Vector3.one * scale;
            var c = new Card { bg = r.GetComponent<Image>() }; c.bg.sprite = rounded; c.bg.type = Image.Type.Sliced; c.bg.color = new Color(0.04f, 0.05f, 0.07f, 0.55f);
            c.gun = new GameObject("Gun", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); c.gun.transform.SetParent(r, false); c.gun.raycastTarget = false;
            var gr = c.gun.rectTransform; gr.anchorMin = new Vector2(0, 0.28f); gr.anchorMax = new Vector2(1, 1); gr.offsetMin = new Vector2(30, 0); gr.offsetMax = new Vector2(-10, -4);
            c.gun.uvRect = new Rect(0, 0.36f, 1, 0.28f);
            c.num = Caption(r, (s + 1).ToString(), 18, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -26), new Vector2(28, -4), TextAnchor.MiddleLeft);
            c.name = Caption(r, "", 16, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 4), new Vector2(-10, 26), TextAnchor.MiddleLeft);
            c.ammo = Caption(r, "", 18, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 4), new Vector2(-10, 26), TextAnchor.MiddleRight);
            cards[s] = c;
            if (!editMode) { int slot = s == 2 ? WeaponController.PistolSlot : s; r.GetComponent<Image>().raycastTarget = true; r.gameObject.AddComponent<TouchPad>().onDown = _ => { CloseLists(); input.TouchSwitch(slot); }; }
            else { c.gun.texture = Icon(s == 2 ? "w_pistol" : "w_ar").texture; c.name.text = s == 2 ? "PISTOL" : "PRIMARY " + (s + 1); }
        }
        static string GunIcon(WeaponClass k) => k switch
        {
            WeaponClass.SMG => "w_smg", WeaponClass.DMR => "w_dmr", WeaponClass.Sniper => "w_sniper", WeaponClass.Shotgun => "w_shotgun",
            WeaponClass.LMG => "w_lmg", WeaponClass.Pistol => "w_pistol", _ => "w_ar",
        };

        float nextRefresh;
        RectTransform dropBtn; Image dropImg, dropBg; Sprite[] dropIcons; int leanState; Image peekL, peekR;
        void Update()
        {
            if (!editMode && dropBtn)
            {
                var dr = KryvonDrop.Instance; bool plane = dr && dr.S == KryvonDrop.Stage.Plane, fall = dr && dr.S == KryvonDrop.Stage.Freefall, chute = dr && dr.S == KryvonDrop.Stage.Chute;
                bool show = plane || fall || chute;
                if (dropBtn.gameObject.activeSelf != show) dropBtn.gameObject.SetActive(show);
                if (show)
                {   // a picture, not words: jumper leaving the jet · open canopy (tap = open) · canopy with a cross (tap = close)
                    var want = plane ? dropIcons[0] : fall ? dropIcons[1] : dropIcons[2];
                    if (dropImg.sprite != want) dropImg.sprite = want;
                    bool can = !chute || dr.CanCloseChute;
                    dropBg.color = chute ? new Color(0.75f, 0.24f, 0.2f, can ? 0.9f : 0.3f) : new Color(0.831f, 0.686f, 0.216f, 0.95f);
                    dropImg.color = new Color(0.07f, 0.07f, 0.07f, can ? 1f : 0.35f);
                    dropBtn.localScale = Vector3.one * (fall ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f) : 1f);
                }
            }
            if (peekL) peekL.color = leanState == -1 ? new Color(0.831f, 0.686f, 0.216f, 0.7f) : Dark;
            if (peekR) peekR.color = leanState == 1 ? new Color(0.831f, 0.686f, 0.216f, 0.7f) : Dark;
            if (!editMode && leanState != 0 && input && wc && GetComponent<PlayerController>() is var pc && pc && pc.IsSprinting) { leanState = 0; input.TouchLean(0); }   // sprinting ends the peek
            if (editMode || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            if (wc)
                for (int s = 0; s < 3; s++)
                {
                    var c = cards[s]; var slot = wc.Slot(s == 2 ? WeaponController.PistolSlot : s); var d = slot?.data;
                    bool cur = wc.CurrentIndex == (s == 2 ? WeaponController.PistolSlot : s) && d != null;
                    c.gun.enabled = d != null; if (d != null) c.gun.texture = Icon(GunIcon(d.weaponClass))?.texture;
                    c.gun.color = cur ? new Color(1f, 0.85f, 0.45f) : new Color(1, 1, 1, 0.85f);
                    c.name.text = d != null ? d.displayName : "EMPTY"; c.name.color = d != null ? Color.white : new Color(1, 1, 1, 0.35f);
                    c.ammo.text = d != null ? $"{slot.ammo} / {wc.Reserve(d.ammoType)}" : "";
                    c.bg.color = cur ? new Color(0.45f, 0.3f, 0.06f, 0.65f) : new Color(0.04f, 0.05f, 0.07f, 0.55f);
                }
            if (medUi != null && gear)
            {
                int shown = medSel >= 0 && gear.counts[medSel] > 0 ? medSel : (int)BestHeal(); Show(medUi, shown);
                medUi.count.text = gear.counts[shown].ToString(); if (medUi.icon) medUi.icon.color = gear.counts[shown] > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
                for (int i = 0; i < medUi.rows.Count; i++) Row(medUi.rows[i], gear.counts[i], i == shown);
            }
            if (throwUi != null && thr)
            {
                int sel = (int)thr.Selected; Show(throwUi, sel); throwUi.count.text = thr.Count(thr.Selected).ToString();
                if (throwUi.icon) throwUi.icon.color = thr.Count(thr.Selected) > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
                for (int i = 0; i < throwUi.rows.Count; i++) Row(throwUi.rows[i], thr.Count((Throwable)i), i == sel);
            }
            if (deployUi != null && thr)
            {
                int n = deploySel == 0 ? thr.spikes : thr.shields; Show(deployUi, deploySel); deployUi.count.text = n.ToString();
                if (deployUi.icon) deployUi.icon.color = n > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
                Row(deployUi.rows[0], thr.spikes, deploySel == 0); Row(deployUi.rows[1], thr.shields, deploySel == 1);
            }
            // a button only shows when you carry something it can use (like the reference games)
            int heals = 0, throws = 0; if (gear) foreach (var n in gear.counts) heals += n;
            if (thr) for (int i = 0; i < 5; i++) throws += thr.Count((Throwable)i);
            Vis("MED", heals > 0); Vis("THROW", throws > 0 || (thr && thr.Cook >= 0)); Vis("DEPLOY", thr && thr.spikes + thr.shields > 0);
            Vis("CLOAK", st && (st.cloakCharges > 0 || st.CloakLeft > 0));
            Vis("NVG", KryvonNightVision.Mine != null);
            Vis("BREATH", wc && wc.IsAds && wc.CurrentScope >= ScopeType.X2);
            Vis("SCOPE", wc && wc.CanCycleScope);
            if (deployUi != null && thr && (deploySel == 0 ? thr.spikes : thr.shields) <= 0 && (deploySel == 0 ? thr.shields : thr.spikes) > 0) deploySel = 1 - deploySel;
        }
        void Vis(string id, bool on) { if (items.TryGetValue(id, out var r) && r && r.gameObject.activeSelf != on) r.gameObject.SetActive(on); }
        static void Row((Image bg, Text n) r, int count, bool sel)
        {
            r.n.text = "×" + count; r.n.color = count > 0 ? Color.white : new Color(1, 1, 1, 0.35f);
            r.bg.color = sel ? new Color(1f, 0.7f, 0.2f, 0.35f) : new Color(1, 1, 1, count > 0 ? 0.08f : 0.03f);
        }

        // ---------- layout editor ----------
        string selected; Text selLabel; UnityEngine.UI.Slider sizeSlider; bool syncing;
        internal void Select(string id)
        {
            selected = id;
            foreach (var kv in items) { var img = kv.Value.GetComponent<Image>(); var o = kv.Value.GetComponent<Outline>() ?? kv.Value.gameObject.AddComponent<Outline>(); o.effectColor = kv.Key == id ? new Color(1f, 0.75f, 0.2f) : Color.clear; o.effectDistance = new Vector2(4, -4); }
            selLabel.text = "SELECTED: " + id; syncing = true; sizeSlider.value = Scale(id); syncing = false;
        }
        float Scale(string id) { var r = items[id]; return id.StartsWith("W") ? r.localScale.x : r.sizeDelta.x / baseSize[id]; }
        void SetScale(string id, float k)
        {
            var r = items[id];
            if (id.StartsWith("W")) r.localScale = Vector3.one * k;
            else { r.sizeDelta = Vector2.one * baseSize[id] * k; var ic = r.Find("Icon") as RectTransform; if (ic) ic.sizeDelta = r.sizeDelta * 0.6f; }
        }
        void BuildEditorBar()
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); bar.SetParent(root, false);
            var bi = bar.GetComponent<Image>(); bi.sprite = rounded; bi.type = Image.Type.Sliced; bi.color = new Color(0.03f, 0.035f, 0.045f, 0.95f);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1); bar.pivot = new Vector2(0.5f, 1); bar.anchoredPosition = new Vector2(0, -16); bar.sizeDelta = new Vector2(1280, 120);
            Caption(bar, "CUSTOMIZE CONTROLS  ·  drag a button to move it, use the slider to resize it", 20, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -36), new Vector2(-20, -6), TextAnchor.MiddleLeft);
            selLabel = Caption(bar, "SELECTED: –", 20, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 12), new Vector2(260, 60), TextAnchor.MiddleLeft);
            // size slider
            var track = new GameObject("Size", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); track.SetParent(bar, false);
            track.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f); track.anchorMin = track.anchorMax = new Vector2(0, 0); track.pivot = new Vector2(0, 0.5f); track.anchoredPosition = new Vector2(280, 36); track.sizeDelta = new Vector2(340, 10);
            var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); fillArea.SetParent(track, false); fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one; fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); fill.SetParent(fillArea, false); fill.GetComponent<Image>().color = new Color(1f, 0.78f, 0.32f); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            var hArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); hArea.SetParent(track, false); hArea.anchorMin = Vector2.zero; hArea.anchorMax = Vector2.one; hArea.offsetMin = hArea.offsetMax = Vector2.zero;
            var handle = Circle(hArea, "Handle", new Vector2(34, 34), Color.white); handle.GetComponent<Image>().raycastTarget = true;
            sizeSlider = track.gameObject.AddComponent<UnityEngine.UI.Slider>(); sizeSlider.fillRect = fill; sizeSlider.handleRect = handle; sizeSlider.targetGraphic = handle.GetComponent<Image>();
            sizeSlider.minValue = 0.6f; sizeSlider.maxValue = 1.6f; sizeSlider.value = 1f; track.GetComponent<Image>().raycastTarget = true;
            sizeSlider.onValueChanged.AddListener(v => { if (!syncing && selected != null) SetScale(selected, v); });
            BarBtn(bar, "RESET", new Vector2(-470, 0), new Color(0.6f, 0.16f, 0.14f), () => { foreach (var l in Layout) PlayerPrefs.DeleteKey(Key(l.id)); PlayerPrefs.Save(); Rebuild(); });
            BarBtn(bar, "CANCEL", new Vector2(-280, 0), new Color(1, 1, 1, 0.12f), Close);
            BarBtn(bar, "SAVE", new Vector2(-90, 0), new Color(1f, 0.78f, 0.32f), () =>
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (var kv in items) PlayerPrefs.SetString(Key(kv.Key), string.Format(ci, "{0};{1};{2}", kv.Value.anchoredPosition.x, kv.Value.anchoredPosition.y, Scale(kv.Key) * (kv.Key.StartsWith("W") ? 1f : baseSize[kv.Key])));
                PlayerPrefs.Save(); Close();
            }, Color.black);
        }
        void BarBtn(RectTransform bar, string label, Vector2 pos, Color c, System.Action a, Color? fg = null)
        {
            var b = new GameObject(label, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); b.SetParent(bar, false);
            var i = b.GetComponent<Image>(); i.sprite = rounded; i.type = Image.Type.Sliced; i.color = c; i.raycastTarget = true;
            b.anchorMin = b.anchorMax = new Vector2(1, 0); b.pivot = new Vector2(1, 0); b.anchoredPosition = pos + new Vector2(-20, 14); b.sizeDelta = new Vector2(170, 56);
            var t = Caption(b, label, 24, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); if (fg.HasValue) t.color = fg.Value;
            b.gameObject.AddComponent<Button>().onClick.AddListener(() => a());
        }
        void Rebuild() { var cb = onClose; var f = font; Destroy(gameObject); OpenEditor(f, cb); }
        void Close() { Destroy(gameObject); onClose?.Invoke(); }
    }

    /// pointer handler used by the touch controls
    public class TouchPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public System.Action<Vector2> onDown, onMove, onDrag; public System.Action onUp;
        public void OnPointerDown(PointerEventData e) { onDown?.Invoke(e.position); onMove?.Invoke(e.position); }
        public void OnDrag(PointerEventData e) { onMove?.Invoke(e.position); onDrag?.Invoke(e.delta); }
        public void OnPointerUp(PointerEventData e) => onUp?.Invoke();
    }

    /// layout editor: drag a control to move it
    public class Dragger : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        internal KryvonTouchControls owner; internal string id;
        public void OnPointerDown(PointerEventData e) => owner.Select(id);
        public void OnDrag(PointerEventData e)
        {
            var r = (RectTransform)transform; var cv = GetComponentInParent<Canvas>();
            r.anchoredPosition += e.delta / (cv ? cv.scaleFactor : 1f);
        }
    }
}

// KRAYVON design system for the main menu (owner's guidelines):
//   matte black glass #121212 · tactical olive #4A5D4E · crimson / amber highlights · white #FFFFFF and grey #B0B0B0 text
//   semi-transparent dark panels with sharp corners and thin olive borders; the selected element glows amber.
// Applied over the lobby after it is built, plus three new pieces:
//   READY FOR BATTLE – the crimson main button (was the gold START)
//   OPERATORS        – character buttons on the right: portrait, name, role badge, the selected one glows
//   COMBAT RECORD    – the player's career stats as a glass strip under the logo (matches, wins, kills, K/D, top 10, headshots)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kryvon
{
    public partial class KryvonLobby
    {
        static readonly Color TacGlass = new(0.071f, 0.071f, 0.071f, 0.74f);     // #121212, glass
        static readonly Color TacSolid = new(0.071f, 0.071f, 0.071f, 0.92f);
        static readonly Color Olive = new(0.29f, 0.365f, 0.306f, 1f);            // #4A5D4E
        static readonly Color OliveEdge = new(0.42f, 0.53f, 0.44f, 0.85f);
        static readonly Color Crimson = new(0.753f, 0.224f, 0.169f, 1f);          // #C0392B danger / hits
        static readonly Color Charcoal = new(0.118f, 0.118f, 0.118f, 1f);       // #1E1E1E
        static readonly Color Sand = new(0.761f, 0.698f, 0.502f, 1f);           // #C2B280 desert camo
        static readonly Color Amber = new(0.831f, 0.686f, 0.216f, 1f);          // #D4AF37 main buttons
        static readonly Color Grey = new(0.69f, 0.69f, 0.69f, 1f);             // #B0B0B0
        const string StartLabel = "READY FOR BATTLE";
        Sprite sharp, frame;
        readonly List<(int hero, Image edge, Image glow)> opChips = new();

        static Sprite MakeFrame(int size, int radius, float thick)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius))), dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;            // <0 inside
                float outer = Mathf.Clamp01(0.5f - d), inner = Mathf.Clamp01(0.5f - (d + thick));
                t.SetPixel(x, y, new Color(1, 1, 1, outer - inner));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * (radius + thick + 1));
        }

        /// dark glass panel with a thin olive edge (the edge sits under the content)
        Image Edge(RectTransform r, Color c)
        {
            var e = r.Find("Edge") as RectTransform;
            if (!e) { e = Panel(r, "Edge", c, frame); Fill(e); e.SetAsFirstSibling(); }
            var im = e.GetComponent<Image>(); im.color = c; im.sprite = frame; im.type = Image.Type.Sliced; return im;
        }
        void Glass(RectTransform r, Color? fill = null, Color? edge = null)
        {
            var img = r.GetComponent<Image>(); if (!img) return;
            img.sprite = sharp; img.type = Image.Type.Sliced; img.color = fill ?? TacGlass;
            Edge(r, edge ?? OliveEdge);
        }
        /// a soft glow behind an element (selection, the main button)
        Image GlowBehind(RectTransform r, Color c, float grow)
        {
            var g = Panel(r.parent, r.name + "Glow", c, glow); g.anchorMin = r.anchorMin; g.anchorMax = r.anchorMax; g.pivot = r.pivot;
            g.sizeDelta = r.sizeDelta + Vector2.one * grow;
            g.anchoredPosition = r.anchoredPosition - new Vector2((r.pivot.x - 0.5f) * grow, (r.pivot.y - 0.5f) * grow);
            g.SetSiblingIndex(r.GetSiblingIndex()); return g.GetComponent<Image>();
        }

        void ApplyTheme()
        {
            sharp = MakeRounded(32, 4); frame = MakeFrame(32, 4, 2f);
            // every glass card of the main screen
            foreach (var img in lobby.GetComponentsInChildren<Image>(true))
            {
                var r = img.rectTransform;
                switch (img.name)
                {
                    case "Card": Glass(r); break;
                    case "Emote": Glass(r, new Color(1, 1, 1, 0.045f), new Color(OliveEdge.r, OliveEdge.g, OliveEdge.b, 0.45f)); break;
                    case "Gold": img.sprite = frame; img.type = Image.Type.Sliced; img.fillCenter = true; img.color = new Color(Amber.r, Amber.g, Amber.b, 0.75f); break;   // primary cards: amber edge
                    case "Frame": img.sprite = frame; img.type = Image.Type.Sliced; img.fillCenter = true; break;
                    case "Tag": if (img.transform.parent == lobby && img.transform.Find("Ready")) Glass(r, TacGlass, new Color(OliveEdge.r, OliveEdge.g, OliveEdge.b, 0.6f)); break;
                }
            }
            foreach (var t in lobby.GetComponentsInChildren<Text>(true)) if (t.color == Gold && t.transform.parent && t.transform.parent.name != "Start") t.color = Amber;

            // READY FOR BATTLE – amber gold #D4AF37 (the main button colour of the design system), charcoal text, gold pulse
            var start = lobby.Find("Start") as RectTransform;
            if (start)
            {
                var si = start.GetComponent<Image>(); si.sprite = sharp; si.type = Image.Type.Sliced; si.color = Amber;
                var shine = start.Find("Shine") as RectTransform; if (shine) { var sh = shine.GetComponent<Image>(); sh.sprite = sharp; sh.color = new Color(1, 1, 1, 0.16f); }
                var deep = Panel(start, "Depth", Color.white, grad); deep.anchorMin = Vector2.zero; deep.anchorMax = new Vector2(1, 0.6f); deep.offsetMin = new Vector2(2, 2); deep.offsetMax = new Vector2(-2, 0); deep.localScale = new Vector3(1, -1, 1); deep.SetSiblingIndex(0);
                deep.GetComponent<Image>().color = new Color(0.35f, 0.25f, 0.02f, 0.4f);
                Edge(start, new Color(1f, 0.92f, 0.6f, 0.9f)).transform.SetAsLastSibling();
                // chevron stripes, a tactical touch
                for (int k = 0; k < 3; k++) { var s = Panel(start, "Chevron", new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.18f + 0.1f * k), sharp); Place(s, new Vector2(0, 0.5f), new Vector2(18 + k * 16, 0), new Vector2(8, 70), new Vector2(0, 0.5f)); s.localRotation = Quaternion.Euler(0, 0, -18); }
                startText.text = StartLabel; startText.fontSize = 54; startText.color = new Color(0.07f, 0.07f, 0.07f);
                startSub.color = new Color(0.12f, 0.1f, 0.05f, 0.85f);
                var oldGlow = lobby.Find("StartGlow"); if (oldGlow) oldGlow.gameObject.SetActive(false);
                var g = GlowBehind(start, new Color(Amber.r, Amber.g, Amber.b, 0.35f), 70);
                StartCoroutine(PulseColor(g, Amber, 0.18f, 0.4f));
            }
            BuildOperators();
            BuildRecord();
            ThemeTabs(-1);
        }
        IEnumerator PulseColor(Image i, Color c, float a0, float a1) { while (i) { i.color = new Color(c.r, c.g, c.b, Mathf.Lerp(a0, a1, 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f))); yield return null; } }

        /// nav selection in the design system: olive plate, amber edge, white text
        void ThemeTabs(int sel)
        {
            if (!sharp) return;
            for (int k = 0; k < tabBgs.Count; k++)
            {
                var b = tabBgs[k]; if (!b) continue; bool on = k == sel;
                b.sprite = sharp; b.type = Image.Type.Sliced; b.color = on ? new Color(Olive.r, Olive.g, Olive.b, 0.92f) : TacGlass;
                Edge(b.rectTransform, on ? Amber : OliveEdge);
                var tx = b.GetComponentInChildren<Text>(); if (tx) tx.color = on ? Color.white : new Color(1, 1, 1, 0.92f);
            }
        }

        // ---------- OPERATORS: character buttons ----------
        void BuildOperators()
        {
            opChips.Clear();
            var ops = new List<int>();
            for (int i = 0; i < heroes.Length; i++) if (heroes[i].model && !heroes[i].role.StartsWith("OUTFIT")) ops.Add(i);
            if (ops.Count == 0) return;
            const float chipH = 50, gap = 6, head = 38;
            var panel = Panel(lobby, "Operators", TacGlass, sharp); Glass(panel);
            Place(panel, new Vector2(1, 1), new Vector2(-28, -98), new Vector2(380, head + ops.Count * (chipH + gap) + 46), new Vector2(1, 1));
            perkText = Label(panel, "", bodyBold, 14, new Color(0.761f, 0.698f, 0.502f), TextAnchor.MiddleLeft); Place(perkText.rectTransform, new Vector2(0, 0), new Vector2(14, 8), new Vector2(356, 30), new Vector2(0, 0));
            perkText.horizontalOverflow = HorizontalWrapMode.Wrap; perkText.resizeTextForBestFit = true; perkText.resizeTextMinSize = 10; perkText.resizeTextMaxSize = 14;
            var hdr = Label(panel, "OPERATORS", display, 24, Amber); Place(hdr.rectTransform, new Vector2(0, 1), new Vector2(14, -6), new Vector2(200, 28), new Vector2(0, 1));
            var cnt = Label(panel, $"{ops.Count} AVAILABLE", bodyBold, 14, Grey, TextAnchor.MiddleRight); Place(cnt.rectTransform, new Vector2(1, 1), new Vector2(-14, -8), new Vector2(160, 24), new Vector2(1, 1));
            for (int n = 0; n < ops.Count; n++)
            {
                int hi = ops[n]; var h = heroes[hi];
                var chip = Panel(panel, "Op", new Color(1, 1, 1, 0.04f), sharp);
                Place(chip, new Vector2(0, 1), new Vector2(8, -(head + n * (chipH + gap))), new Vector2(364, chipH), new Vector2(0, 1));
                var glowImg = Panel(chip, "Sel", new Color(Amber.r, Amber.g, Amber.b, 0.14f), sharp); Fill(glowImg);
                var edge = Edge(chip, OliveEdge);
                // portrait (face crop of the outfit card)
                var ph = Panel(chip, "Photo", new Color(0, 0, 0, 0.5f), sharp); Place(ph, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(chipH - 8, chipH - 8), new Vector2(0, 0.5f));
                var face = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); face.transform.SetParent(ph, false); face.raycastTarget = false;
                face.texture = Thumb(h.id); face.uvRect = new Rect(0.3f, 0.66f, 0.4f, 0.28f); Fill(face.rectTransform, 1);
                var nm = Label(chip, h.name, display, 24, Color.white); Place(nm.rectTransform, new Vector2(0, 0.5f), new Vector2(chipH + 6, 0), new Vector2(150, chipH), new Vector2(0, 0.5f));
                // role badge (first word: RECON, MEDIC, ENGINEER, HEAVY, SNIPER, ASSAULT)
                string role = h.role.Split(' ')[0].Trim();
                if (role == "FIELD") role = "MEDIC";
                var badge = Panel(chip, "Role", new Color(Olive.r, Olive.g, Olive.b, 0.95f), sharp); Place(badge, new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(116, 26), new Vector2(1, 0.5f));
                Edge(badge, new Color(0.6f, 0.72f, 0.6f, 0.6f));
                Label(badge, role, bodyBold, 14, Color.white, TextAnchor.MiddleCenter);
                // the Jordan flag patch on every operator (the brand marker)
                var flag = Panel(chip, "Flag", Color.white, null); Place(flag, new Vector2(1, 0.5f), new Vector2(-132, 0), new Vector2(22, 14), new Vector2(1, 0.5f)); JordanFlag(flag);
                Btn(chip, () => { ShowHero(hi); ThemeOperators(); });
                opChips.Add((hi, edge, glowImg.GetComponent<Image>()));
            }
            ThemeOperators();
        }
        Text perkText;
        void ThemeOperators()
        {
            if (perkText && heroes.Length > 0) perkText.text = "SKILL:  " + KryvonClass.Perk(KryvonClass.Of(heroes[hero].id));
            foreach (var (hi, edge, g) in opChips) { bool on = hi == hero; edge.color = on ? Amber : new Color(OliveEdge.r, OliveEdge.g, OliveEdge.b, 0.5f); g.enabled = on; }
        }
        static Texture2D jordanTex;
        void JordanFlag(RectTransform r)
        {
            if (!jordanTex)
            {   // black / white / green stripes, red chevron with the white star
                const int W = 44, H = 28; jordanTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    Color c = y >= H * 2 / 3 ? Color.black : y >= H / 3 ? Color.white : new Color(0, 0.48f, 0.24f);
                    float tri = (W * 0.45f) * (1f - Mathf.Abs(y - H / 2f + 0.5f) / (H / 2f));
                    if (x < tri) c = new Color(0.81f, 0.07f, 0.15f);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(W * 0.15f, H / 2f - 0.5f)) < 1.6f) c = Color.white;
                    jordanTex.SetPixel(x, y, c);
                }
                jordanTex.Apply();
            }
            var img = r.GetComponent<Image>(); if (img) img.enabled = false;        // a GameObject can hold only one Graphic: draw the flag on a child
            var ri = new GameObject("Jordan", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); ri.transform.SetParent(r, false);
            ri.texture = jordanTex; ri.raycastTarget = false; Fill(ri.rectTransform);
        }

        /// military rank from performance: survival (top 10, wins), eliminations and matches played
        public static readonly (string name, int points)[] Ranks =
        { ("PRIVATE", 0), ("CORPORAL", 60), ("SERGEANT", 180), ("LIEUTENANT", 400), ("CAPTAIN", 800), ("MAJOR", 1400), ("COLONEL", 2300), ("COMMANDER", 3600) };
        public static int RankPoints() => KryvonWallet.Stat("kills") * 10 + KryvonWallet.Stat("wins") * 60 + KryvonWallet.Stat("top10") * 15 + KryvonWallet.Stat("matches") * 3;
        static string Rank() { int pts = RankPoints(); string r = Ranks[0].name; foreach (var k in Ranks) if (pts >= k.points) r = k.name; return r; }

        // ---------- COMBAT RECORD: career stats ----------
        void BuildRecord()
        {
            int matches = KryvonWallet.Stat("matches"), wins = KryvonWallet.Stat("wins"), kills = KryvonWallet.Stat("kills"), deaths = KryvonWallet.Stat("deaths");
            int top10 = KryvonWallet.Stat("top10"), hits = KryvonWallet.Stat("hits"), heads = KryvonWallet.Stat("headshots");
            float kd = kills / (float)Mathf.Max(1, deaths);
            var stats = new (string label, string value, Color c)[]
            {
                ("RANK", Rank(), Sand),
                ("MATCHES", matches.ToString(), Color.white),
                ("WINS", wins.ToString(), Amber),
                ("KILLS", kills.ToString(), Color.white),
                ("K/D", kd.ToString("0.00"), kd >= 1f ? new Color(0.55f, 0.85f, 0.55f) : Color.white),
                ("TOP 10", top10.ToString(), Color.white),
                ("HEADSHOT", hits > 0 ? Mathf.RoundToInt(100f * heads / hits) + "%" : "–", new Color(1f, 0.45f, 0.4f)),
            };
            const float cellW = 104;
            var bar = Panel(lobby, "Record", TacGlass, sharp); Glass(bar);
            Place(bar, new Vector2(0.5f, 1), new Vector2(0, -136), new Vector2(cellW * stats.Length + 16, 70), new Vector2(0.5f, 1));
            var cap = Panel(bar, "Caption", new Color(Olive.r, Olive.g, Olive.b, 0.95f), sharp); Place(cap, new Vector2(0.5f, 1), new Vector2(0, 11), new Vector2(170, 22), new Vector2(0.5f, 1));
            Edge(cap, new Color(0.6f, 0.72f, 0.6f, 0.6f)); Label(cap, "COMBAT RECORD", bodyBold, 13, Color.white, TextAnchor.MiddleCenter);
            for (int i = 0; i < stats.Length; i++)
            {
                var cell = new GameObject("Stat", typeof(RectTransform)).GetComponent<RectTransform>(); cell.SetParent(bar, false);
                Place(cell, new Vector2(0, 0.5f), new Vector2(8 + i * cellW, -2), new Vector2(cellW, 60), new Vector2(0, 0.5f));
                var v = Label(cell, stats[i].value, display, 30, stats[i].c, TextAnchor.MiddleCenter); v.rectTransform.offsetMin = new Vector2(4, 18); v.rectTransform.offsetMax = new Vector2(-4, 0);
                v.horizontalOverflow = HorizontalWrapMode.Wrap; v.resizeTextForBestFit = true; v.resizeTextMinSize = 12; v.resizeTextMaxSize = 30;   // long ranks (LIEUTENANT) shrink to fit
                var l = Label(cell, stats[i].label, bodyBold, 12, Grey, TextAnchor.MiddleCenter); l.rectTransform.offsetMax = new Vector2(0, -38);
                if (i > 0) { var sep = Panel(cell, "Sep", new Color(OliveEdge.r, OliveEdge.g, OliveEdge.b, 0.35f)); Place(sep, new Vector2(0, 0.5f), Vector2.zero, new Vector2(1, 38), new Vector2(0, 0.5f)); }
            }
        }
    }
}

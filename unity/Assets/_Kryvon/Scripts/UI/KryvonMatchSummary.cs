// End of match – the cinematic summary (design system: charcoal glass, olive edges, gold for the main action):
//   VICTORY (gold) or #place (white) with who took you out, then the match in numbers that count up one after the other:
//   PLACE · KILLS · DAMAGE · HEADSHOTS · SURVIVED, the rank bar moving from the old rank points to the new ones,
//   coins earned, and BACK TO LOBBY (through the real loading screen).
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kryvon
{
    public class KryvonMatchSummary : MonoBehaviour
    {
        public struct Result
        {
            public bool won; public int place, players, kills, damage, headshots, coins, rankBefore, rankAfter; public float seconds; public string killer, weapon;
        }

        static readonly Color Glass = new(0.071f, 0.071f, 0.071f, 0.86f), OliveEdge = new(0.42f, 0.53f, 0.44f, 0.85f), Gold = new(0.831f, 0.686f, 0.216f),
            Crimson = new(0.753f, 0.224f, 0.169f), Sand = new(0.761f, 0.698f, 0.502f), Grey = new(0.69f, 0.69f, 0.69f);
        Font display, body; Sprite sharp, frame, soft; Result r; RectTransform root;

        public static void Show(Result r, Font display)
        {
            var g = new GameObject("MatchSummary"); var s = g.AddComponent<KryvonMatchSummary>();
            s.r = r; s.display = display ? display : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); s.body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ---------- sprites ----------
        static Sprite Rounded(int size, int radius, float thick = 0, bool softEdge = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius))), dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                float a = softEdge ? Mathf.Clamp01(1f - (d + radius) / radius) : Mathf.Clamp01(0.5f - d);
                if (thick > 0) a -= Mathf.Clamp01(0.5f - (d + thick));
                t.SetPixel(x, y, new Color(1, 1, 1, softEdge ? a * a : a));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * (radius + thick + 1));
        }
        RectTransform Img(Transform p, string n, Color c, Sprite s = null)
        {
            var i = new GameObject(n, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); i.transform.SetParent(p, false);
            i.color = c; i.sprite = s; i.type = s ? Image.Type.Sliced : Image.Type.Simple; i.raycastTarget = false; return i.rectTransform;
        }
        Text Txt(Transform p, string s, Font f, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.transform.SetParent(p, false);
            t.text = s; t.font = f; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(t.rectTransform); return t;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static RectTransform At(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size) { r.anchorMin = r.anchorMax = r.pivot = anchor; r.anchoredPosition = pos; r.sizeDelta = size; return r; }
        RectTransform GlassCard(Transform p, Vector2 pos, Vector2 size, Color edge)
        {
            var c = Img(p, "Card", Glass, sharp); At(c, new Vector2(0.5f, 0.5f), pos, size);
            var e = Img(c, "Edge", edge, frame); Stretch(e); return c;
        }

        void Start()
        {
            sharp = Rounded(32, 4); frame = Rounded(32, 4, 2f); soft = Rounded(128, 60, 0, true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var cg = new GameObject("SummaryUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            cg.transform.SetParent(transform, false);
            var cv = cg.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 500;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cg.transform;
            if (!FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            StartCoroutine(Run(cg.GetComponent<CanvasGroup>()));
        }

        IEnumerator Run(CanvasGroup group)
        {
            group.alpha = 0;
            var dim = Img(root, "Dim", new Color(0.02f, 0.02f, 0.02f, 0.84f)); Stretch(dim);
            var band = Img(root, "Band", new Color(0, 0, 0, 0.55f)); At(band, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(4000, 300));
            var accent = Img(root, "Accent", r.won ? Gold : Crimson); At(accent, new Vector2(0.5f, 0.5f), new Vector2(0, 98), new Vector2(560, 4));
            var glow = Img(root, "Glow", new Color((r.won ? Gold : Crimson).r, (r.won ? Gold : Crimson).g, (r.won ? Gold : Crimson).b, 0.22f), soft); At(glow, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1300, 380));

            // title
            var titleBox = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>(); titleBox.SetParent(root, false); At(titleBox, new Vector2(0.5f, 0.5f), new Vector2(0, 270), new Vector2(1600, 180));
            var title = Txt(titleBox, r.won ? "VICTORY" : $"#{r.place}", display, r.won ? 170 : 190, r.won ? Gold : Color.white);
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);
            var sub = Txt(root, r.won ? "WINNER WINNER  ·  KRAYVON DINNER" : (string.IsNullOrEmpty(r.killer) ? "YOU WERE TAKEN OUT" : $"ELIMINATED BY  {r.killer.ToUpperInvariant()}" + (string.IsNullOrEmpty(r.weapon) ? "" : $"  ·  {r.weapon.ToUpperInvariant()}")),
                display, 40, r.won ? Color.white : new Color(1f, 0.82f, 0.78f));
            At(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1600, 60));
            var region = Txt(root, "BILAD AL-SHAM  ·  " + (r.won ? "LAST ONE STANDING" : $"{r.place} OF {r.players}"), body, 22, Grey);
            At(region.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 66), new Vector2(1200, 30));

            // stats
            var stats = new (string label, int value, string fmt)[]
            {
                ("PLACE", r.place, "#{0}"), ("KILLS", r.kills, "{0}"), ("DAMAGE", r.damage, "{0:N0}"), ("HEADSHOTS", r.headshots, "{0}"), ("SURVIVED", Mathf.RoundToInt(r.seconds), "time"),
            };
            var vals = new Text[stats.Length];
            for (int i = 0; i < stats.Length; i++)
            {
                var c = GlassCard(root, new Vector2((i - 2) * 250, -60), new Vector2(230, 140), i == 1 && r.kills > 0 ? new Color(Gold.r, Gold.g, Gold.b, 0.9f) : OliveEdge);
                var v = Txt(c, "0", display, 64, i == 0 && r.won ? Gold : Color.white); v.rectTransform.offsetMin = new Vector2(0, 40);
                var l = Txt(c, stats[i].label, body, 18, Grey); l.rectTransform.offsetMax = new Vector2(0, -96); l.fontStyle = FontStyle.Bold;
                vals[i] = v;
            }

            // rank bar
            var rankCard = GlassCard(root, new Vector2(-130, -235), new Vector2(980, 110), OliveEdge);
            var rankName = Txt(rankCard, "", display, 40, Sand, TextAnchor.UpperLeft); rankName.rectTransform.offsetMin = new Vector2(24, 0); rankName.rectTransform.offsetMax = new Vector2(0, -12);
            var rankPts = Txt(rankCard, "", body, 20, Grey, TextAnchor.UpperRight); rankPts.rectTransform.offsetMax = new Vector2(-24, -20); rankPts.fontStyle = FontStyle.Bold;
            var track = Img(rankCard, "Track", new Color(1, 1, 1, 0.1f), sharp); At(track, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(932, 14));
            var fill = Img(track, "Fill", Gold, sharp); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero; fill.pivot = new Vector2(0, 0.5f);
            // coins
            var coinCard = GlassCard(root, new Vector2(500, -235), new Vector2(240, 110), new Color(Gold.r, Gold.g, Gold.b, 0.7f));
            var coins = Txt(coinCard, "+0", display, 52, Gold); coins.rectTransform.offsetMin = new Vector2(0, 30);
            var cl = Txt(coinCard, "COINS", body, 18, Grey); cl.rectTransform.offsetMax = new Vector2(0, -74); cl.fontStyle = FontStyle.Bold;

            // button
            var btn = Img(root, "Back", Gold, sharp); At(btn, new Vector2(0.5f, 0.5f), new Vector2(0, -390), new Vector2(520, 92));
            btn.GetComponent<Image>().raycastTarget = true;
            var be = Img(btn, "Edge", new Color(1f, 0.92f, 0.6f, 0.9f), frame); Stretch(be);
            var bt = Txt(btn, "BACK TO LOBBY", display, 46, new Color(0.07f, 0.07f, 0.07f));
            var b = btn.gameObject.AddComponent<Button>(); b.targetGraphic = btn.GetComponent<Image>();
            b.onClick.AddListener(() => SceneManager.LoadScene(0));
            btn.gameObject.SetActive(false);

            // ---- animation: fade in, title punch, numbers count up one by one, rank bar fills ----
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.5f; titleBox.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, t / 0.5f); yield return null; }
            group.alpha = 1; titleBox.localScale = Vector3.one;
            for (int i = 0; i < stats.Length; i++)
            {
                var (_, value, fmt) = stats[i];
                for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    int v = Mathf.RoundToInt(Mathf.Lerp(i == 0 ? r.players : 0, value, t / 0.35f));
                    vals[i].text = fmt == "time" ? $"{v / 60}:{v % 60:00}" : string.Format(fmt, v); yield return null;
                }
                vals[i].text = fmt == "time" ? $"{value / 60}:{value % 60:00}" : string.Format(fmt, value);
            }
            // rank: from the points before the match to the points after
            (string name, int lo, int hi) RankAt(int pts)
            {
                var ranks = KryvonLobby.Ranks; int k = 0; for (int i = 0; i < ranks.Length; i++) if (pts >= ranks[i].points) k = i;
                return (ranks[k].name, ranks[k].points, k + 1 < ranks.Length ? ranks[k + 1].points : ranks[k].points + 1000);
            }
            int gained = Mathf.Max(0, r.rankAfter - r.rankBefore);
            rankPts.text = $"+{gained} RP";
            for (float t = 0; t <= 1.2f; t += Time.unscaledDeltaTime)
            {
                int pts = Mathf.RoundToInt(Mathf.Lerp(r.rankBefore, r.rankAfter, Mathf.SmoothStep(0, 1, t / 1.2f)));
                var (n, lo, hi) = RankAt(pts); rankName.text = n;
                fill.anchorMax = new Vector2(Mathf.Clamp01((pts - lo) / (float)Mathf.Max(1, hi - lo)), 1);
                coins.text = "+" + Mathf.RoundToInt(Mathf.Lerp(0, r.coins, t / 1.2f)).ToString("N0");
                yield return null;
            }
            var (n2, lo2, hi2) = RankAt(r.rankAfter); rankName.text = n2; fill.anchorMax = new Vector2(Mathf.Clamp01((r.rankAfter - lo2) / (float)Mathf.Max(1, hi2 - lo2)), 1);
            coins.text = "+" + r.coins.ToString("N0");
            if (RankAt(r.rankBefore).name != n2) { rankPts.text = $"PROMOTED!  +{gained} RP"; rankPts.color = Gold; }
            btn.gameObject.SetActive(true);
            for (float t = 0; ; t += Time.unscaledDeltaTime) { if (!btn) yield break; btn.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(t * 4f)); yield return null; }
        }
    }
}

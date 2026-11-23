// Squad route planning on the full map (folded in from the TeamRotation idea, on top of KryvonMinimap – no second camera).
// Pick a marker (LAND · MOVE · HOLD · DANGER), tap the island to drop it; up to 8 markers, numbered in order.
// The route (dotted) shows on the full map and on the round minimap, with a beam of light over the next stop in the world.
// Walking within 25 m of a stop ticks it off; CLEAR removes the plan.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kryvon
{
    public class KryvonRoutePlan
    {
        public enum Mark { Land, Move, Hold, Danger }
        static readonly string[] Names = { "LAND", "MOVE", "HOLD", "DANGER" };
        static readonly Color[] Colors = { new(0.83f, 0.69f, 0.22f), new(0.92f, 0.95f, 1f), new(0.35f, 0.85f, 0.45f), new(0.9f, 0.22f, 0.18f) };
        const int MaxMarks = 8; const float ReachRadius = 25f;

        class Stop { public Vector2 at; public Mark kind; public bool reached; public RectTransform full, mini; public Text num; }
        readonly List<Stop> stops = new();
        readonly List<RectTransform> fullDots = new(), miniDots = new();
        readonly RectTransform map, mini; readonly Sprite dot, disc; readonly Font font;
        readonly float hz; Mark current = Mark.Move; readonly List<Image> chips = new();
        LineRenderer beam;

        public KryvonRoutePlan(RectTransform fullScreen, RectTransform fullMap, RectTransform miniView, float halfZ, Sprite dotSprite, Sprite discSprite)
        {
            map = fullMap; mini = miniView; hz = halfZ; dot = dotSprite; disc = discSprite;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // the map itself takes the taps (instead of closing): place a marker where you tap
            var catcher = map.gameObject.AddComponent<Catcher>(); catcher.plan = this;
            map.GetComponent<RawImage>().raycastTarget = true;

            // marker picker + CLEAR, bottom-left of the full map
            for (int i = 0; i <= 4; i++)
            {
                int idx = i; bool clear = i == 4;
                var b = new GameObject(clear ? "Clear" : "Mark_" + Names[i], typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
                b.SetParent(fullScreen, false); b.anchorMin = b.anchorMax = b.pivot = Vector2.zero; b.anchoredPosition = new Vector2(30 + i * 150, 30); b.sizeDelta = new Vector2(140, 64);
                var im = b.GetComponent<Image>(); im.color = clear ? new Color(0.25f, 0.27f, 0.3f, 0.95f) : new Color(0.08f, 0.1f, 0.12f, 0.95f);
                var t = Label(b, clear ? "CLEAR" : Names[i], 24, clear ? Color.white : Colors[i]); t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                if (clear) b.GetComponent<Button>().onClick.AddListener(Clear);
                else { chips.Add(im); b.GetComponent<Button>().onClick.AddListener(() => { current = (Mark)idx; Paint(); }); }
            }
            var hint = Label(fullScreen, "ROUTE PLAN  ·  pick a marker, tap the map", 20, new Color(1, 1, 1, 0.75f));
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = Vector2.zero; hint.rectTransform.anchoredPosition = new Vector2(30, 104); hint.rectTransform.sizeDelta = new Vector2(600, 30);
            hint.alignment = TextAnchor.MiddleLeft;
            Paint();
        }

        void Paint() { for (int i = 0; i < chips.Count; i++) chips[i].color = i == (int)current ? new Color(Colors[i].r * 0.45f, Colors[i].g * 0.45f, Colors[i].b * 0.45f, 0.95f) : new Color(0.08f, 0.1f, 0.12f, 0.95f); }

        class Catcher : MonoBehaviour, IPointerClickHandler
        {
            public KryvonRoutePlan plan;
            public void OnPointerClick(PointerEventData e)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local)) plan.Place(local);
            }
        }

        float K => map.rect.size.y / (2 * hz);           // UI units per metre on the full map
        void Place(Vector2 local)
        {
            local -= map.rect.center;                     // the map is centred on the world origin
            var w = local / K;
            // tapping an existing marker removes it
            for (int i = 0; i < stops.Count; i++) if ((stops[i].at - w).magnitude * K < 22f) { Remove(i); return; }
            if (stops.Count >= MaxMarks) { KryvonKillFeed.Notice("ROUTE FULL  ·  TAP A MARKER TO REMOVE IT"); return; }
            if (current == Mark.Land) for (int i = stops.Count - 1; i >= 0; i--) if (stops[i].kind == Mark.Land) Remove(i);   // one landing spot
            var s = new Stop { at = w, kind = current };
            s.full = Pin(map, 30f); s.mini = Pin(mini, 16f);
            s.full.GetComponent<Image>().color = Colors[(int)current]; s.mini.GetComponent<Image>().color = Colors[(int)current];
            s.num = Label(s.full, "", 17, new Color(0.06f, 0.06f, 0.06f)); s.num.rectTransform.anchorMin = Vector2.zero; s.num.rectTransform.anchorMax = Vector2.one; s.num.rectTransform.offsetMin = s.num.rectTransform.offsetMax = Vector2.zero;
            if (current == Mark.Land) stops.Insert(0, s); else stops.Add(s);
            Renumber();
        }
        void Remove(int i) { var s = stops[i]; if (s.full) Object.Destroy(s.full.gameObject); if (s.mini) Object.Destroy(s.mini.gameObject); stops.RemoveAt(i); Renumber(); }
        void Clear() { for (int i = stops.Count - 1; i >= 0; i--) Remove(i); }
        void Renumber() { int n = 1; foreach (var s in stops) s.num.text = s.kind == Mark.Danger ? "!" : (n++).ToString(); }

        RectTransform Pin(RectTransform parent, float size)
        {
            var g = new GameObject("RoutePin", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
            var im = g.GetComponent<Image>(); im.sprite = disc; im.raycastTarget = false;
            var r = (RectTransform)g.transform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = Vector2.one * size; return r;
        }
        Text Label(Transform parent, string s, int size, Color c)
        {
            var g = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow)); g.transform.SetParent(parent, false);
            var t = g.GetComponent<Text>(); t.font = font; t.fontSize = size; t.text = s; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; return t;
        }
        RectTransform Dot(List<RectTransform> pool, int i, RectTransform parent, float size)
        {
            while (pool.Count <= i) { var d = Pin(parent, size); d.GetComponent<Image>().sprite = dot; pool.Add(d); }
            return pool[i];
        }

        /// me = player (or jet) position; k = minimap UI units per metre; edge = minimap radius in UI units
        public void Update(Vector3 me, float k, float edge, bool fullOpen)
        {
            var me2 = new Vector2(me.x, me.z);
            // tick off stops you have reached (in order; danger zones are never "reached")
            foreach (var s in stops) if (!s.reached && s.kind != Mark.Danger && (s.at - me2).magnitude < ReachRadius) { s.reached = true; KryvonKillFeed.Notice("ROUTE  ·  " + Names[(int)s.kind] + " " + s.num.text + " REACHED"); }
            Stop next = null; foreach (var s in stops) if (!s.reached && s.kind != Mark.Danger) { next = s; break; }

            float kf = K; int fd = 0, md = 0;
            Vector2 prev = me2; bool first = true;
            foreach (var s in stops)
            {
                float a = s.reached ? 0.35f : 1f; var c = Colors[(int)s.kind];
                s.full.anchoredPosition = s.at * kf; s.full.GetComponent<Image>().color = new Color(c.r, c.g, c.b, a);
                var mp = (s.at - me2) * k; if (mp.magnitude > edge) mp = mp.normalized * edge;   // off the minimap: stuck to the rim
                s.mini.anchoredPosition = mp; s.mini.GetComponent<Image>().color = new Color(c.r, c.g, c.b, s.reached ? 0.3f : 1f);
                s.mini.sizeDelta = Vector2.one * (s == next ? 16f + 4f * Mathf.Sin(Time.time * 6f) : 14f);
                if (s.kind == Mark.Danger || s.reached) continue;
                // dotted leg: player → first open stop → … (full map); only the first leg on the minimap
                float len = (s.at - prev).magnitude; int n = Mathf.Clamp(Mathf.RoundToInt(len * kf / 16f), 1, 60);
                if (fullOpen) for (int i = 1; i < n; i++) { var d = Dot(fullDots, fd++, map, 6f); d.gameObject.SetActive(true); d.anchoredPosition = Vector2.Lerp(prev, s.at, i / (float)n) * kf; d.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.85f); }
                if (first)
                {
                    int mn = Mathf.Clamp(Mathf.RoundToInt(len * k / 12f), 1, 24);
                    for (int i = 1; i < mn; i++) { var p = (Vector2.Lerp(prev, s.at, i / (float)mn) - me2) * k; if (p.magnitude > edge) break; var d = Dot(miniDots, md++, mini, 4f); d.gameObject.SetActive(true); d.anchoredPosition = p; d.GetComponent<Image>().color = new Color(c.r, c.g, c.b, 0.8f); }
                    first = false;
                }
                prev = s.at;
            }
            for (int i = fd; i < fullDots.Count; i++) fullDots[i].gameObject.SetActive(false);
            for (int i = md; i < miniDots.Count; i++) miniDots[i].gameObject.SetActive(false);

            // a column of light over the next stop, so you can find it without the map
            if (next != null)
            {
                if (!beam)
                {
                    var g = new GameObject("RouteBeam"); beam = g.AddComponent<LineRenderer>(); beam.positionCount = 2; beam.widthMultiplier = 1.2f;
                    beam.material = new Material(Shader.Find("Sprites/Default")); beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; beam.receiveShadows = false;
                }
                var c = Colors[(int)next.kind]; float y = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(new Vector3(next.at.x, 0, next.at.y)) + Terrain.activeTerrain.transform.position.y : me.y;
                beam.enabled = true; beam.SetPosition(0, new Vector3(next.at.x, y, next.at.y)); beam.SetPosition(1, new Vector3(next.at.x, y + 120f, next.at.y));
                beam.startColor = new Color(c.r, c.g, c.b, 0.55f); beam.endColor = new Color(c.r, c.g, c.b, 0f);
            }
            else if (beam) beam.enabled = false;
        }
    }
}

// Kill feed (top right), a big banner for your own kills, and a hit marker on the crosshair.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public class KryvonKillFeed : MonoBehaviour
    {
        static KryvonKillFeed inst;
        readonly List<(string text, float t, bool mine)> lines = new();
        float banner = -9, hitT = -9; bool hitHead; int kills;
        GUIStyle line, big, mark;

        static KryvonKillFeed I => inst ? inst : inst = new GameObject("KillFeed").AddComponent<KryvonKillFeed>();
        public static void Push(string text, bool mine)
        {
            var k = I; k.lines.Insert(0, (text, Time.time, mine)); if (k.lines.Count > 5) k.lines.RemoveAt(5);
            if (mine) { k.banner = Time.time; k.kills++; }
        }
        public static void Hit(bool head) { var k = I; k.hitT = Time.time; k.hitHead = head; }
        string notice; float noticeT = -9; GUIStyle noticeStyle;
        /// a short warning under the crosshair (crimson): HELMET DESTROYED, VEST DESTROYED …
        public static void Notice(string text) { var k = I; k.notice = text; k.noticeT = Time.time; }

        void Awake() => inst = this;
        WeaponController wc; Texture2D px; float gapNow = 10f, hitScale;
        void Start()
        {
            wc = FindAnyObjectByType<WeaponController>();
            if (wc) wc.OnHit += (_, _, head) => Hit(head);
            px = Texture2D.whiteTexture;
        }

        /// dynamic crosshair: four ticks whose gap is the weapon's real spread (opens when moving / firing, closes when aiming)
        void Crosshair()
        {
            if (!wc || !wc.enabled || wc.IsMelee || wc.Current == null || KryvonDrop.Active) return;
            var cam = Camera.main; if (!cam || wc.IsAds && cam.fieldOfView < 40f) return;          // a scope draws its own reticle
            float spread = Mathf.Max(0.05f, wc.CurrentSpread);
            float px1 = Mathf.Tan(spread * Mathf.Deg2Rad) * Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float target = Mathf.Clamp(px1, 4f, Screen.height * 0.12f);
            gapNow = Mathf.Lerp(gapNow, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            float cx = Screen.width / 2f, cy = Screen.height / 2f, len = Mathf.Max(8f, Screen.height * 0.012f), th = Mathf.Max(2f, Screen.height * 0.0024f);
            var col = new Color(1, 1, 1, 0.9f);
            void R(float x, float y, float w, float h) { GUI.color = new Color(0, 0, 0, 0.45f); GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), px); GUI.color = col; GUI.DrawTexture(new Rect(x, y, w, h), px); }
            R(cx - th / 2, cy - gapNow - len, th, len); R(cx - th / 2, cy + gapNow, th, len);
            R(cx - gapNow - len, cy - th / 2, len, th); R(cx + gapNow, cy - th / 2, len, th);
            GUI.color = Color.white;
        }
        /// hit marker: four short diagonal strokes, white on the body, crimson #C0392B on the head; it pops and fades
        void HitMark()
        {
            float age = Time.time - hitT; if (age > 0.22f) return;
            float k = 1f - age / 0.22f, cx = Screen.width / 2f, cy = Screen.height / 2f;
            float r0 = Screen.height * 0.012f * (1.25f - 0.25f * k), len = Screen.height * 0.014f, th = Mathf.Max(2f, Screen.height * 0.003f);
            var c = hitHead ? new Color(0.753f, 0.224f, 0.169f, k) : new Color(1, 1, 1, k);
            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + 90f * i; var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy));
                GUI.color = new Color(0, 0, 0, 0.5f * k); GUI.DrawTexture(new Rect(cx + r0 - 1, cy - th / 2 - 1, len + 2, th + 2), px);
                GUI.color = c; GUI.DrawTexture(new Rect(cx + r0, cy - th / 2, len, th), px);
                GUI.matrix = m;
            }
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (KryvonTrailer.HideHud) return;
            line ??= new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.82f, 0.3f) } };
            mark ??= new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            float y = 70;
            foreach (var (text, t, mine) in lines)
            {
                float age = Time.time - t; if (age > 7) continue;
                GUI.color = new Color(1, mine ? 0.85f : 1, mine ? 0.5f : 1, Mathf.Clamp01(7 - age));
                GUI.Box(new Rect(Screen.width - 380, y, 360, 28), text, line); y += 32;
            }
            GUI.color = Color.white;
            if (Time.time - banner < 2.2f)
            {
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(2.2f - (Time.time - banner)));
                GUI.Label(new Rect(0, Screen.height * 0.28f, Screen.width, 50), "ELIMINATED  ✦  " + kills, big);
                GUI.color = Color.white;
            }
            Crosshair();
            HitMark();
            if (Time.time - noticeT < 1.8f && !string.IsNullOrEmpty(notice))
            {
                noticeStyle ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 34), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                float a = Mathf.Clamp01(1.8f - (Time.time - noticeT)) * Mathf.Clamp01((Time.time - noticeT) * 8f);
                noticeStyle.normal.textColor = new Color(0.753f, 0.224f, 0.169f, a);
                GUI.color = new Color(0, 0, 0, 0.55f * a); GUI.Label(new Rect(2, Screen.height * 0.62f + 2, Screen.width, 40), notice, noticeStyle);
                GUI.color = Color.white; GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 40), notice, noticeStyle);
            }
        }
    }
}

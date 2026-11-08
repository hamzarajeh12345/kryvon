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

        void Awake() => inst = this;
        void Start()
        {
            var w = FindAnyObjectByType<WeaponController>();
            if (w) w.OnHit += (_, _, head) => Hit(head);
        }

        void OnGUI()
        {
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
            if (Time.time - hitT < 0.18f)
            {
                mark.normal.textColor = hitHead ? new Color(1f, 0.3f, 0.25f) : Color.white;
                GUI.Label(new Rect(Screen.width / 2f - 30, Screen.height / 2f - 30, 60, 60), "✕", mark);
            }
        }
    }
}

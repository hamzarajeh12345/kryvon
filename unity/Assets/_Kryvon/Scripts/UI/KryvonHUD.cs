// In-match HUD: health and boost bars, armour, consumable slots (4–9) with use progress,
// weapon + ammo (calibre colour), zone timer, players alive. Drawn with IMGUI so it needs no prefabs.
using UnityEngine;

namespace Kryvon
{
    public class KryvonHUD : MonoBehaviour
    {
        [SerializeField] Font font;
        Health hp; KryvonGear gear; WeaponController weapons; SafeZone zone;
        GUIStyle s14, s18, s26, s34; Texture2D tex;
        static readonly Color Gold = new(1f, 0.78f, 0.32f), Panel = new(0, 0, 0, 0.55f);

        void Start()
        {
            hp = GetComponent<Health>(); gear = GetComponent<KryvonGear>(); weapons = GetComponent<WeaponController>();
            zone = FindAnyObjectByType<SafeZone>();
            var dbg = FindAnyObjectByType<KryvonDebugHUD>(); if (dbg) dbg.enabled = false;   // replaced by this HUD
        }
        GUIStyle S(int size, TextAnchor a = TextAnchor.MiddleLeft, bool bold = true) => new(GUI.skin.label) { font = font, fontSize = size, alignment = a, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, normal = { textColor = Color.white } };
        void Box(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, tex); GUI.color = Color.white; }

        void OnGUI()
        {
            if (hp == null) return;
            tex ??= Texture2D.whiteTexture;
            s14 ??= S(14); s18 ??= S(18); s26 ??= S(26, TextAnchor.MiddleCenter); s34 ??= S(34, TextAnchor.MiddleRight);
            float W = Screen.width, H = Screen.height, cx = W / 2;

            // ---- health ----
            var hr = new Rect(cx - 220, H - 62, 440, 14);
            Box(new Rect(hr.x - 3, hr.y - 3, hr.width + 6, hr.height + 6), Panel);
            float f = Mathf.Clamp01(hp.Current / 100f);
            Box(new Rect(hr.x, hr.y, hr.width * f, hr.height), f < 0.25f ? new Color(1f, 0.25f, 0.2f) : f < 0.75f ? new Color(1f, 0.92f, 0.85f) : Color.white);
            Box(new Rect(hr.x + hr.width * 0.75f, hr.y, 2, hr.height), new Color(0, 0, 0, 0.5f));          // the 75 % mark (bandage/first-aid cap)
            // ---- boost: four segments ----
            if (gear)
                for (int i = 0; i < 4; i++)
                {
                    var br = new Rect(cx - 220 + i * 111, H - 80, 107, 6);
                    Box(br, new Color(1, 1, 1, 0.15f));
                    float seg = Mathf.Clamp01((gear.Boost - i * 25f) / 25f);
                    Box(new Rect(br.x, br.y, br.width * seg, br.height), new Color(1f, 0.62f, 0.15f));
                }
            // ---- armour ----
            GUI.Label(new Rect(cx - 220, H - 42, 200, 24), $"VEST {(hp.vestReduction > 0 ? "Lv." + Mathf.RoundToInt(hp.vestReduction / 0.15f - 1) : "—")}   HELMET {(hp.helmetReduction > 0 ? "Lv." + Mathf.RoundToInt(hp.helmetReduction / 0.15f - 1) : "—")}", s14);

            // ---- consumables 4..9 ----
            if (gear)
            {
                for (int i = 0; i < 6; i++)
                {
                    var r = new Rect(W - 470 + i * 76, H - 92, 70, 70);
                    bool active = gear.Using == (Consumable)i;
                    Box(r, active ? new Color(1f, 0.62f, 0.15f, 0.5f) : Panel);
                    GUI.color = gear.counts[i] > 0 ? Color.white : new Color(1, 1, 1, 0.3f);
                    GUI.Label(new Rect(r.x + 6, r.y + 2, 30, 20), (i + 4).ToString(), s14);
                    GUI.Label(new Rect(r.x, r.y + 20, r.width, 30), gear.counts[i].ToString(), s26);
                    var small = new GUIStyle(s14) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
                    GUI.Label(new Rect(r.x, r.y + 50, r.width, 16), KryvonGear.Names[i].ToUpper(), small);
                    GUI.color = Color.white;
                }
                if (gear.Using.HasValue)
                {
                    var pr = new Rect(cx - 140, H * 0.62f, 280, 8);
                    Box(new Rect(pr.x - 2, pr.y - 2, pr.width + 4, pr.height + 4), Panel); Box(new Rect(pr.x, pr.y, pr.width * gear.UseProgress, pr.height), Gold);
                    GUI.Label(new Rect(pr.x, pr.y - 30, pr.width, 26), "USING " + KryvonGear.Names[(int)gear.Using.Value].ToUpper(), s26);
                }
                if (gear.flares > 0) GUI.Label(new Rect(W - 470, H - 122, 400, 24), $"✸ FLARE GUN x{gear.flares}  —  press G", s14);
            }

            // ---- weapon + ammo ----
            if (weapons && weapons.Current)
            {
                var slot = weapons.CurrentSlot; var d = weapons.Current;
                GUI.color = KryvonLoot.AmmoColor(d.ammoType);
                GUI.Label(new Rect(W - 470, H - 170, 450, 40), $"{slot.ammo} / {weapons.Reserve(d.ammoType)}", s34);
                GUI.color = Color.white;
                GUI.Label(new Rect(W - 470, H - 135, 450, 22), $"{d.displayName.ToUpper()}   ·   {KryvonLoot.AmmoName(d.ammoType)}", new GUIStyle(s14) { alignment = TextAnchor.MiddleRight });
            }

            // ---- zone + alive ----
            if (zone)
            {
                string t = Mathf.FloorToInt(zone.Timer / 60) + ":" + Mathf.FloorToInt(zone.Timer % 60).ToString("00");
                string msg = zone.Landing ? (zone.CircleShown ? $"ZONE APPEARS — FIRST SHRINK IN {t}" : $"LANDING  {t}") : zone.Shrinking ? $"AL-TAWQ CLOSING  {t}" : $"NEXT SHRINK IN {t}";
                var zr = new Rect(cx - 220, 14, 440, 34);
                Box(zr, Panel); GUI.color = zone.Shrinking ? new Color(0.5f, 0.75f, 1f) : Color.white;
                GUI.Label(zr, msg + $"   ·   PHASE {zone.PhaseIndex + 1}", s26); GUI.color = Color.white;
            }
            int alive = 0; foreach (var h in FindObjectsByType<Health>()) if (h.IsAlive) alive++;
            Box(new Rect(W - 150, 14, 130, 54), Panel);
            GUI.Label(new Rect(W - 150, 14, 130, 36), alive.ToString(), s26);
            GUI.Label(new Rect(W - 150, 46, 130, 18), "ALIVE", new GUIStyle(s14) { alignment = TextAnchor.MiddleCenter });
        }
    }
}

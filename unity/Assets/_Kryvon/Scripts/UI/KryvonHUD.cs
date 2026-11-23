// In-match HUD: health and boost bars, armour, consumable slots (4–9) with use progress,
// weapon + ammo (calibre colour), zone timer, players alive. Drawn with IMGUI so it needs no prefabs.
using UnityEngine;

namespace Kryvon
{
    public class KryvonHUD : MonoBehaviour
    {
        [SerializeField] Font font;
        Health hp; KryvonGear gear; WeaponController weapons; SafeZone zone; KryvonThrowables thr; KryvonStealth st;
        GUIStyle s14, s18, s26, s34; Texture2D tex;
        static readonly Color Gold = new(1f, 0.78f, 0.32f), Panel = new(0, 0, 0, 0.55f);

        void Start()
        {
            hp = GetComponent<Health>(); gear = GetComponent<KryvonGear>(); weapons = GetComponent<WeaponController>();
            zone = FindAnyObjectByType<SafeZone>(); thr = GetComponent<KryvonThrowables>(); st = GetComponent<KryvonStealth>();
            var dbg = FindAnyObjectByType<KryvonDebugHUD>(); if (dbg) dbg.enabled = false;   // replaced by this HUD
        }
        GUIStyle S(int size, TextAnchor a = TextAnchor.MiddleLeft, bool bold = true) => new(GUI.skin.label) { font = font, fontSize = size, alignment = a, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, normal = { textColor = Color.white } };
        void Box(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, tex); GUI.color = Color.white; }

        void OnGUI()
        {
            if (KryvonTrailer.HideHud) return;
            KryvonDebugHUD.DrawAim(weapons);
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
                int shown = 0;
                for (int i = 5; i >= 0; i--)
                {
                    if (gear.counts[i] <= 0 && gear.Using != (Consumable)i) continue;
                    var r = new Rect(W - 96 - shown * 76, H - 92, 70, 70); shown++;
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
            if (weapons && weapons.IsMelee)
            {
                GUI.Label(new Rect(W - 470, H - 170, 450, 40), weapons.HeldName.ToUpper(), s34);
                GUI.Label(new Rect(W - 470, H - 135, 450, 22), weapons.Current ? "MELEE   ·   CLICK TO SWING" : "NO WEAPON   ·   FIND ONE ON THE MAP", new GUIStyle(s14) { alignment = TextAnchor.MiddleRight });
            }
            else if (weapons && weapons.Current)
            {
                var slot = weapons.CurrentSlot; var d = weapons.Current;
                GUI.color = KryvonLoot.AmmoColor(d.ammoType);
                GUI.Label(new Rect(W - 470, H - 170, 450, 40), $"{slot.ammo} / {weapons.Reserve(d.ammoType)}", s34);
                GUI.color = Color.white;
                GUI.Label(new Rect(W - 470, H - 135, 450, 22), $"{d.displayName.ToUpper()}   ·   {KryvonLoot.AmmoName(d.ammoType)}", new GUIStyle(s14) { alignment = TextAnchor.MiddleRight });
            }

            // ---- throwables + deployables ----
            if (thr)
            {
                var tr = new Rect(W - 470, H - 210, 450, 30);
                bool touch = KryvonTouchControls.Wanted;     // on phones the buttons show all of this – no keyboard hints over them
                if (!touch && thr.Count(thr.Selected) > 0) GUI.Label(tr, $"[G]  {KryvonThrowables.Names[(int)thr.Selected].ToUpper()} ×{thr.Count(thr.Selected)}", new GUIStyle(s14) { alignment = TextAnchor.MiddleRight });
                string dep = (thr.spikes > 0 ? $"[Y] SPIKES ×{thr.spikes}   " : "") + (thr.shields > 0 ? $"[U] SHIELD ×{thr.shields}" : "");
                if (!touch && dep.Length > 0) GUI.Label(new Rect(W - 470, H - 232, 450, 22), dep, new GUIStyle(s14) { alignment = TextAnchor.MiddleRight, fontSize = 12 });
                if (thr.Cook >= 0 && thr.Selected == Throwable.Frag)
                {
                    float left = Mathf.Max(0, 5f - thr.Cook);
                    GUI.color = left < 1.5f ? new Color(1f, 0.3f, 0.25f) : Gold; GUI.Label(new Rect(cx - 100, H * 0.56f, 200, 30), $"COOKING  {left:0.0}s", s26); GUI.color = Color.white;
                }
            }
            // ---- stealth ----
            if (st && (st.HasGhillie || st.MudLeft > 0 || st.CloakLeft > 0 || st.Indoors || st.cloakCharges > 0 || st.RollProgress > 0 || st.Visibility < 0.9f))
            {
                var vr = new Rect(cx + 240, H - 66, 150, 22);
                GUI.Label(vr, $"SEEN {Mathf.RoundToInt(st.Visibility * 100)}%", s14);
                Box(new Rect(vr.x, vr.yMax, 140, 4), new Color(1, 1, 1, 0.15f)); Box(new Rect(vr.x, vr.yMax, 140 * st.Visibility, 4), st.Visibility < 0.35f ? new Color(0.4f, 0.9f, 0.5f) : Color.white);
                string tags = (st.HasGhillie ? "GHILLIE  " : "") + (st.MudLeft > 0 ? $"MUD {st.MudLeft:0}s  " : "") + (st.Indoors ? "IN SHADOW  " : "") + (st.CloakLeft > 0 ? $"CLOAK {st.CloakLeft:0.0}s" : st.cloakCharges > 0 ? (st.CloakCooldown > 0 ? $"CLOAK {st.CloakCooldown:0}s" : $"CLOAK x{st.cloakCharges} (X)") : "");
                GUI.Label(new Rect(cx + 240, H - 40, 400, 20), tags, new GUIStyle(s14) { fontSize = 12 });
                if (st.RollProgress > 0) { var pr = new Rect(cx - 140, H * 0.62f, 280, 8); Box(pr, Panel); Box(new Rect(pr.x, pr.y, pr.width * st.RollProgress, pr.height), new Color(0.6f, 0.45f, 0.3f)); GUI.Label(new Rect(pr.x, pr.y - 30, pr.width, 26), "MUD CAMOUFLAGE", s26); }
            }

            // ---- zone + alive ----
            if (zone && (zone.Landing || zone.Running))
            {
                string t = Mathf.FloorToInt(zone.Timer / 60) + ":" + Mathf.FloorToInt(zone.Timer % 60).ToString("00");
                string msg = zone.Landing ? (zone.CircleShown ? $"ZONE SHRINKS IN  {t}" : $"LANDING  {t}") : zone.Shrinking ? $"ZONE CLOSING  {t}" : $"ZONE SHRINKS IN  {t}";
                var zr = new Rect(cx - 220, 14, 440, 34);
                Box(zr, Panel); GUI.color = zone.Shrinking ? Color.Lerp(KryvonGraphics.ZoneColor, Color.white, 0.35f) : Color.white;
                GUI.Label(zr, zone.Landing ? msg : msg + $"   ·   PHASE {zone.PhaseIndex + 1}", s26); GUI.color = Color.white;
            }
            int alive = 0; foreach (var h in FindObjectsByType<Health>()) if (h.IsAlive) alive++;
            Box(new Rect(W - 150, 14, 130, 54), Panel);
            GUI.Label(new Rect(W - 150, 14, 130, 36), alive.ToString(), s26);
            GUI.Label(new Rect(W - 150, 46, 130, 18), "ALIVE", new GUIStyle(s14) { alignment = TextAnchor.MiddleCenter });
        }
    }
}

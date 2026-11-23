// Floating labels over ground loot, only for loot the player can actually SEE (nothing through walls or floors):
// the item under the crosshair (up to 12 m) gets a full tag with its rarity colour; other visible items within 4 m a small name.
using UnityEngine;

namespace Kryvon
{
    public class KryvonLootLabels : MonoBehaviour
    {
        [SerializeField] float lookRange = 12f, nearRange = 4f;
        KryvonLoot aimed; readonly System.Collections.Generic.List<KryvonLoot> near = new(); Transform player; float nextScan;
        // a clear line from the camera to the item (the item itself and the player's own body don't count as blockers)
        bool Visible(Camera cam, KryvonLoot l)
        {
            var from = cam.transform.position; var to = l.transform.position + Vector3.up * 0.15f; var d = to - from; float len = d.magnitude;
            if (len < 0.2f) return true;
            foreach (var h in Physics.RaycastAll(from, d / len, len - 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<KryvonLoot>() == l) continue;
                if (player && h.collider.transform.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }
        void Update()
        {
            if (Time.unscaledTime < nextScan) return; nextScan = Time.unscaledTime + 0.1f;
            var cam = Camera.main; aimed = null; near.Clear(); if (!cam) return;
            if (!player) { var pc = FindAnyObjectByType<PlayerController>(); if (pc) player = pc.transform; }
            float best = 0.06f; var ray = cam.transform.forward; KryvonLoot cand = null;
            foreach (var l in KryvonLoot.All)
            {
                if (!l) continue;
                var d = l.transform.position - cam.transform.position; float dist = d.magnitude;
                if (dist > lookRange) continue;
                float off = 1f - Vector3.Dot(d / dist, ray);
                if (off < best) { best = off; cand = l; }
                float pd = player ? Vector3.Distance(l.transform.position, player.position) : dist;
                if (pd < nearRange && Visible(cam, l)) near.Add(l);
            }
            if (cand && Visible(cam, cand)) aimed = cand;
            Glows(cam);
        }

        // ---------- ground glow: a soft ring in the rarity colour under nearby loot, a faint light beam over epic+ ----------
        // Pooled (20 rings, 6 beams) and placed on the closest items, so thousands of items on the map cost nothing.
        readonly System.Collections.Generic.List<Transform> rings = new(), beams = new();
        readonly System.Collections.Generic.List<(KryvonLoot l, float d)> sorted = new();
        static MaterialPropertyBlock glowMpb;
        Transform Marker(bool beam)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(g.GetComponent<Collider>()); g.name = beam ? "LootBeam" : "LootRing";
            var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = beam ? KryvonFX.BeamMat : KryvonFX.GlowMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            g.SetActive(false); return g.transform;
        }
        static Color Rar(KryvonLoot l) => l.kind == LootKind.Ammo ? KryvonLoot.AmmoColor(l.ammoType) : KryvonLoot.RarityColors[Mathf.Clamp(l.rarity, 0, 4)];
        void Glows(Camera cam)
        {
            if (!player) return;
            glowMpb ??= new MaterialPropertyBlock();
            sorted.Clear();
            foreach (var l in KryvonLoot.All) { if (!l) continue; float d = (l.transform.position - player.position).sqrMagnitude; if (d < 70f * 70f) sorted.Add((l, d)); }
            sorted.Sort((a, b) => a.d.CompareTo(b.d));
            int ri = 0, bi = 0; float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.5f);
            foreach (var (l, d) in sorted)
            {
                var c = Rar(l);
                if (ri < 20 && d < 18f * 18f)
                {
                    if (ri >= rings.Count) rings.Add(Marker(false));
                    var t = rings[ri++]; t.gameObject.SetActive(true);
                    t.SetPositionAndRotation(l.transform.position + Vector3.up * 0.03f, Quaternion.Euler(90, 0, 0)); t.localScale = Vector3.one * (l.rarity >= 3 ? 1.3f : 0.95f);
                    glowMpb.SetColor("_BaseColor", new Color(c.r, c.g, c.b, (l.rarity >= 2 ? 0.55f : 0.32f) * pulse)); t.GetComponent<MeshRenderer>().SetPropertyBlock(glowMpb);
                }
                if (bi < 6 && l.rarity >= 3 && l.kind != LootKind.Ammo)
                {
                    if (bi >= beams.Count) beams.Add(Marker(true));
                    var t = beams[bi++]; t.gameObject.SetActive(true);
                    var face = cam.transform.position - l.transform.position; face.y = 0;
                    t.SetPositionAndRotation(l.transform.position + Vector3.up * 2.2f, Quaternion.LookRotation(-face.normalized)); t.localScale = new Vector3(0.35f, 4.5f, 1f);
                    float far = Mathf.Clamp01(Mathf.Sqrt(d) / 25f);                  // fainter up close, readable from afar
                    glowMpb.SetColor("_BaseColor", new Color(c.r, c.g, c.b, Mathf.Lerp(0.12f, 0.4f, far))); t.GetComponent<MeshRenderer>().SetPropertyBlock(glowMpb);
                }
            }
            for (int i = ri; i < rings.Count; i++) rings[i].gameObject.SetActive(false);
            for (int i = bi; i < beams.Count; i++) beams[i].gameObject.SetActive(false);
        }
        GUIStyle big, small, tag; Texture2D white;
        static string Icon(LootKind k) => k switch
        {
            LootKind.Weapon => "▲", LootKind.Ammo => "■", LootKind.Medkit or LootKind.Bandage or LootKind.Syringe or LootKind.Drink or LootKind.FirstAid or LootKind.Painkiller => "✚",
            LootKind.Vest or LootKind.Helmet => "◆", LootKind.Backpack => "▣", LootKind.Grenade => "●", LootKind.Scope => "◎", LootKind.FlareGun => "✸", LootKind.Molotov or LootKind.SmokeGrenade or LootKind.Flashbang => "●", LootKind.SpikeStrip or LootKind.Shield => "▬", LootKind.Ghillie or LootKind.CloakDevice => "◐", LootKind.NightVision => "◉", _ => "•",
        };

        void OnGUI()
        {
            if (KryvonTrailer.HideHud) return;
            var cam = Camera.main; if (!cam) return;
            white ??= Texture2D.whiteTexture;
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            tag ??= new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, 1, 1, 0.75f) } };

            if (aimed && !near.Contains(aimed)) near.Add(aimed);
            foreach (var l in near)
            {
                if (!l) continue;
                var p = l.transform.position + Vector3.up * 0.35f;
                var sp = cam.WorldToScreenPoint(p); if (sp.z <= 0) continue;
                float dist = Vector3.Distance(cam.transform.position, p);
                var c = l.kind == LootKind.Ammo ? KryvonLoot.AmmoColor(l.ammoType) : KryvonLoot.RarityColors[Mathf.Clamp(l.rarity, 0, 4)];
                var pos = new Vector2(sp.x, Screen.height - sp.y);
                if (l == aimed)
                {
                    var name = Icon(l.kind) + "  " + l.Describe();
                    float w = Mathf.Max(170, big.CalcSize(new GUIContent(name)).x + 30);
                    var r = new Rect(pos.x - w / 2, pos.y - 58, w, 50);
                    GUI.color = new Color(0, 0, 0, 0.72f); GUI.DrawTexture(r, white);
                    GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, 4, r.height), white);
                    GUI.DrawTexture(new Rect(pos.x - 1, r.yMax, 2, 10), white);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(r.x + 14, r.y + 2, w - 14, 28), name, big);
                    tag.normal.textColor = c; GUI.Label(new Rect(r.x + 14, r.y + 28, w - 14, 18), KryvonLoot.RarityNames[Mathf.Clamp(l.rarity, 0, 4)] + $"  ·  {dist:0} m", tag);
                }
                else
                {
                    GUI.color = c; GUI.Label(new Rect(pos.x - 90, pos.y - 22, 180, 20), Icon(l.kind) + " " + l.Describe(), small); GUI.color = Color.white;
                }
            }
        }
    }
}

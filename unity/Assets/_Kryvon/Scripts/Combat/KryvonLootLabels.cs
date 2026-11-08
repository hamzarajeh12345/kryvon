// Floating labels over ground loot: the item you look at (up to 12 m) gets a full tag with its rarity colour,
// other items within 6 m get a small name. Lets you tell weapons, meds, armour and bags apart at a glance.
using UnityEngine;

namespace Kryvon
{
    public class KryvonLootLabels : MonoBehaviour
    {
        [SerializeField] float lookRange = 12f, nearRange = 6f;
        GUIStyle big, small, tag; Texture2D white;
        static string Icon(LootKind k) => k switch
        {
            LootKind.Weapon => "▲", LootKind.Ammo => "■", LootKind.Medkit or LootKind.Bandage or LootKind.Syringe or LootKind.Drink or LootKind.FirstAid or LootKind.Painkiller => "✚",
            LootKind.Vest or LootKind.Helmet => "◆", LootKind.Backpack => "▣", LootKind.Grenade => "●", LootKind.Scope => "◎", LootKind.FlareGun => "✸", LootKind.Molotov or LootKind.SmokeGrenade or LootKind.Flashbang => "●", LootKind.SpikeStrip or LootKind.Shield => "▬", LootKind.Ghillie or LootKind.CloakDevice => "◐", _ => "•",
        };

        void OnGUI()
        {
            var cam = Camera.main; if (!cam) return;
            white ??= Texture2D.whiteTexture;
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            tag ??= new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, 1, 1, 0.75f) } };

            // the item closest to the crosshair ray
            KryvonLoot aimed = null; float bestScore = 0.06f;
            var ray = cam.transform.forward;
            foreach (var l in KryvonLoot.All)
            {
                var d = l.transform.position - cam.transform.position; float dist = d.magnitude;
                if (dist > lookRange) continue;
                float off = 1f - Vector3.Dot(d / dist, ray);
                if (off < bestScore) { bestScore = off; aimed = l; }
            }
            foreach (var l in KryvonLoot.All)
            {
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
                else if (dist < nearRange)
                {
                    GUI.color = c; GUI.Label(new Rect(pos.x - 90, pos.y - 22, 180, 20), Icon(l.kind) + " " + l.Describe(), small); GUI.color = Color.white;
                }
            }
        }
    }
}

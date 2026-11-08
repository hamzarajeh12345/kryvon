// On the player: F interacts with the nearest thing – picks up loot, opens/closes doors, opens a grave crate.
using UnityEngine;

namespace Kryvon
{
    public class KryvonLootPicker : MonoBehaviour
    {
        [SerializeField] float range = 2.2f;
        [SerializeField] AudioClip pickupSound;
        PlayerInputReader input; AudioSource src; GUIStyle style, title, btn;
        KryvonLoot loot; KryvonDoor door; KryvonDeathBox box, openBox; KryvonVehicle car, driving;

        void Awake() { input = GetComponent<PlayerInputReader>(); src = GetComponent<AudioSource>(); }

        void Update()
        {
            if (driving)
            {
                if (input != null && input.InteractPressed) { driving.Exit(); driving = null; }
                return;
            }
            loot = null; door = null; box = null; car = null;
            float best = range * range;
            var p = transform.position + Vector3.up * 0.9f;
            foreach (var l in KryvonLoot.All) { float d = (l.transform.position - p).sqrMagnitude; if (d < best) { best = d; loot = l; } }
            foreach (var b in KryvonDeathBox.All) { float d = (b.transform.position - p).sqrMagnitude; if (d < best) { best = d; box = b; loot = null; } }
            foreach (var dr in KryvonDoor.All) { float d = (dr.transform.position + dr.transform.right * 0.5f - p).sqrMagnitude; if (d < best) { best = d; door = dr; loot = null; box = null; } }
            foreach (var v in KryvonVehicle.All) { if (v.Driver) continue; float d = (v.transform.position - p).sqrMagnitude; if (d < Mathf.Max(best, 9f)) { best = d; car = v; loot = null; box = null; door = null; } }
            if (openBox && (openBox != box || !openBox.isActiveAndEnabled)) Close();

            if (input == null || !input.InteractPressed) return;
            if (openBox) { Close(); return; }
            if (car) { car.Enter(gameObject); driving = car; car = null; }
            else if (door) door.Toggle(transform.position);
            else if (box) { openBox = box; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            else if (loot && loot.Apply(gameObject)) { Play(); Destroy(loot.gameObject); }
        }

        void Close() { openBox = null; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void Play() { if (src && pickupSound) src.PlayOneShot(pickupSound, 0.7f); }

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
            btn ??= new GUIStyle(GUI.skin.button) { fontSize = 18 };
            if (openBox)
            {
                var items = openBox.Items; float h = 70 + items.Count * 44;
                var r = new Rect(Screen.width / 2f - 220, Screen.height / 2f - h / 2, 440, h);
                GUI.Box(r, ""); GUI.Box(r, "");
                GUI.Label(new Rect(r.x, r.y + 8, r.width, 30), "✝  " + openBox.owner, title);
                if (items.Count == 0) GUI.Label(new Rect(r.x, r.y + 40, r.width, 30), "—", title);
                for (int i = 0; i < items.Count; i++)
                    if (GUI.Button(new Rect(r.x + 20, r.y + 50 + i * 44, r.width - 40, 38), items[i].Describe(), btn) && openBox.Take(i, gameObject)) { Play(); break; }
                return;
            }
            if (driving) { GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 140, 360, 40), $"{driving.displayName}  ·  {Mathf.Abs(driving.Speed) * 3.6f:0} km/h  ·  [F] exit", style); return; }
            string hint = car ? "Drive " + car.displayName : door ? (door.IsOpen ? "Close door" : "Open door") : box ? "Open " + box.owner + "'s crate" : loot ? loot.Describe() : null;
            if (hint == null) return;
            GUI.Box(new Rect(Screen.width / 2f + 60, Screen.height / 2f + 40, 360, 40), "[F]  " + hint, style);
        }
    }
}

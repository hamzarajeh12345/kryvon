// On the player: shows the nearest loot item and picks it up with F (Interact).
using UnityEngine;

namespace Kryvon
{
    /// on the player: shows the nearest item and picks it up with F
    public class KryvonLootPicker : MonoBehaviour
    {
        [SerializeField] float range = 2.2f;
        [SerializeField] AudioClip pickupSound;
        PlayerInputReader input; KryvonLoot near; AudioSource src; GUIStyle style;

        void Awake() { input = GetComponent<PlayerInputReader>(); src = GetComponent<AudioSource>(); }

        void Update()
        {
            near = null; float best = range * range;
            foreach (var l in KryvonLoot.All)
            {
                float d = (l.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; near = l; }
            }
            if (near != null && input != null && input.InteractPressed && near.Apply(gameObject))
            {
                if (src && pickupSound) src.PlayOneShot(pickupSound, 0.7f);
                Destroy(near.gameObject); near = null;
            }
        }

        void OnGUI()
        {
            if (near == null) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            var r = new Rect(Screen.width / 2f + 60, Screen.height / 2f + 40, 340, 40);
            GUI.Box(r, $"[F]  {near.Describe()}", style);
        }
    }
}

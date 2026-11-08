// A glass pane: blocks the player until it is shot. Then it shatters and you can shoot or climb through.
using UnityEngine;

namespace Kryvon
{
    public class KryvonWindow : MonoBehaviour, IDamageable
    {
        [SerializeField] float hp = 1f;
        [SerializeField] AudioClip shatterSound;
        bool broken;
        public bool Broken => broken;
        public void TakeDamage(DamageInfo info)
        {
            if (broken) return;
            hp -= info.Amount; if (hp > 0) return;
            broken = true;
            KryvonFX.Glass(transform.position);
            if (shatterSound) AudioSource.PlayClipAtPoint(shatterSound, transform.position, 1f);
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }
    }
}

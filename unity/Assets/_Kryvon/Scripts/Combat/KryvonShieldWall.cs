// Deployable shield: steel cover with HP that blocks bullets until destroyed.
using UnityEngine;

namespace Kryvon
{
    public class KryvonShieldWall : MonoBehaviour, IDamageable
    {
        [SerializeField] float hp = 600f;
        [SerializeField] AudioClip hitSound;
        public void TakeDamage(DamageInfo info)
        {
            hp -= info.Amount;
            if (hitSound && Random.value < 0.4f) AudioSource.PlayClipAtPoint(hitSound, transform.position, 0.6f);
            if (hp <= 0) { KryvonFX.Dust(transform.position + Vector3.up * 0.6f); Destroy(gameObject); }
        }
    }
}

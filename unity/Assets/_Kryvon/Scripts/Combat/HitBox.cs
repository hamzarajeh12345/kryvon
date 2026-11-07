// HitBox – put on each collider of a character (head sphere, body capsule…) and point it at the Health.
// WeaponController checks for it to know whether a shot was a headshot.
using UnityEngine;

namespace Kryvon
{
    public class HitBox : MonoBehaviour
    {
        public Health owner;
        public bool isHead;
        [Tooltip("Extra multiplier, e.g. 0.8 for legs.")]
        public float damageMultiplier = 1f;

        void Reset() => owner = GetComponentInParent<Health>();
    }
}

// A thrown grenade/bottle in flight: counts down its fuse, or (molotov) breaks on first impact.
using UnityEngine;

namespace Kryvon
{
    public class KryvonProjectile : MonoBehaviour
    {
        public KryvonThrowables owner; public Throwable kind; public float fuse = 3f;
        bool done;
        void Update()
        {
            if (done) return;
            fuse -= Time.deltaTime;
            if (fuse <= 0) Fire();
            if (kind == Throwable.Molotov || kind == Throwable.Smoke) KryvonFX.Trail(transform.position, kind == Throwable.Molotov);
        }
        void OnCollisionEnter(Collision c) { if (kind == Throwable.Molotov && c.relativeVelocity.magnitude > 2f) Fire(); }
        void Fire() { if (done) return; done = true; if (owner) owner.Detonate(kind, transform.position, gameObject); else Destroy(gameObject); }
    }
}

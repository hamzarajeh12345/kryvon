// A hinged wooden door: F opens/closes it, swinging away from the player. Blocks movement and bullets when closed;
// bullets, melee and grenades splinter it (about 9 rifle hits, 2 shotgun blasts, one grenade).
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public class KryvonDoor : MonoBehaviour, IDamageable
    {
        public static readonly List<KryvonDoor> All = new();
        [SerializeField] float openAngle = 100f, speed = 300f;
        [SerializeField] AudioClip openSound, closeSound;
        float target, angle; bool open;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [SerializeField] float hp = 360f;
        [SerializeField] AudioClip breakSound;
        public bool IsOpen => open;
        public void TakeDamage(DamageInfo info)
        {
            if (hp <= 0) return;
            hp -= info.Amount; KryvonFX.Dust(transform.position + Vector3.up * 1f);
            if (hp > 0) return;
            // splinters: a few planks that fall and fade out
            var r = GetComponentInChildren<Renderer>(); var b = r ? r.bounds : new Bounds(transform.position + Vector3.up, new Vector3(1, 2, 0.1f));
            for (int i = 0; i < 7; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube); p.name = "Splinter";
                p.transform.position = b.center + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.8f, 0.8f), Random.Range(-0.1f, 0.1f));
                p.transform.rotation = Random.rotation; p.transform.localScale = new Vector3(Random.Range(0.08f, 0.16f), Random.Range(0.35f, 0.8f), 0.04f);
                if (r) p.GetComponent<Renderer>().sharedMaterial = r.sharedMaterial;
                var rb = p.AddComponent<Rigidbody>(); rb.mass = 0.5f; rb.AddExplosionForce(160f, info.Source ? info.Source.transform.position : b.center - transform.forward, 6f, 0.3f);
                Destroy(p, 6f);
            }
            if (breakSound) AudioSource.PlayClipAtPoint(breakSound, b.center, 0.9f);
            KryvonFX.Dust(b.center); Destroy(gameObject);
        }
        public void Toggle(Vector3 from)
        {
            manualUntil = Time.time + 4f; autoOpened = false;   // F still works and wins over the automatic opening for a moment
            Swing(from);
        }
        void Swing(Vector3 from)
        {
            open = !open;
            if (open)
            {
                var n = transform.parent ? transform.parent.forward : Vector3.forward;
                target = Vector3.Dot(from - transform.position, n) > 0 ? -openAngle : openAngle;
            }
            else target = 0;
            var clip = open ? openSound : closeSound;
            if (clip) AudioSource.PlayClipAtPoint(clip, transform.position, 0.7f);
        }
        // automatic: the door swings open (away from you) when someone walks up to it, and closes again behind them
        float nextCheck, manualUntil, emptySince; bool autoOpened;
        static Transform player;
        void AutoDoor()
        {
            if (Time.time < nextCheck) return; nextCheck = Time.time + 0.15f + Random.value * 0.05f;
            if (Time.time < manualUntil || hp <= 0) return;
            Vector3 c = transform.position + transform.right * 0.5f;
            if (!player) { var g = GameObject.FindGameObjectWithTag("Player"); if (g) player = g.transform; }
            Transform who = null; float best = 1.7f * 1.7f;
            if (player) { var d = player.position - c; d.y *= 2.5f; if (d.sqrMagnitude < best) { best = d.sqrMagnitude; who = player; } }
            foreach (var b in KryvonBot.All) { if (!b || !b.isActiveAndEnabled) continue; var d = b.transform.position - c; d.y *= 2.5f; if (d.sqrMagnitude < best) { best = d.sqrMagnitude; who = b.transform; } }
            if (who) { emptySince = Time.time; if (!open) { Swing(who.position); autoOpened = true; } }
            else if (open && autoOpened && Time.time - emptySince > 2.5f)
            {   // nobody within 3 m for a while: close it again (doesn't slam on someone standing in the doorway)
                bool near = player && (player.position - c).sqrMagnitude < 9f;
                if (!near) foreach (var b in KryvonBot.All) if (b && b.isActiveAndEnabled && (b.transform.position - c).sqrMagnitude < 9f) { near = true; break; }
                if (near) emptySince = Time.time; else { Swing(c); autoOpened = false; }
            }
        }
        void Update()
        {
            AutoDoor();
            if (Mathf.Approximately(angle, target)) return;
            angle = Mathf.MoveTowards(angle, target, speed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0, angle, 0);
        }
    }
}

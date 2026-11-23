// Health – HP, helmet/vest armour, knock (team modes) and revive, death.
// Anything that can be shot implements IDamageable. HitBox marks head/body colliders.
using System;
using UnityEngine;

namespace Kryvon
{
    public struct DamageInfo
    {
        public float Amount;
        public bool Headshot;
        public bool Limb;              // legs / arms: less damage, the vest doesn't cover them
        public GameObject Source;     // who caused it (null for the zone)
        public string Cause;          // weapon name, "Al-Tawq", "Bled out" …
        public bool IgnoreArmor;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }

    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Identity")]
        [Tooltip("Players with the same team id never damage each other.")]
        public int teamId;

        [Header("Health")]
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Set by MatchManager: true in duo/squad, where lethal damage knocks you down first.")]
        public bool canBeKnocked;
        [SerializeField] float knockedPool = 100f;
        [SerializeField] float knockedBleedPerSecond = 2.5f;

        [Header("Armour (0 = none)")]
        [Range(0f, 0.6f)] public float helmetReduction;
        [Range(0f, 0.6f)] public float vestReduction;
        public float helmetDurability, vestDurability;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsAlive { get; private set; } = true;
        public bool IsKnocked { get; private set; }
        public int KnockCount { get; private set; }
        public DamageInfo LastDamage { get; private set; }

        public event Action<Health, DamageInfo> OnDamaged;
        public event Action<Health, DamageInfo> OnKnocked;
        public event Action<Health> OnRevived;
        public event Action<Health, DamageInfo> OnDied;

        void Awake() => Current = maxHealth;

        public event Action<Health, bool> OnArmorBroken;          // true = helmet, false = vest
        /// a blast close by tears the helmet off (it is gone until you find another)
        public void BlowOffHelmet() { if (helmetDurability > 0f || helmetReduction > 0f) ArmorBroke(true); }
        /// destroyed armour: no more protection, pieces fly off, and the player is told
        void ArmorBroke(bool helmet)
        {
            if (helmet) { helmetReduction = 0f; helmetDurability = 0f; } else { vestReduction = 0f; vestDurability = 0f; }
            var at = transform.position + Vector3.up * (helmet ? 1.72f : 1.25f);
            KryvonFX.ArmorShatter(at, helmet);
            OnArmorBroken?.Invoke(this, helmet);
            if (CompareTag("Player")) KryvonKillFeed.Notice(helmet ? "HELMET DESTROYED" : "VEST DESTROYED");
        }

        // badly hurt or knocked: blood drops on the ground where you walk or crawl (fade after a few seconds)
        float nextDrip;
        void Bleed()
        {
            if (!IsAlive || Time.time < nextDrip) return;
            bool low = Current < maxHealth * 0.3f;
            if (!IsKnocked && !low) return;
            nextDrip = Time.time + (IsKnocked ? 0.7f : 1.4f);
            var cam = Camera.main; if (cam && (cam.transform.position - transform.position).sqrMagnitude > 60f * 60f) return;
            var from = transform.position + Vector3.up * 0.8f + new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), 0, UnityEngine.Random.Range(-0.25f, 0.25f));
            foreach (var h in Physics.RaycastAll(from, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(transform)) { KryvonFX.BloodDrop(h.point, h.normal); break; }
        }

        void Update()
        {
            Bleed();
            if (!IsKnocked) return;
            // bleeding out gets faster every time you are knocked again
            Current -= knockedBleedPerSecond * (1f + 0.5f * (KnockCount - 1)) * Time.deltaTime;
            if (Current <= 0f) Die(new DamageInfo { Cause = "Bled out", Source = LastDamage.Source });
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return;
            // career stats for the lobby (the local player's hits on others)
            if (info.Source && info.Source.CompareTag("Player") && !CompareTag("Player"))
            { KryvonWallet.Count("damage", Mathf.RoundToInt(Mathf.Min(info.Amount, 200f))); if (info.Headshot) KryvonWallet.Count("headshots"); KryvonWallet.Count("hits"); }

            float dmg = info.Amount;
            if (!info.IgnoreArmor && !IsKnocked)
            {
                if (info.Headshot && helmetDurability > 0f) { helmetDurability -= dmg; dmg *= 1f - helmetReduction; if (helmetDurability <= 0f) ArmorBroke(true); }
                else if (!info.Headshot && !info.Limb && vestDurability > 0f) { vestDurability -= dmg; dmg *= 1f - vestReduction; if (vestDurability <= 0f) ArmorBroke(false); }
            }

            if (CompareTag("Player") && info.Cause != "Al-Tawq") dmg *= KryvonClass.DamageTakenMul;   // heavy class: armour
            Current -= dmg;
            info.Amount = dmg;
            LastDamage = info;
            OnDamaged?.Invoke(this, info);

            if (Current > 0f) return;
            if (!IsKnocked && canBeKnocked && (MatchManager.Instance == null || MatchManager.Instance.HasStandingTeammate(this))) Knock(info);
            else Die(info);
        }

        void Knock(DamageInfo info)
        {
            IsKnocked = true;
            KnockCount++;
            Current = knockedPool;
            OnKnocked?.Invoke(this, info);
        }

        /// <summary>Called when a teammate finishes reviving.</summary>
        public void Revive(float healthAfter = 20f)
        {
            if (!IsAlive || !IsKnocked) return;
            IsKnocked = false;
            Current = healthAfter;
            OnRevived?.Invoke(this);
        }

        public void Heal(float amount, float cap = 100f)
        {
            if (!IsAlive || IsKnocked) return;
            Current = Mathf.Max(Current, Mathf.Min(cap, Current + amount));
        }

        public void Kill(DamageInfo info) => Die(info);

        /// <summary>Back to full health (training dummies, respawn modes).</summary>
        public void Respawn()
        {
            IsAlive = true; IsKnocked = false; KnockCount = 0; Current = maxHealth;
        }

        void Die(DamageInfo info)
        {
            if (!IsAlive) return;
            IsAlive = false;
            IsKnocked = false;
            Current = 0f;
            OnDied?.Invoke(this, info);
        }
    }
}

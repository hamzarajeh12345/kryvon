// What the player carries besides weapons, and using it:
//   4 Bandage      +10 HP, up to 75 %         2 s
//   5 First Aid    to 75 %                     6 s
//   6 Med Kit      to 100 %                    8 s
//   7 Energy Drink +30 boost                   2 s
//   8 Painkillers  +60 boost                   4 s
//   9 Adrenaline   boost to 100                6 s
// Boost slowly drains; while it lasts you heal over time, and above 60 you run faster.
// Moving slows healing to a walk; firing, sprinting or driving cancels it.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public enum Consumable { Bandage, FirstAid, MedKit, EnergyDrink, Painkillers, Adrenaline }

    public class KryvonGear : MonoBehaviour
    {
        public int backpack, grenades, flares;
        public int[] counts = new int[6];
        public float Boost { get; private set; }            // 0..100
        public Consumable? Using { get; private set; }
        public float UseProgress { get; private set; }       // 0..1
        public int drinks { get => counts[3]; set => counts[3] = value; }
        public int syringes { get => counts[5]; set => counts[5] = value; }
        [SerializeField] GameObject[] backpackModels;
        [SerializeField] AudioClip useSound, doneSound;

        public static readonly string[] Names = { "Bandage", "First Aid", "Med Kit", "Energy Drink", "Painkillers", "Adrenaline" };
        static readonly float[] Seconds = { 2f, 6f, 8f, 2f, 4f, 6f };
        Health hp; PlayerController body; WeaponController weapons; PlayerInputReader input; float useTime, regenTick;

        void Awake() { hp = GetComponent<Health>(); body = GetComponent<PlayerController>(); weapons = GetComponent<WeaponController>(); input = GetComponent<PlayerInputReader>(); }

        public void SetBackpack(int level)
        {
            backpack = level;
            if (backpackModels == null) return;
            for (int i = 0; i < backpackModels.Length; i++) if (backpackModels[i]) backpackModels[i].SetActive(i == level - 1);
        }
        public void Add(Consumable c, int n = 1) => counts[(int)c] += n;

        public bool CanUse(Consumable c)
        {
            if (counts[(int)c] <= 0 || hp == null || !hp.IsAlive) return false;
            return c switch
            {
                Consumable.Bandage or Consumable.FirstAid => hp.Current < 75f,
                Consumable.MedKit => hp.Current < 100f,
                _ => Boost < 100f,
            };
        }
        public void StartUse(Consumable c)
        {
            if (!CanUse(c) || Using.HasValue) return;
            Using = c; useTime = 0; UseProgress = 0;
            if (useSound) AudioSource.PlayClipAtPoint(useSound, transform.position, 0.6f);
        }
        public void Cancel() { Using = null; UseProgress = 0; }

        void Finish(Consumable c)
        {
            counts[(int)c]--;
            switch (c)
            {
                case Consumable.Bandage: hp.Heal(10f, 75f); break;
                case Consumable.FirstAid: hp.Heal(75f - hp.Current, 75f); break;
                case Consumable.MedKit: hp.Heal(100f, 100f); break;
                case Consumable.EnergyDrink: Boost = Mathf.Min(100, Boost + 30); break;
                case Consumable.Painkillers: Boost = Mathf.Min(100, Boost + 60); break;
                case Consumable.Adrenaline: Boost = 100; break;
            }
            if (doneSound) AudioSource.PlayClipAtPoint(doneSound, transform.position, 0.6f);
            Using = null; UseProgress = 0;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                var keys = new[] { kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
                for (int i = 0; i < keys.Length; i++) if (keys[i].wasPressedThisFrame) { if (Using == (Consumable)i) Cancel(); else { Cancel(); StartUse((Consumable)i); } }
            }
            if (Using.HasValue)
            {
                bool interrupted = (input && (input.FirePressed || input.SprintHeld)) || (body && !body.enabled) || (hp && !hp.IsAlive);
                if (interrupted) Cancel();
                else
                {
                    useTime += Time.deltaTime; UseProgress = useTime / Seconds[(int)Using.Value];
                    if (UseProgress >= 1f) Finish(Using.Value);
                }
            }
            if (body) body.SpeedMultiplier = Using.HasValue ? 0.45f : Boost > 60f ? 1.06f : 1f;
            // boost: drains over ~3 minutes; heals 1 HP every 2 s (every 1 s above 60)
            if (Boost > 0f)
            {
                Boost = Mathf.Max(0, Boost - Time.deltaTime * 0.55f);
                regenTick -= Time.deltaTime;
                if (regenTick <= 0f) { regenTick = Boost > 60 ? 1f : 2f; if (hp && hp.IsAlive && !hp.IsKnocked && hp.Current < 100f) hp.Heal(1f, 100f); }
            }
        }
    }
}

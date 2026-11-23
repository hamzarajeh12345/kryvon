// SafeZone ("Al-Tawq") – shrinking circle in phases: wait → shrink → next phase.
// Each new circle is picked fully inside the previous one. Players outside take damage that ignores armour.
// Starts when MatchManager enters InMatch (or immediately if there is no MatchManager).
using System;
using UnityEngine;

namespace Kryvon
{
    [Serializable]
    public class ZonePhase
    {
        public float waitSeconds = 60f;
        public float shrinkSeconds = 40f;
        public float radius = 300f;
        public float damagePerSecond = 1f;
    }

    public class SafeZone : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] Vector3 mapCenter = Vector3.zero;
        [SerializeField] float startRadius = 1150f;
        [Tooltip("Landing phase: no zone, no damage. The first circle appears halfway through.")]
        [SerializeField] float landingSeconds = 120f;

        [Header("Phases")]
        [SerializeField] ZonePhase[] phases =
        {
            // first circle 500 m; waits a little longer than before because players move at realistic speeds now
            new ZonePhase { waitSeconds = 95, shrinkSeconds = 60, radius = 500, damagePerSecond = 0.6f },
            new ZonePhase { waitSeconds = 70, shrinkSeconds = 50, radius = 320, damagePerSecond = 1f },
            new ZonePhase { waitSeconds = 55, shrinkSeconds = 42, radius = 200, damagePerSecond = 2f },
            new ZonePhase { waitSeconds = 40, shrinkSeconds = 30, radius = 130, damagePerSecond = 3.5f },
            new ZonePhase { waitSeconds = 35, shrinkSeconds = 25, radius = 75, damagePerSecond = 5f },
            new ZonePhase { waitSeconds = 30, shrinkSeconds = 20, radius = 40, damagePerSecond = 7.5f },
            new ZonePhase { waitSeconds = 25, shrinkSeconds = 20, radius = 18, damagePerSecond = 10f },
            new ZonePhase { waitSeconds = 20, shrinkSeconds = 25, radius = 2, damagePerSecond = 14f },
        };

        [Header("Visual")]
        [Tooltip("A Unity Cylinder with a transparent blue material (default cylinder: radius 0.5, height 2).")]
        [SerializeField] Transform wallVisual;
        [SerializeField] float wallHeight = 400f;
        [SerializeField] float damageTickSeconds = 0.5f;

        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        public Vector3 NextCenter { get; private set; }
        public float NextRadius { get; private set; }
        public int PhaseIndex { get; private set; }
        public bool Shrinking { get; private set; }
        public bool Running { get; private set; }
        public bool Landing { get; private set; }
        public bool CircleShown => Running || (Landing && Timer <= landingSeconds * 0.5f);
        /// <summary>Seconds until the shrink starts (while waiting) or ends (while shrinking).</summary>
        public float Timer { get; private set; }
        public float CurrentDamagePerSecond => phases[Mathf.Min(PhaseIndex, phases.Length - 1)].damagePerSecond;

        public event Action<int> OnWaitStarted;    // phase index, new circle is known
        public event Action<int> OnShrinkStarted;

        Vector3 fromCenter;
        float fromRadius, tickTimer;

        void Start()
        {
            // tuning that must win over the values saved in the scene: the first circle is 500 m, and every wait / shrink is
            // 20% longer because players now move at realistic speeds
            if (phases.Length > 0)
            {
                phases[0].radius = 500f;
                for (int i = 1; i < phases.Length; i++) phases[i].radius = Mathf.Min(phases[i].radius, phases[i - 1].radius * 0.68f);
                foreach (var ph in phases) { ph.waitSeconds *= 1.2f; ph.shrinkSeconds *= 1.6f; }   // the edge moves at ≤ 1.9 m/s – slower than a run (2.6)
            }
            Center = mapCenter; Radius = startRadius;
            UpdateVisual();
            if (KryvonDrop.Active) return;          // the drop starts the zone when the player jumps
            if (MatchManager.Instance != null) MatchManager.Instance.OnPhaseChanged += p => { if (p == MatchPhase.InMatch) Begin(); };
            else Begin();
        }

        public void Begin()
        {
            if (Running || Landing || phases.Length == 0) return;
            Landing = true; Timer = landingSeconds;
            StartPhase(0); Timer = landingSeconds;   // the next circle is known but hidden for the first half of the landing
        }
        void EndLanding()
        {
            Landing = false; Running = true;
            Timer = phases[0].waitSeconds;
        }

        public bool IsInside(Vector3 p)
        {
            Vector2 d = new Vector2(p.x - Center.x, p.z - Center.z);
            return d.sqrMagnitude <= Radius * Radius;
        }

        void StartPhase(int i)
        {
            PhaseIndex = i;
            Shrinking = false;
            Timer = phases[i].waitSeconds;
            // next centre: random point such that the new circle stays inside the current one
            float slack = Mathf.Max(0f, Radius - phases[i].radius);
            Vector2 r = UnityEngine.Random.insideUnitCircle * slack;
            NextCenter = Center + new Vector3(r.x, 0f, r.y);
            NextRadius = phases[i].radius;
            OnWaitStarted?.Invoke(i);
        }

        void Update()
        {
            if (Landing) { Timer -= Time.deltaTime; if (Timer <= 0f) EndLanding(); return; }
            if (!Running) return;
            Timer -= Time.deltaTime;

            if (!Shrinking && Timer <= 0f)
            {
                Shrinking = true;
                Timer = phases[PhaseIndex].shrinkSeconds;
                fromCenter = Center; fromRadius = Radius;
                OnShrinkStarted?.Invoke(PhaseIndex);
            }
            else if (Shrinking)
            {
                float t = 1f - Mathf.Max(0f, Timer) / phases[PhaseIndex].shrinkSeconds;
                Center = Vector3.Lerp(fromCenter, NextCenter, t);
                Radius = Mathf.Lerp(fromRadius, NextRadius, t);
                if (Timer <= 0f)
                {
                    if (PhaseIndex + 1 < phases.Length) StartPhase(PhaseIndex + 1);
                    else { Shrinking = false; Running = false; } // final circle stays
                }
            }

            UpdateVisual();
            DamageOutside();
        }

        void DamageOutside()
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer > 0f) return;
            tickTimer = damageTickSeconds;
            var players = MatchManager.Instance != null ? MatchManager.Instance.AllPlayers : (System.Collections.Generic.IReadOnlyList<Health>)FindObjectsByType<Health>();
            float dmg = CurrentDamagePerSecond * damageTickSeconds;
            foreach (var h in players)
                if (h != null && h.IsAlive && !IsInside(h.transform.position))
                    h.TakeDamage(new DamageInfo { Amount = dmg, IgnoreArmor = true, Cause = "Al-Tawq" });
        }

        void UpdateVisual()
        {
            if (wallVisual == null) return;
            wallVisual.gameObject.SetActive(CircleShown);
            wallVisual.position = new Vector3(Center.x, mapCenter.y + wallHeight * 0.5f - 20f, Center.z);
            wallVisual.localScale = new Vector3(Radius * 2f, wallHeight * 0.5f, Radius * 2f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
            DrawCircle(Application.isPlaying ? Center : mapCenter, Application.isPlaying ? Radius : startRadius);
            if (Application.isPlaying && Running) { Gizmos.color = Color.white; DrawCircle(NextCenter, NextRadius); }
        }

        static void DrawCircle(Vector3 c, float r)
        {
            Vector3 prev = c + new Vector3(r, 0, 0);
            for (int i = 1; i <= 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, p); prev = p;
            }
        }
    }
}

// MatchManager – the match state machine and team bookkeeping.
// Lobby → Island (waiting area) → Plane → InMatch → Ended.
// Every Health in the scene is registered automatically; teams are grouped by Health.teamId.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kryvon
{
    public enum MatchPhase { Lobby, Island, Plane, InMatch, Ended }

    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Mode")]
        [Tooltip("1 = solo, 2 = duo, 4 = squad. In team modes players get knocked before dying.")]
        [SerializeField, Range(1, 4)] int teamSize = 1;

        [Header("Timing (seconds)")]
        [SerializeField] float islandSeconds = 20f;
        [Tooltip("Used only if no PlaneController calls BeginMatch() itself.")]
        [SerializeField] float planeFallbackSeconds = 30f;
        [SerializeField] bool autoStart = true;

        public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
        public float PhaseTimer { get; private set; }
        public int TeamSize => teamSize;
        public int PlayersAlive => all.Count(h => h.IsAlive);
        public int TeamsAlive => teams.Count(t => t.Value.Any(h => h.IsAlive));
        public IReadOnlyList<Health> AllPlayers => all;

        public event Action<MatchPhase> OnPhaseChanged;
        /// <summary>(teamId, place) – place 2 = runner-up …</summary>
        public event Action<int, int> OnTeamEliminated;
        /// <summary>(winning teamId, or -1 if nobody survived)</summary>
        public event Action<int> OnMatchEnded;
        /// <summary>(victim, info) – feed the kill feed from here.</summary>
        public event Action<Health, DamageInfo> OnPlayerDied;
        public event Action<Health, DamageInfo> OnPlayerKnocked;

        readonly List<Health> all = new();
        readonly Dictionary<int, List<Health>> teams = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            foreach (var h in FindObjectsByType<Health>()) Register(h);
            if (autoStart) SetPhase(MatchPhase.Island);
        }

        public void Register(Health h)
        {
            if (all.Contains(h)) return;
            all.Add(h);
            if (!teams.TryGetValue(h.teamId, out var list)) teams[h.teamId] = list = new List<Health>();
            list.Add(h);
            h.canBeKnocked = teamSize > 1;
            h.OnDied += HandleDeath;
            h.OnKnocked += HandleKnock;
        }

        public bool HasStandingTeammate(Health h) =>
            teams.TryGetValue(h.teamId, out var list) && list.Any(m => m != h && m.IsAlive && !m.IsKnocked);

        public IEnumerable<Health> TeammatesOf(Health h) =>
            teams.TryGetValue(h.teamId, out var list) ? list.Where(m => m != h) : Enumerable.Empty<Health>();

        void Update()
        {
            if (Phase == MatchPhase.Lobby || Phase == MatchPhase.Ended) return;
            PhaseTimer -= Time.deltaTime;
            if (Phase == MatchPhase.Island && PhaseTimer <= 0f) SetPhase(MatchPhase.Plane);
            else if (Phase == MatchPhase.Plane && PhaseTimer <= 0f) BeginMatch();
        }

        /// <summary>PlaneController calls this when the last player has left the plane.</summary>
        public void BeginMatch()
        {
            if (Phase == MatchPhase.Plane || Phase == MatchPhase.Island) SetPhase(MatchPhase.InMatch);
        }

        void SetPhase(MatchPhase p)
        {
            Phase = p;
            PhaseTimer = p == MatchPhase.Island ? islandSeconds : p == MatchPhase.Plane ? planeFallbackSeconds : 0f;
            OnPhaseChanged?.Invoke(p);
        }

        void HandleKnock(Health h, DamageInfo info)
        {
            OnPlayerKnocked?.Invoke(h, info);
            // whole team down → everyone who is knocked dies
            if (!HasStandingTeammate(h))
                foreach (var m in teams[h.teamId].ToList())
                    if (m.IsAlive && m.IsKnocked) m.Kill(new DamageInfo { Cause = "Squad wiped", Source = info.Source });
        }

        void HandleDeath(Health h, DamageInfo info)
        {
            OnPlayerDied?.Invoke(h, info);
            if (Phase == MatchPhase.Ended) return;
            if (teams[h.teamId].Any(m => m.IsAlive)) return;

            int alive = TeamsAlive;
            OnTeamEliminated?.Invoke(h.teamId, alive + 1);
            if (alive <= 1)
            {
                var winner = teams.FirstOrDefault(t => t.Value.Any(m => m.IsAlive));
                SetPhase(MatchPhase.Ended);
                OnMatchEnded?.Invoke(winner.Value != null ? winner.Key : -1);
            }
        }
    }
}

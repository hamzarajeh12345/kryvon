// The match: fills it with AI opponents that ride the same jet, and ends it.
//   • you die  → "#rank" screen (place, kills, time survived, coins earned) → back to the lobby
//   • last one standing → VICTORY screen
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public class KryvonBots : MonoBehaviour
    {
        [SerializeField] GameObject[] templates;
        [SerializeField] GameObject chuteTemplate;
        [SerializeField] Font font;
        [SerializeField] AudioClip victorySound;

        static readonly string[] Names = { "Abu Fares", "Saqr_77", "Nimr", "Layth", "Ghaith", "Rakan", "Yazan", "Omar.K", "Tareq", "Haya", "Dana", "Khaled", "Fadi", "Majd", "Nour", "Zaid", "Laith99", "Basel", "Hamoudeh", "Sanad", "Qusai", "Moath", "Ward", "Jood", "Sultan", "Hashem", "Yamen", "Bader", "Talal", "Raed", "Shadi" };
        readonly List<KryvonBot> bots = new();
        Health player; float startTime; int kills, place; bool ended, won, spawned; float endAt;
        int rankBefore, dmg0, hs0; string killer, killWeapon;

        /// bot skill for this match: 0 easy · 1 normal · 2 pro. Matched to the player's rank (like real matchmaking)
        /// unless the player picked one ("kryvon.botskill" 0/1/2, -1 = auto). Each bot gets its own skill around that level.
        public static int Difficulty
        {
            get
            {
                int pick = PlayerPrefs.GetInt("kryvon.botskill", -1); if (pick >= 0) return Mathf.Clamp(pick, 0, 2);
                int pts = KryvonLobby.RankPoints(); return pts < 180 ? 0 : pts < 800 ? 1 : 2;
            }
        }
        static float SkillRoll(int diff) => diff switch { 0 => Random.Range(0.05f, 0.4f), 1 => Random.Range(0.25f, 0.7f), _ => Random.Range(0.55f, 1f) };

        void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.GetComponent<Health>();
            // no more respawning practice targets: this is a real match
            var squad = GameObject.Find("Squad"); if (squad) squad.SetActive(false);
            if (player)
            {
                var death = player.GetComponent<KryvonDeath>();
                var f = typeof(KryvonDeath).GetField("respawnAfter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); if (death && f != null) f.SetValue(death, 0f);
                player.OnDied += (_, info) => { place = Alive(); killer = info.Source && info.Source != player.gameObject ? info.Source.name : null; killWeapon = info.Cause; EndSoon(false); };
            }
            startTime = Time.time;
            rankBefore = KryvonLobby.RankPoints(); dmg0 = KryvonWallet.Stat("damage"); hs0 = KryvonWallet.Stat("headshots");
            if (!KryvonDrop.Instance) { Spawn(); foreach (var b in bots) b.Jump(RandomSky(), chuteTemplate, Random.ColorHSV(), Random.ColorHSV()); }
        }

        /// teammates from the lobby: 0 solo, 1 duo, 3 squad (kryvon.team = 0 / 1 / 2)
        public static int Mates => new[] { 0, 1, 3 }[Mathf.Clamp(PlayerPrefs.GetInt("kryvon.team", 0), 0, 2)];
        public static readonly string[] MateNames = { "Zain", "Layla", "Omar" };
        readonly List<KryvonBot> mates = new();
        static int Count => Mathf.Clamp(KryvonGraphics.Quality, 0, 4) switch { 0 => 15, 1 => 23, 2 => 31, _ => 39 };

        void Spawn()
        {
            if (spawned || templates == null || templates.Length == 0) return; spawned = true;
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                var t = templates[i % templates.Length]; if (!t) continue;
                var g = Instantiate(t); g.SetActive(true);
                bool mate = i < Mates; g.name = mate ? MateNames[i % MateNames.Length] : Names[i % Names.Length];
                var h = g.GetComponent<Health>(); h.teamId = mate && player ? player.teamId : 100 + i;
                h.OnDied += (_, info) =>
                {
                    if (info.Source && info.Source.CompareTag("Player"))
                    {
                        kills++; KryvonWallet.Add("kills");
                        if (player && player.IsAlive && Alive() == 1) KryvonFinisher.Play();   // the match-winning kill: slow motion
                        if (KryvonSkins.AbsoluteZero(info.Source.GetComponent<WeaponController>())) KryvonFX.IceBurst(g.transform.position + Vector3.up);
                    }
                    CheckWin();
                };
                var b = g.GetComponent<KryvonBot>(); b.skill = mate ? 0.7f : SkillRoll(Difficulty); if (mate) mates.Add(b);
                if (!mate && Random.value < 0.15f) b.ArmSniper();                 // a few snipers in every match (watch for scope glare)
                b.jumpAt = Random.Range(0.12f, 0.88f); b.Board(); bots.Add(b);
                g.transform.position = new Vector3(0, -500, 0);
            }
        }

        Vector3 RandomSky() => new(Random.Range(-380f, 380f), 300f, Random.Range(-380f, 380f));

        void Update()
        {
            var d = KryvonDrop.Instance;
            if (d && d.S == KryvonDrop.Stage.Plane) Spawn();
            if (d && spawned)
                foreach (var b in bots)
                    if (b && mates.Contains(b) && b.S == KryvonBot.State.Plane && (d.S == KryvonDrop.Stage.Freefall || d.S == KryvonDrop.Stage.Chute || d.S == KryvonDrop.Stage.Landed))
                    {   // mates jump right after the player
                        var skin = KryvonWallet.Chutes[Random.Range(0, KryvonWallet.Chutes.Length)];
                        b.Jump(player.transform.position + Vector3.up * 6f + Random.insideUnitSphere * 8f, chuteTemplate, skin.a, skin.b);
                    }
                    else if (b && !mates.Contains(b) && b.S == KryvonBot.State.Plane && d.S != KryvonDrop.Stage.Waiting && (d.PathT >= b.jumpAt || d.PathT >= 0.86f) && d.OverMap(d.PlanePos))
                    {
                        var skin = KryvonWallet.Chutes[Random.Range(0, KryvonWallet.Chutes.Length)];
                        b.Jump(d.PlanePos - Vector3.up * 3f + Random.insideUnitSphere * 4f, chuteTemplate, skin.a, skin.b);
                    }
                    else if (b && b.S == KryvonBot.State.Plane && d.S != KryvonDrop.Stage.Waiting && d.PathT >= 1f)
                    { var skin = KryvonWallet.Chutes[Random.Range(0, KryvonWallet.Chutes.Length)]; b.Jump(RandomSky(), chuteTemplate, skin.a, skin.b); }
            if (ended && Time.time > endAt && !shown)
            {
                shown = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                PlayerPrefs.Save();
                KryvonMatchSummary.Show(new KryvonMatchSummary.Result
                {
                    won = won, place = Mathf.Max(1, place), players = Count + 1 - Mates, kills = kills, seconds = Time.time - startTime,
                    damage = Mathf.Max(0, KryvonWallet.Stat("damage") - dmg0), headshots = Mathf.Max(0, KryvonWallet.Stat("headshots") - hs0),
                    coins = earned, rankBefore = rankBefore, rankAfter = KryvonLobby.RankPoints(), killer = killer, weapon = killWeapon,
                }, font);
            }
        }
        bool shown;

        int Alive() { int a = player && player.IsAlive ? 1 : 0; foreach (var b in bots) if (b && !mates.Contains(b) && b.S != KryvonBot.State.Dead) a++; return a; }
        void CheckWin() { if (!ended && player && player.IsAlive && Alive() == 1) { won = true; place = 1; EndSoon(true); } }
        void EndSoon(bool victory)
        {
            if (ended) return; ended = true; endAt = Time.time + (victory ? 1.5f : 3.5f);
            int coins = (victory ? 1500 : 0) + kills * 150 + Mathf.Max(0, (Count + 1 - Mates - place)) * 20;
            KryvonWallet.Coins += coins; earned = coins;
            if (place > 0 && place <= 10) KryvonWallet.Add("top10");
            if (!victory) KryvonWallet.Add("deaths");
            if (place > 0) PlayerPrefs.SetInt("s.best", Mathf.Min(PlayerPrefs.GetInt("s.best", 99), place));
            if (victory && player) KryvonFX.Fireworks(player.transform.position);     // victory: fireworks over the winner
            if (victory) { KryvonWallet.Add("wins"); if (victorySound) AudioSource.PlayClipAtPoint(victorySound, Camera.main ? Camera.main.transform.position : Vector3.zero); }
        }
        int earned;

        // the end-of-match screen is KryvonMatchSummary (uGUI, design system)
    }
}

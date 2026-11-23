// The player's wallet and progress, saved on the device (PlayerPrefs): coins, gems, parachute skins owned / equipped,
// match stats and mission rewards. In-game currency only – nothing here is bought with real money.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public static class KryvonWallet
    {
        public static int Coins { get => PlayerPrefs.GetInt("w.coins", 12450); set { PlayerPrefs.SetInt("w.coins", Mathf.Max(0, value)); PlayerPrefs.Save(); } }
        public static int Gems { get => PlayerPrefs.GetInt("w.gems", 620); set { PlayerPrefs.SetInt("w.gems", Mathf.Max(0, value)); PlayerPrefs.Save(); } }

        // ---- parachute skins: name, canopy colour A, colour B, price, paid in gems? ----
        public static readonly (string name, Color a, Color b, int price, bool gems)[] Chutes =
        {
            ("ROYAL PURPLE", new Color(0.36f, 0.16f, 0.62f), new Color(0.98f, 0.72f, 0.18f), 0, false),
            ("DESERT SAND",  new Color(0.82f, 0.66f, 0.42f), new Color(0.55f, 0.38f, 0.2f), 800, false),
            ("OLIVE GROVE",  new Color(0.33f, 0.4f, 0.2f),   new Color(0.85f, 0.82f, 0.6f), 1200, false),
            ("PETRA ROSE",   new Color(0.78f, 0.42f, 0.36f), new Color(0.96f, 0.85f, 0.72f), 2000, false),
            ("NIGHT RAID",   new Color(0.08f, 0.09f, 0.11f), new Color(0.3f, 0.32f, 0.36f), 60, true),
            ("GOLDEN EAGLE", new Color(0.95f, 0.7f, 0.12f),  new Color(0.12f, 0.1f, 0.08f), 150, true),
        };
        public static bool Owns(int chute) => chute == 0 || PlayerPrefs.GetInt("w.chute." + chute, 0) == 1;
        public static int Equipped { get => Mathf.Clamp(PlayerPrefs.GetInt("w.chute", 0), 0, Chutes.Length - 1); set { PlayerPrefs.SetInt("w.chute", value); PlayerPrefs.Save(); } }
        public static bool Buy(int chute)
        {
            if (Owns(chute)) return true;
            var c = Chutes[chute];
            if (c.gems) { if (Gems < c.price) return false; Gems -= c.price; } else { if (Coins < c.price) return false; Coins -= c.price; }
            PlayerPrefs.SetInt("w.chute." + chute, 1); Equipped = chute; return true;
        }

        // ---- outfits (skins for Hamza) ----
        public static readonly (string id, string name, string rarity, int price, bool gems)[] Outfits =
        {
            ("mythic", "MYTHIC RUNE", "LEGENDARY", 300, true),
        };
        public static int OutfitIndex(string id) => System.Array.FindIndex(Outfits, o => o.id == id);
        public static bool OutfitUnlocked(string id) => OutfitIndex(id) < 0 || PlayerPrefs.GetInt("w.outfit." + id, 0) == 1;
        public static bool BuyOutfit(int i)
        {
            var o = Outfits[i]; if (OutfitUnlocked(o.id)) return true;
            if (o.gems) { if (Gems < o.price) return false; Gems -= o.price; } else { if (Coins < o.price) return false; Coins -= o.price; }
            PlayerPrefs.SetInt("w.outfit." + o.id, 1); PlayerPrefs.Save(); return true;
        }

        // ---- stats + missions ----
        public static int Stat(string k) => PlayerPrefs.GetInt("s." + k, 0);
        public static void Add(string k, int n = 1) { PlayerPrefs.SetInt("s." + k, Stat(k) + n); PlayerPrefs.Save(); }
        /// counted during the fight without writing to disk each time (saved with the match result)
        public static void Count(string k, int n = 1) => PlayerPrefs.SetInt("s." + k, Stat(k) + n);

        public static readonly (string title, string stat, int goal, int coins, int gems)[] Missions =
        {
            ("Play a match",                   "matches",  1,  500, 0),
            ("Land with your parachute",       "landings", 1,  300, 0),
            ("Pick up 20 items",               "loot",     20, 600, 0),
            ("Take down 3 enemies",            "kills",    3,  900, 10),
            ("Play 5 matches",                 "matches",  5, 1500, 25),
            ("Pick up 100 items",              "loot",     100, 2000, 30),
        };
        public static bool Claimed(int m) => PlayerPrefs.GetInt("m.claimed." + m, 0) == 1;
        public static bool Claim(int m)
        {
            var x = Missions[m]; if (Claimed(m) || Stat(x.stat) < x.goal) return false;
            PlayerPrefs.SetInt("m.claimed." + m, 1); Coins += x.coins; Gems += x.gems; return true;
        }

        // kills: listen to every Health in a match
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded += (_, _) => Hook(); Hook(); }
        static void Hook()
        {
            foreach (var h in Object.FindObjectsByType<Health>())
                if (!h.CompareTag("Player")) h.OnDied += (v, info) => { if (info.Source && info.Source.CompareTag("Player")) Add("kills"); };
        }
    }
}

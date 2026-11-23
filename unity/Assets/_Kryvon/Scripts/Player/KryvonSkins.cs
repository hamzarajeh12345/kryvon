// Cosmetics: weapon skins (one per weapon, with a free look shared by every gun, store designs, an evolving M4A1 skin
// and a premium-pass AKM skin), parachutes, backpacks, helmets and vehicle skins. A skin is applied to the LIVE model
// with a MaterialPropertyBlock (texture, tint, metal, gloss, glow), so the mesh and its normal map stay and nothing is
// reloaded. Ownership / equipped / level are saved in PlayerPrefs (cos.own.*, cos.eq.*, cos.lv.*).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public enum CosKind { Weapon, Parachute, Backpack, Helmet, Vehicle, Jet, Emote }

    public class Cos
    {
        public string id, name, rarity = "CLASSIC", target = "*", tex, desc = "", source = "store";   // source: free · store · pass · evolve
        public CosKind kind; public int price; public bool gems;
        public Color tint = Color.white, glow = Color.clear; public float metal = 0.2f, smooth = 0.4f, tile = 3f;
    }

    public static class KryvonSkins
    {
        // every gun in the arsenal: weapon id → display name (WeaponData assets are not in Resources)
        public static readonly (string id, string name, string model)[] Guns =
        {
            ("saqr4", "M4A1", "w_ar"), ("barq762", "AKM", "w_ak"), ("nimr47", "AK-47", "w_ak"), ("shahin", "HK416", "w_bullpup"), ("burj16", "M16A4", "w_ar"), ("jabal36", "G36C", "w_ar"), ("zawbaa", "AK-12", "w_bullpup2"),
            ("zalzal45", "UMP45", "w_smg"), ("asifav", "Vector", "w_vector"), ("asifa", "MP5", "w_uzi"), ("tabl", "Thompson", "w_drum"),
            ("malik300", "AWM .300", "w_bigsniper"), ("qannas24", "M24", "w_sniper"), ("qannas98", "Kar98k", "w_sniper"), ("rami14", "M14 EBR", "w_dmr"), ("ramiskr", "SKS", "w_dmr"), ("sahm14", "Mini-14", "w_dmr"),
            ("raad97", "M1897", "w_shotgun"), ("raads12", "Saiga-12", "w_autoshot"), ("raaddb", "Double Barrel", "w_dbshot"), ("jabal249", "M249", "w_lmg"), ("sahn28", "DP-28", "w_dp"), ("saif19", "Glock 19", "w_pistol"),
        };
        public static string GunName(string id) { foreach (var g in Guns) if (g.id == id) return g.name; return id; }
        public static string GunModel(string id) { foreach (var g in Guns) if (g.id == id) return g.model; return "w_ar"; }

        public static readonly List<Cos> All = Build();
        static List<Cos> Build()
        {
            var l = new List<Cos>();
            foreach (var (gid, gname, _) in Guns)
            {
                // the free look: the same "Sand Raider" finish on every gun
                l.Add(new Cos { id = "free_" + gid, kind = CosKind.Weapon, target = gid, name = gname + " · SAND RAIDER", source = "free", tex = "desert", desc = "Free for everyone – the same desert finish on every gun." });
                l.Add(new Cos { id = "wood_" + gid, kind = CosKind.Weapon, target = gid, name = gname + " · WOODLAND", rarity = "RARE", price = 900, tex = "woodland" });
                l.Add(new Cos { id = "urban_" + gid, kind = CosKind.Weapon, target = gid, name = gname + " · URBAN", rarity = "RARE", price = 900, tex = "urban" });
                l.Add(new Cos { id = "digi_" + gid, kind = CosKind.Weapon, target = gid, name = gname + " · NIGHT DIGITAL", rarity = "EPIC", price = 1600, tex = "digital", metal = 0.4f, smooth = 0.55f });
                l.Add(new Cos { id = "crim_" + gid, kind = CosKind.Weapon, target = gid, name = gname + " · CRIMSON", rarity = "EPIC", price = 80, gems = true, tex = "crimson", smooth = 0.6f });
                l.Add(new Cos { id = "skull_" + gid, kind = CosKind.Weapon, target = gid, name = gname + " · JOLLY ROGER", rarity = "LEGENDARY", price = 160, gems = true, tex = "skull", smooth = 0.5f, tile = 2f });
            }
            l.Add(new Cos { id = "evo_m4", kind = CosKind.Weapon, target = "saqr4", name = "M4A1 · GLACIER", rarity = "MYTHIC", price = 240, gems = true, source = "evolve", tex = "digital", tint = new Color(0.72f, 0.86f, 1f), metal = 0.5f, smooth = 0.7f,
                desc = "Evolving skin, 7 levels: ice camo → polished steel → frost glow → crystal → blue storm → frozen gold → ABSOLUTE ZERO (ice burst on every kill)." });
            l.Add(new Cos { id = "pass_akm", kind = CosKind.Weapon, target = "barq762", name = "AKM · DAMASCUS SULTAN", rarity = "LEGENDARY", source = "pass", tex = "damascus", tint = new Color(1f, 0.86f, 0.62f), metal = 1f, smooth = 0.85f, glow = new Color(1f, 0.55f, 0.15f) * 0.6f,
                desc = "Nashama Pass premium reward (level 30): Damascus steel with gold inlay." });

            void Set(CosKind k, string pre, string what)
            {
                l.Add(new Cos { id = pre + "_classic", kind = k, name = what + " · CLASSIC", source = "free", tex = null, desc = "The standard issue." });
                l.Add(new Cos { id = pre + "_desert", kind = k, name = what + " · DESERT", rarity = "RARE", price = 700, tex = "desert" });
                l.Add(new Cos { id = pre + "_urban", kind = k, name = what + " · URBAN", rarity = "RARE", price = 700, tex = "urban" });
                l.Add(new Cos { id = pre + "_crimson", kind = k, name = what + " · CRIMSON", rarity = "EPIC", price = 60, gems = true, tex = "crimson" });
                l.Add(new Cos { id = pre + "_skull", kind = k, name = what + " · JOLLY ROGER", rarity = "LEGENDARY", price = 120, gems = true, tex = "skull", tile = 2f });
                l.Add(new Cos { id = pre + "_gold", kind = k, name = what + " · ROYAL GOLD", rarity = "LEGENDARY", price = 180, gems = true, tex = "skull_gold", metal = 0.9f, smooth = 0.8f, tile = 2f });
            }
            Set(CosKind.Parachute, "chute", "PARACHUTE"); Set(CosKind.Backpack, "bag", "BACKPACK"); Set(CosKind.Helmet, "helmet", "HELMET"); Set(CosKind.Vehicle, "car", "VEHICLE");
            // NASHAMA PASS (100 levels): Shaheen drop-jet liveries, desert camos for the main guns, emotes from home
            l.Add(new Cos { id = "jet_classic", kind = CosKind.Jet, name = "SHAHEEN · AIR FORCE GREY", source = "free", desc = "The drop jet in standard air-force grey." });
            l.Add(new Cos { id = "jet_desert", kind = CosKind.Jet, name = "SHAHEEN · DESERT FALCON", rarity = "RARE", source = "pass", tex = "desert", tile = 1.5f, desc = "Sand and dune-brown splinter camo." });
            l.Add(new Cos { id = "jet_night", kind = CosKind.Jet, name = "SHAHEEN · NIGHT HAWK", rarity = "EPIC", source = "pass", tex = "digital", tint = new Color(0.45f, 0.5f, 0.6f), metal = 0.5f, smooth = 0.6f, tile = 1.5f, desc = "Dark digital camo for night drops." });
            l.Add(new Cos { id = "jet_petra", kind = CosKind.Jet, name = "SHAHEEN · PETRA ROSE", rarity = "EPIC", source = "pass", tex = "desert", tint = new Color(1f, 0.68f, 0.6f), smooth = 0.6f, tile = 1.2f, desc = "Rose-red sandstone of the Treasury." });
            l.Add(new Cos { id = "jet_gold", kind = CosKind.Jet, name = "SHAHEEN · ROYAL GOLD", rarity = "LEGENDARY", source = "pass", tex = "gold", metal = 1f, smooth = 0.85f, glow = new Color(1f, 0.6f, 0.2f) * 0.25f, tile = 1f, desc = "Polished gold with an amber glow." });
            foreach (var gid in new[] { "saqr4", "barq762", "nimr47", "shahin", "zalzal45", "qannas98", "rami14", "jabal249", "raads12", "malik300" })
                l.Add(new Cos { id = "dune_" + gid, kind = CosKind.Weapon, target = gid, name = GunName(gid) + " · WADI RUM DUNE", rarity = "EPIC", source = "pass", tex = "desert", tint = new Color(1f, 0.8f, 0.6f), smooth = 0.5f, tile = 2.5f, desc = "Nashama Pass: red sand of Wadi Rum." });
            l.Add(new Cos { id = "emote_salute", kind = CosKind.Emote, name = "EMOTE · SALUTE", source = "free", desc = "B (or the ☺ button) in a match." });
            l.Add(new Cos { id = "emote_dabke", kind = CosKind.Emote, name = "EMOTE · DABKE", rarity = "EPIC", source = "pass", desc = "The Levant line dance: stomp, kick, stomp." });
            l.Add(new Cos { id = "emote_ardah", kind = CosKind.Emote, name = "EMOTE · SWORD DANCE", rarity = "LEGENDARY", source = "pass", desc = "Ardah: the sword raised high, swaying to the drum." });
            l.Add(new Cos { id = "emote_dallah", kind = CosKind.Emote, name = "EMOTE · ARABIC COFFEE", rarity = "RARE", source = "pass", desc = "Pour a cup from the dallah and offer it." });
            l.Add(new Cos { id = "emote_victory", kind = CosKind.Emote, name = "EMOTE · NASHAMA CHEER", rarity = "RARE", source = "pass", desc = "Both arms up – the crowd goes wild." });
            l.Find(c => c.id == "chute_classic").name = "PARACHUTE · JORDAN";
            l.Find(c => c.id == "chute_classic").desc = "Digital camo and olive cells with the Jordan flag on top.";
            return l;
        }
        public static Cos Get(string id) => All.Find(c => c.id == id);

        // ---- ownership, equipping, levels ----
        public static bool Owned(Cos c) => c != null && (c.source == "free" || PlayerPrefs.GetInt("cos.own." + c.id, 0) == 1);
        public static void Grant(string id) { PlayerPrefs.SetInt("cos.own." + id, 1); PlayerPrefs.Save(); }
        public static bool Buy(Cos c)
        {
            if (Owned(c)) return true; if (c.source == "pass") return false;
            if (c.gems) { if (KryvonWallet.Gems < c.price) return false; KryvonWallet.Gems -= c.price; } else { if (KryvonWallet.Coins < c.price) return false; KryvonWallet.Coins -= c.price; }
            Grant(c.id); Equip(c); return true;
        }
        static string EqKey(CosKind k, string target) => $"cos.eq.{k}.{target}";
        public static Cos Equipped(CosKind k, string target = "*")
        {
            var id = PlayerPrefs.GetString(EqKey(k, target), "");
            var c = Get(id); if (c != null && Owned(c)) return c;
            return k == CosKind.Weapon ? Get("free_" + target) : Get(DefaultPrefix(k) + "_classic");
        }
        static string DefaultPrefix(CosKind k) => k == CosKind.Jet ? "jet" : k == CosKind.Emote ? "emote" : k == CosKind.Parachute ? "chute" : k == CosKind.Backpack ? "bag" : k == CosKind.Helmet ? "helmet" : "car";
        public static void Equip(Cos c) { PlayerPrefs.SetString(EqKey(c.kind, c.target), c.id); PlayerPrefs.Save(); ApplyAll(); Changed?.Invoke(); }
        public const int MaxLevel = 7;
        public static readonly string[] GlacierLevels = { "ICE CAMO", "POLISHED STEEL", "FROST GLOW", "CRYSTAL", "BLUE STORM", "FROZEN GOLD", "ABSOLUTE ZERO" };
        public static int Level(Cos c) => c.source == "evolve" ? Mathf.Clamp(PlayerPrefs.GetInt("cos.lv." + c.id, 1), 1, MaxLevel) : 1;
        public const int UpgradeCost = 1500;   // coins per evolution step
        public static bool Upgrade(Cos c)
        {
            if (!Owned(c) || Level(c) >= MaxLevel || KryvonWallet.Coins < UpgradeCost) return false;
            KryvonWallet.Coins -= UpgradeCost; PlayerPrefs.SetInt("cos.lv." + c.id, Level(c) + 1); PlayerPrefs.Save(); ApplyAll(); Changed?.Invoke(); return true;
        }
        public static event System.Action Changed;
        /// the GLACIER kill effect: true when the player's gun in hand is the M4A1 with GLACIER at level 7
        public static bool AbsoluteZero(WeaponController wc)
        {
            if (!wc || wc.Current == null || wc.Current.weaponId != "saqr4") return false;
            var c = Equipped(CosKind.Weapon, "saqr4"); return c != null && c.source == "evolve" && Level(c) >= MaxLevel;
        }

        // ---- applying ----
        static readonly string[] TexProps = { "baseColorTexture", "_BaseMap" }, ColProps = { "baseColorFactor", "_BaseColor" };
        static MaterialPropertyBlock mpb;
        public static Texture2D Tex(string name) => string.IsNullOrEmpty(name) ? null : Resources.Load<Texture2D>("Skins/" + name);
        /// paints every renderer under root with the cosmetic (null = the model's own finish)
        public static void Apply(GameObject root, Cos c) => Apply(root, c, 0);
        /// levelOverride > 0 shows that evolution level (store preview of levels you don't own yet)
        public static void Apply(GameObject root, Cos c, int levelOverride)
        {
            if (!root) return; mpb ??= new MaterialPropertyBlock();
            int lv = c != null ? (levelOverride > 0 ? Mathf.Clamp(levelOverride, 1, MaxLevel) : Level(c)) : 1;
            string texName = c?.tex; var tint = c?.tint ?? Color.white; float metal = c?.metal ?? 0, smooth = c?.smooth ?? 0; var glow = c?.glow ?? Color.clear;
            if (c != null && c.source == "evolve")
            {   // GLACIER: 1 ice camo · 2 polished steel · 3 frost glow · 4 crystal · 5 blue storm · 6 frozen gold · 7 ABSOLUTE ZERO
                if (lv >= 2) { metal = 0.9f; smooth = 0.85f; }
                if (lv >= 3) glow = new Color(0.3f, 0.7f, 1f) * 0.5f;
                if (lv >= 4) { tint = new Color(0.82f, 0.95f, 1f); metal = 1f; smooth = 0.95f; }
                if (lv >= 5) glow = new Color(0.3f, 0.7f, 1f) * 1.0f;
                if (lv >= 6) { texName = "gold"; tint = new Color(1f, 0.95f, 0.85f); glow = new Color(0.45f, 0.8f, 1f) * 1.1f; }
                if (lv >= 7) glow = new Color(0.55f, 0.9f, 1f) * 1.8f;
            }
            var tex = Tex(texName);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                // glass, tyres, lights and the people inside keep their own look
                if (r.GetComponentInParent<Health>() || r.GetComponentInParent<Animator>()) continue;
                var mn = r.sharedMaterial ? r.sharedMaterial.name.ToLowerInvariant() : "";
                if (mn.Contains("glass") || mn.Contains("tyre") || mn.Contains("tire") || mn.Contains("wheel") || mn.Contains("rubber") || mn.Contains("light") || r.name.ToLowerInvariant().Contains("wheel")) continue;
                if (c == null || (tex == null && glow == Color.clear && tint == Color.white)) { r.SetPropertyBlock(null); continue; }
                mpb.Clear();
                if (tex) foreach (var p in TexProps) { mpb.SetTexture(p, tex); mpb.SetVector(p + "_ST", new Vector4(c.tile, c.tile, 0, 0)); }
                foreach (var p in ColProps) mpb.SetColor(p, tint);
                mpb.SetFloat("metallicFactor", metal); mpb.SetFloat("_Metallic", metal);
                mpb.SetFloat("roughnessFactor", 1f - smooth); mpb.SetFloat("_Smoothness", smooth);
                if (glow != Color.clear) { mpb.SetColor("emissiveFactor", glow); mpb.SetColor("_EmissionColor", glow); }
                r.SetPropertyBlock(mpb);
            }
        }

        /// the player's held guns (each Gun_<model> gets the skin of the weapon currently in hand)
        public static void ApplyAll()
        {
            foreach (var wc in Object.FindObjectsByType<WeaponController>(FindObjectsInactive.Include)) ApplyHeld(wc);
        }
        public static void ApplyHeld(WeaponController wc)
        {
            if (!wc || wc.GetComponent<KryvonBot>()) return;
            var d = wc.Current; var c = d ? Equipped(CosKind.Weapon, d.weaponId) : null;
            foreach (var t in wc.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Gun_")) Apply(t.gameObject, c);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { ApplyAll(); SceneManager.sceneLoaded += (_, _) => ApplyAll(); }
    }
}

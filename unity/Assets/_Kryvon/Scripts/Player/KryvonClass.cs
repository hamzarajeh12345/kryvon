// Operator classes (design doc: squad synergy). The hero picked in the lobby brings one passive skill into the match:
//   SNIPER   (Sara)       steadier aim: 25 % less spread while aiming, 15 % less recoil kick
//   ASSAULT  (Hamza)      fast hands: reloads 20 % faster
//   RECON    (Rasida)     recon scan: every 20 s enemies within 70 m flash on the minimap; 5 % faster on foot
//   MEDIC    (Hakeem)     field medic: heals and boosts are used 35 % faster
//   ENGINEER (Muhandisa)  mechanic: the car you drive takes 30 % less damage and repairs itself slowly
//   HEAVY    (Dir')       heavy armour: 12 % less damage from guns and blasts, 5 % slower
using UnityEngine;

namespace Kryvon
{
    public enum OperatorClass { Assault, Sniper, Recon, Medic, Engineer, Heavy }

    public static class KryvonClass
    {
        public static OperatorClass Of(string heroId) => heroId switch
        {
            "sara" => OperatorClass.Sniper, "recon" => OperatorClass.Recon, "medic" => OperatorClass.Medic,
            "engineer" => OperatorClass.Engineer, "heavy" => OperatorClass.Heavy, _ => OperatorClass.Assault,
        };
        /// the local player's class (the hero chosen in the lobby; outfits of Hamza count as Assault)
        public static OperatorClass Current => Of(PlayerPrefs.GetString(KryvonHeroSwitch.Pref, "hamza"));
        public static bool Is(OperatorClass c) => Current == c;

        public static string Perk(OperatorClass c) => c switch
        {
            OperatorClass.Sniper => "STEADY AIM  ·  25% less spread when aiming",
            OperatorClass.Recon => "RECON SCAN  ·  enemies within 70 m show on the map every 20 s",
            OperatorClass.Medic => "FIELD MEDIC  ·  heals and boosts 35% faster",
            OperatorClass.Engineer => "MECHANIC  ·  your car takes 30% less damage and self-repairs",
            OperatorClass.Heavy => "HEAVY ARMOUR  ·  12% less damage, a little slower",
            _ => "FAST HANDS  ·  reloads 20% faster",
        };

        // ---- the numbers the game systems read ----
        public static float AimSpreadMul => Is(OperatorClass.Sniper) ? 0.75f : 1f;
        public static float RecoilMul => Is(OperatorClass.Sniper) ? 0.85f : 1f;
        public static float ReloadMul => Is(OperatorClass.Assault) ? 0.8f : 1f;
        public static float HealTimeMul => Is(OperatorClass.Medic) ? 0.65f : 1f;
        public static float DamageTakenMul => Is(OperatorClass.Heavy) ? 0.88f : 1f;
        public static float MoveMul => Is(OperatorClass.Recon) ? 1.05f : Is(OperatorClass.Heavy) ? 0.95f : 1f;
        public static float VehicleDamageMul => Is(OperatorClass.Engineer) ? 0.7f : 1f;
    }
}

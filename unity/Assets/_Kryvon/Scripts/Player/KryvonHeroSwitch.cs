// Keeps only the hero picked in the lobby (PlayerPrefs "kryvon.hero") active under the player.
using UnityEngine;

namespace Kryvon
{
    [DefaultExecutionOrder(-200)]   // before the animator bridge and death scripts look for an Animator
    public class KryvonHeroSwitch : MonoBehaviour
    {
        public static readonly string[] Heroes = { "sara", "hamza", "mythic", "recon", "medic", "engineer", "heavy" };
        public const string Pref = "kryvon.hero";
        [SerializeField] GameObject[] models;

        void Awake()
        {
            var want = PlayerPrefs.GetString(Pref, Heroes[0]);
            if (System.Array.IndexOf(Heroes, want) < 0 || !KryvonWallet.OutfitUnlocked(want)) want = "hamza";   // retired outfits fall back to Hamza
            bool any = false;
            foreach (var m in models) if (m) { bool on = m.name == want; m.SetActive(on); any |= on; }
            if (!any && models.Length > 0 && models[0]) models[0].SetActive(true);
        }
    }
}

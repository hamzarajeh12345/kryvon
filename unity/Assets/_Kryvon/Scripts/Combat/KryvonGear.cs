// What the player carries besides weapons: backpack level (shown on the back), drinks, syringes, grenades.
using UnityEngine;

namespace Kryvon
{
    public class KryvonGear : MonoBehaviour
    {
        public int backpack, drinks, syringes, grenades, flares;
        [SerializeField] GameObject[] backpackModels;   // one per level (1..3), on the character's back

        public void SetBackpack(int level)
        {
            backpack = level;
            if (backpackModels == null) return;
            for (int i = 0; i < backpackModels.Length; i++) if (backpackModels[i]) backpackModels[i].SetActive(i == level - 1);
        }
    }
}

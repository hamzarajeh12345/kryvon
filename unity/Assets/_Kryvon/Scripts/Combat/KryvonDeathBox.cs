// The crate on a grave: holds a dead player's items. Stand next to it, press F to open, click items to take them.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public class KryvonDeathBox : MonoBehaviour
    {
        public static readonly List<KryvonDeathBox> All = new();
        public string owner = "";
        readonly List<KryvonLoot> items = new();
        public IReadOnlyList<KryvonLoot> Items => items;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Fill(Transform template, string who)
        {
            owner = who;
            foreach (var l in items) if (l) Destroy(l.gameObject);
            items.Clear();
            foreach (Transform t in template)
            {
                var src = t.GetComponent<KryvonLoot>(); if (!src) continue;
                var go = new GameObject(t.name); go.transform.SetParent(transform, false); go.SetActive(false);   // data only
                var l = go.AddComponent<KryvonLoot>();
                l.kind = src.kind; l.weapon = src.weapon; l.ammoType = src.ammoType; l.amount = src.amount; l.level = src.level; l.label = src.label;
                items.Add(l);
            }
        }

        public bool Take(int i, GameObject player)
        {
            if (i < 0 || i >= items.Count) return false;
            var l = items[i]; if (!l.Apply(player)) return false;
            items.RemoveAt(i); Destroy(l.gameObject); return true;
        }
    }
}

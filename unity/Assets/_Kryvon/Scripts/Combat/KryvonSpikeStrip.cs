// Spike strip (trigger that blows the tyres of any vehicle that drives over it) and the deployable shield (steel cover with HP).
using UnityEngine;

namespace Kryvon
{
    public class KryvonSpikeStrip : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<KryvonSpikeStrip> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        public Bounds Area { get { var c = GetComponent<Collider>(); return c ? c.bounds : new Bounds(transform.position, new Vector3(4, 0.5f, 0.6f)); } }
    }
}

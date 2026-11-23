// One map, several regions: the region picked in the lobby ("kryvon.region") is where the player lands.
// Spawn points are empty objects named Spawn_0 … Spawn_4 placed by the scene builder.
using UnityEngine;

namespace Kryvon
{
    [DefaultExecutionOrder(-150)]
    public class KryvonLanding : MonoBehaviour
    {
        void Start()
        {
            int r = PlayerPrefs.GetInt("kryvon.region", 0);
            var sp = GameObject.Find("Spawn_" + r); if (!sp) return;
            var cc = GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            var p = sp.transform.position;
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * 0.1f;
            transform.SetPositionAndRotation(p, sp.transform.rotation);
            if (cc) cc.enabled = true;
        }
    }
}

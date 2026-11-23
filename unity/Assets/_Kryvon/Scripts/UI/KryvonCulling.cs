// Distance culling for the match map (the "LOD" item of the design doc, the cheap way):
// every small or medium prop gets a one-level LODGroup, so the GPU stops drawing it once it is only a few pixels tall
// on screen. Crates, rocks, signs, fences, street furniture vanish far away; houses and landmarks never do.
// The thresholds are multiplied by QualitySettings.lodBias, which KryvonGraphics sets per device (0.45 on SMOOTH … 2.2 on
// ULTRA), so weak phones drop props sooner and strong PCs keep them longer – no per-device tuning here.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    public static class KryvonCulling
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Run(SceneManager.GetActiveScene()); SceneManager.sceneLoaded += (s, _) => Run(s); }

        static void Run(Scene s)
        {
            if (s.name != "Play" && s.name != "Levant") return;
            int small = 0, medium = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.GetComponentInParent<LODGroup>() || r.GetComponent<LODGroup>()) continue;
                // moving or gameplay things keep their own rules
                if (r.GetComponentInParent<Health>() || r.GetComponentInParent<KryvonVehicle>() || r.GetComponentInParent<KryvonLoot>() || r.GetComponentInParent<Camera>()) continue;
                if (r.GetComponentInParent<Terrain>()) continue;
                float size = r.bounds.size.magnitude;
                float h;                                    // screen-height fraction below which the prop is not drawn
                if (size < 1.5f) { h = 0.012f; small++; }  // bottles, cans, small crates, stones
                else if (size < 5f) { h = 0.006f; medium++; } // crates, barrels, fences, signs, bushes
                else continue;                              // buildings, walls, big rocks: always drawn
                var g = r.gameObject.AddComponent<LODGroup>();
                g.SetLODs(new[] { new LOD(h, new Renderer[] { r }) });
                g.RecalculateBounds();
            }
            Debug.Log($"KRYVON culling: {small} small + {medium} medium props get distance culling");
        }
    }
}

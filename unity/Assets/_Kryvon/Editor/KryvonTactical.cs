// Editor setup for stealth and throwables: ghillie and cloak materials, throwable/deployable templates,
// and wiring on the player (with a starter kit so everything can be tried straight away).
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static partial class KryvonWorld
    {
        static Material GhillieMat()
        {
            var tex = Tex("T_Ghillie", 128, (x, y) =>
            {
                // vertical ragged strands: earth, grey rock and black, alpha-cut
                float col = Mathf.PerlinNoise(x * 0.35f, 3.1f);
                float strand = Mathf.PerlinNoise(x * 0.9f, y * 0.04f);
                float tip = 1f - y / 127f + (Mathf.PerlinNoise(x * 0.5f, 9) - 0.5f) * 0.6f;
                float a = strand > 0.42f && tip > 0.05f ? 1f : 0f;
                var c = col < 0.33f ? new Color(0.36f, 0.31f, 0.24f) : col < 0.66f ? new Color(0.45f, 0.44f, 0.41f) : new Color(0.1f, 0.1f, 0.1f);
                c *= 0.8f + 0.4f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f); c.a = a; return c;
            }, true);
            var path = $"{Root}/Materials/M_Ghillie.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0); m.SetFloat("_Smoothness", 0.05f);
            m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static Material CloakMat()
        {
            var path = $"{Root}/Materials/M_Cloak.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetColor("_BaseColor", new Color(0.55f, 0.7f, 0.8f, 0.12f)); m.SetFloat("_Smoothness", 1f); m.SetFloat("_Metallic", 0.4f);
            EditorUtility.SetDirty(m); return m;
        }

        static GameObject Template(string model, float size, char axis, Transform parent, string name)
        {
            var g = Spawn("Loot/" + model, size, axis, parent);
            if (g == null) { g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(g.GetComponent<Collider>()); g.transform.SetParent(parent, false); g.transform.localScale = Vector3.one * size; }
            g.name = name; g.SetActive(false); return g;
        }

        public static void SetupTactical(GameObject player, System.Func<string, AudioClip> A)
        {
            rng ??= new System.Random(5);
            var holder = new GameObject("TacticalTemplates").transform; holder.SetParent(player.transform, false); holder.localPosition = Vector3.down * 50;
            var st = player.AddComponent<KryvonStealth>();
            var so = new SerializedObject(st);
            so.FindProperty("ghillieMat").objectReferenceValue = GhillieMat(); so.FindProperty("cloakMat").objectReferenceValue = CloakMat();
            so.FindProperty("cloakHum").objectReferenceValue = A("ring_ears"); so.FindProperty("cloakBreak").objectReferenceValue = K("impactGlass_heavy_001");
            so.FindProperty("mudSound").objectReferenceValue = K("footstep_grass_000");
            so.ApplyModifiedPropertiesWithoutUndo();

            var th = player.AddComponent<KryvonThrowables>();
            var t = new SerializedObject(th);
            var tp = t.FindProperty("templates"); tp.arraySize = 5;
            tp.GetArrayElementAtIndex(0).objectReferenceValue = Template("l_grenade", 0.13f, 'y', holder, "Frag");
            tp.GetArrayElementAtIndex(1).objectReferenceValue = Template("l_molotov", 0.3f, 'y', holder, "Molotov");
            tp.GetArrayElementAtIndex(2).objectReferenceValue = Template("l_smoke", 0.16f, 'y', holder, "Smoke");
            tp.GetArrayElementAtIndex(3).objectReferenceValue = Template("l_flash", 0.14f, 'y', holder, "Flashbang");
            tp.GetArrayElementAtIndex(4).objectReferenceValue = Template("l_flaregun", 0.3f, 'x', holder, "Flare");
            // spike strip: unfolded strip lying across the road, trigger collider
            var spike = Template("l_spikes", 4f, 'x', holder, "SpikeStrip");
            var sc = spike.AddComponent<BoxCollider>(); sc.isTrigger = true; sc.center = new Vector3(0, 0.15f, 0); sc.size = new Vector3(4f, 0.4f, 0.7f) / Mathf.Max(0.001f, spike.transform.lossyScale.x);
            spike.AddComponent<KryvonSpikeStrip>();
            // deployable shield: steel wall you can hide behind
            var shield = Template("l_shield", 1.3f, 'y', holder, "Shield");
            Collide(shield);
            var sw = shield.AddComponent<KryvonShieldWall>(); var swo = new SerializedObject(sw); swo.FindProperty("hitSound").objectReferenceValue = K("impactMetal_heavy_000"); swo.ApplyModifiedPropertiesWithoutUndo();
            t.FindProperty("spikeTemplate").objectReferenceValue = spike; t.FindProperty("shieldTemplate").objectReferenceValue = shield;
            t.FindProperty("pinSound").objectReferenceValue = K("impactMetal_heavy_001") ?? A("ui_dry_fire");
            t.FindProperty("throwSound").objectReferenceValue = K("impactSoft_heavy_000");
            t.FindProperty("boomSound").objectReferenceValue = A("boom_frag"); t.FindProperty("glassSound").objectReferenceValue = K("impactGlass_heavy_002");
            t.FindProperty("hissSound").objectReferenceValue = A("hiss_smoke"); t.FindProperty("bangSound").objectReferenceValue = A("bang_flash");
            t.FindProperty("ringSound").objectReferenceValue = A("ring_ears"); t.FindProperty("fireLoop").objectReferenceValue = A("fire_loop");
            // starter kit, so every tool can be tried right away
            var counts = t.FindProperty("counts"); counts.arraySize = 5;
            int[] kit = { 3, 2, 2, 2, 0 }; for (int i = 0; i < 5; i++) counts.GetArrayElementAtIndex(i).intValue = kit[i];
            t.FindProperty("spikes").intValue = 1; t.FindProperty("shields").intValue = 1;
            t.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

// Runtime materials kept in Resources so their shaders are always in the build (Shader.Find returns null for stripped shaders).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    [InitializeOnLoad]
    static class KryvonResources
    {
        static KryvonResources() { EditorApplication.delayCall += Ensure; }
        public static void Ensure()
        {
            const string dir = "Assets/_Kryvon/Resources";
            Directory.CreateDirectory(dir);
            if (!AssetDatabase.LoadAssetAtPath<Material>(dir + "/KryvonParticle.mat"))
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                AssetDatabase.CreateAsset(m, dir + "/KryvonParticle.mat");
            }
            if (!AssetDatabase.LoadAssetAtPath<Material>(dir + "/KryvonUnlit.mat"))
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Unlit")), dir + "/KryvonUnlit.mat");
            AssetDatabase.SaveAssets();
        }
    }
}

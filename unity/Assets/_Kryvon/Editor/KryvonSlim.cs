// Makes the builds small: glTFast keeps the textures embedded in the Hyper3D .glb models UNCOMPRESSED (≈21 MB each,
// 228 of them ≈ 4.8 GB). The images were extracted once to Assets/_Kryvon/Baked/Tex/<model>/<image>.png; here they get
// proper import settings (PC 2048 BC/DXT, phones 1024 ASTC) and every .glb material gets a twin in Baked/Mat that uses
// them. While a player is being built, every scene's renderers are switched to the twins, so the raw textures are
// never included. (The scenes on disk are not changed.)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kryvon.EditorTools
{
    public class KryvonSlim : IProcessSceneWithReport
    {
        const string Tex = "Assets/_Kryvon/Baked/Tex", Mat = "Assets/_Kryvon/Baked/Mat";
        public int callbackOrder => 100;

        static bool Linear(string n) { n = n.ToLower(); return n.Contains("normal") || n.Contains("metal") || n.Contains("rough") || n.Contains("occlusion") || n.Contains("orm"); }

        [MenuItem("KRYVON/Prepare compressed textures")]
        public static void Prepare()
        {
            if (!Directory.Exists(Tex)) { Debug.LogWarning("KRYVON: no extracted textures"); return; }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var f in Directory.GetFiles(Tex, "*.*", SearchOption.AllDirectories))
                {
                    if (f.EndsWith(".meta")) continue;
                    var p = f.Replace('\\', '/');
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) continue;
                    bool lin = Linear(Path.GetFileNameWithoutExtension(p)), dirty = false;
                    if (ti.sRGBTexture == lin) { ti.sRGBTexture = !lin; dirty = true; }
                    if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
                    if (!ti.mipmapEnabled || !ti.streamingMipmaps) { ti.mipmapEnabled = true; ti.streamingMipmaps = true; dirty = true; }
                    if (ti.maxTextureSize != 2048 || ti.textureCompression != TextureImporterCompression.Compressed) { ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
                    var a = ti.GetPlatformTextureSettings("Android");
                    if (!a.overridden || a.maxTextureSize != 1024 || a.format != TextureImporterFormat.ASTC_6x6)
                    { a.overridden = true; a.maxTextureSize = 1024; a.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(a); dirty = true; }
                    var i = ti.GetPlatformTextureSettings("iPhone");
                    if (!i.overridden || i.maxTextureSize != 1024) { i.overridden = true; i.maxTextureSize = 1024; i.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(i); dirty = true; }
                    if (dirty) ti.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            // material twins
            Directory.CreateDirectory(Mat);
            int made = 0;
            foreach (var glb in Directory.GetFiles("Assets/_Kryvon/Models", "*.glb", SearchOption.AllDirectories))
            {
                var gp = glb.Replace('\\', '/'); var model = Path.GetFileNameWithoutExtension(gp);
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(gp))
                {
                    if (o is not Material m) continue;
                    var path = $"{Mat}/{model}__{San(m.name)}.mat";
                    var twin = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (twin == null) { twin = new Material(m); AssetDatabase.CreateAsset(twin, path); made++; }
                    else twin.CopyPropertiesFromMaterial(m);
                    foreach (var prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop); if (t == null) continue;
                        if (!AssetDatabase.GetAssetPath(t).EndsWith(".glb")) continue;
                        var baked = Baked(model, t.name);
                        twin.SetTexture(prop, baked);     // null if missing: better than shipping the raw one
                        if (baked == null) Debug.LogWarning($"KRYVON: no baked texture for {model}/{t.name}");
                    }
                    EditorUtility.SetDirty(twin);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"KRYVON: compressed textures ready ({made} new materials)");
        }

        static string San(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }

        static Texture2D Baked(string model, string texName)
        {
            int k = texName.IndexOf("_sampler"); if (k > 0) texName = texName.Substring(0, k);
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{model}/{texName}{ext}"); if (t) return t;
            }
            return null;
        }

        static readonly Dictionary<Material, Material> cache = new();
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!BuildPipeline.isBuildingPlayer) return;
            int swapped = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; if (m == null) continue;
                        if (!cache.TryGetValue(m, out var twin))
                        {
                            twin = null; var p = AssetDatabase.GetAssetPath(m);
                            if (p.EndsWith(".glb")) twin = AssetDatabase.LoadAssetAtPath<Material>($"{Mat}/{Path.GetFileNameWithoutExtension(p)}__{San(m.name)}.mat");
                            cache[m] = twin;
                        }
                        if (twin) { mats[i] = twin; changed = true; swapped++; }
                    }
                    if (changed) r.sharedMaterials = mats;
                }
            Debug.Log($"KRYVON: {scene.name}: {swapped} materials switched to compressed textures");
        }
    }
}

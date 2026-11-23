// Bakes a top-down picture of the finished map (orthographic camera over the whole terrain, fog off) into
// Resources/UI/minimap.png, so the in-match minimap and full map show the real towns, roads and coast.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonMinimapBake
    {
        public static void Bake(float hx, float hz)
        {
            int W = 2048, H = Mathf.RoundToInt(2048 * hz / hx);
            var go = new GameObject("MinimapCam"); var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = hz; cam.aspect = hx / hz;
            go.transform.SetPositionAndRotation(new Vector3(0, 1500, 0), Quaternion.Euler(90, 0, 0));
            cam.nearClipPlane = 1; cam.farClipPlane = 3000; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.32f, 0.42f);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null; RenderSettings.fog = fog; cam.targetTexture = null;
            Object.DestroyImmediate(go); rt.Release();
            var path = "Assets/_Kryvon/Resources/UI/minimap.png";
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path); imp.maxTextureSize = 2048; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
            Debug.Log($"KRYVON: minimap baked {W}x{H}");
        }
    }
}

// Builds the AI opponents into Play.unity: one disabled template per squad character (recon, medic, engineer, heavy),
// animated, armed, with hit boxes, a grave with a lootable crate, and the KryvonBot brain; plus the match manager.
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonBotsBuild
    {
        const string Root = "Assets/_Kryvon";
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field); if (p == null) { Debug.LogWarning("KRYVON: no field " + field); return; }
            switch (value)
            {
                case Object[] arr: p.arraySize = arr.Length; for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i]; break;
                case Object o: p.objectReferenceValue = o; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Setup(AudioClip shot, Font font, AudioClip victory)
        {
            var holder = new GameObject("BotTemplates");
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var ch in new[] { "recon", "medic", "engineer", "heavy" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{ch}.glb");
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{Root}/Animations/{ch}.controller") ?? KryvonPlay.BuildController(ch);
                if (!asset) continue;
                var root = new GameObject("Bot_" + ch); root.transform.SetParent(holder.transform, false);
                var cc = root.AddComponent<CharacterController>(); cc.radius = 0.35f; cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.4f; cc.slopeLimit = 50f;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform); model.name = ch;
                Fit(model, 1.8f);
                var an = model.GetComponentInChildren<Animator>(); if (!an) an = model.transform.GetChild(0).gameObject.AddComponent<Animator>();
                an.runtimeAnimatorController = ctrl; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.CullCompletely;
                KryvonPlay.AttachGun(model, "w_ar", 0.95f);
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                var h = root.AddComponent<Health>();
                var hb = new GameObject("HitBody"); hb.transform.SetParent(root.transform, false);
                var cap = hb.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0, 0.85f, 0); cap.height = 1.65f; cap.radius = 0.3f; hb.AddComponent<HitBox>().owner = h;
                var hd = new GameObject("HitHead"); hd.transform.SetParent(root.transform, false); hd.transform.localPosition = new Vector3(0, 1.66f, 0);
                hd.AddComponent<SphereCollider>().radius = 0.14f; var hh = hd.AddComponent<HitBox>(); hh.owner = h; hh.isHead = true;
                KryvonWorld.MakeGrave(root, 0f);
                var bot = root.AddComponent<KryvonBot>(); Set(bot, "shotSound", shot);
                root.SetActive(false); list.Add(root);
            }
            var match = new GameObject("Match").AddComponent<KryvonBots>();
            Set(match, "templates", list.Select(g => (Object)g).ToArray());
            Set(match, "chuteTemplate", KryvonDropBuild.Chute); Set(match, "font", font);
            if (victory) Set(match, "victorySound", victory);
            Debug.Log($"KRYVON: {list.Count} bot templates ready");
        }

        static void Fit(GameObject go, float h)
        {
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.localScale = Vector3.one * (h / Mathf.Max(0.01f, b.size.y));
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var p = go.transform.parent; go.transform.position += new Vector3(p.position.x - b.center.x, p.position.y - b.min.y, p.position.z - b.center.z);
        }
    }
}

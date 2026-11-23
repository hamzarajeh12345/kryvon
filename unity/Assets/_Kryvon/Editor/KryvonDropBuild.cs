// Builds the start of the match into Play.unity: the waiting area (the forward base + old hill city, outside the
// south edge of the map), the drop jet template, the parachute template and the KryvonDrop component on the player.
using UnityEditor;
using UnityEngine;

namespace Kryvon.EditorTools
{
    public static class KryvonDropBuild
    {
        const string Root = "Assets/_Kryvon";
        public static Vector3 BaseOrigin = new(0, 0, -600);
        public static GameObject Chute;
        public static bool Levant; public static Vector2 MapHalf;

        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field); if (p == null) { Debug.LogWarning("KRYVON: no field " + field); return; }
            switch (value) { case Object o: p.objectReferenceValue = o; break; case float f: p.floatValue = f; break; }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Setup(GameObject player, System.Func<string, AudioClip> A, Font font)
        {
            // ---- waiting area ----
            var bse = KryvonWorld.BuildBase(BaseOrigin);
            bse.name = "WaitingArea"; bse.rotation = Quaternion.Euler(0, 180, 0);    // the base faces the map, the hill city is behind it
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "WaitingGround";
            ground.transform.position = BaseOrigin + new Vector3(0, -0.32f, -40); ground.transform.localScale = new Vector3(60, 1, 34);
            var sand = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/M_Sand.mat") ?? AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/M_BaseConcrete.mat");
            if (sand) ground.GetComponent<Renderer>().sharedMaterial = sand;
            var ws = new GameObject("WaitingSpawn"); ws.transform.SetPositionAndRotation(BaseOrigin + new Vector3(0, 0.2f, -2), Quaternion.Euler(0, 180, 0));

            // ---- drop jet ----
            var jetAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Vehicles/v_jet.glb");
            GameObject jet;
            if (jetAsset)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(jetAsset); m.transform.localScale = Vector3.one;
                var rs = m.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                m.transform.localScale = Vector3.one * (34f / Mathf.Max(0.01f, Mathf.Max(b.size.z, b.size.x)));     // a 34 m transport jet
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                jet = new GameObject("JetTemplate"); m.transform.SetParent(jet.transform, true); m.transform.position -= b.center;
            }
            else jet = new GameObject("JetTemplate");
            foreach (var c in jet.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            jet.SetActive(false);

            // ---- parachute: an arched ram-air canopy, purple and gold cells ----
            var chute = new GameObject("ChuteTemplate");
            var real = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Props/p_chute.glb");
            if (real)
            {   // the real canopy (digital camo + olive cells, Jordan flag on top), ~7.5 m wide, centred on the template
                var g = (GameObject)PrefabUtility.InstantiatePrefab(real); var rs = g.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.localScale = Vector3.one * (7.5f / Mathf.Max(b.size.x, b.size.z)); b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.z > b.size.x * 1.15f) g.transform.rotation = Quaternion.Euler(0, 90, 0);      // span left–right
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                g.transform.SetParent(chute.transform, true); g.transform.position -= b.center;
                foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            else
            {
                var mf = chute.AddComponent<MeshFilter>(); var mr = chute.AddComponent<MeshRenderer>();
                mf.sharedMesh = Canopy();
                mr.sharedMaterials = new[] { ChuteMat("M_ChutePurple", new Color(0.36f, 0.16f, 0.62f)), ChuteMat("M_ChuteGold", new Color(0.98f, 0.72f, 0.18f)) };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            chute.SetActive(false);
            Chute = chute;

            var d = player.AddComponent<KryvonDrop>();
            if (Levant) { Set(d, "mapHalfX", MapHalf.x); Set(d, "mapHalfZ", MapHalf.y); Set(d, "altitude", 430f); }
            Set(d, "jetTemplate", jet); Set(d, "chuteTemplate", chute); Set(d, "font", font);
            Set(d, "engineLoop", A("vehicle_engine")); Set(d, "windLoop", A("amb_wind"));
            var open = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/ThirdParty/Kenney/impactSoft_heavy_000.ogg"); if (open) Set(d, "chuteOpen", open);
        }

        static Material ChuteMat(string name, Color c)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.35f); m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;
            EditorUtility.SetDirty(m); return m;
        }

        static Mesh Canopy()
        {
            const int cells = 9, chordSegs = 6; const float span = 8.4f, chord = 3.2f, arch = 1.6f;
            var v = new System.Collections.Generic.List<Vector3>(); var n = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>();
            var tris = new[] { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };
            for (int c = 0; c < cells; c++)
            {
                int start = v.Count;
                for (int i = 0; i <= 2; i++)          // two strips per cell so the arch is smooth
                    for (int j = 0; j <= chordSegs; j++)
                    {
                        float s = (c + i / 2f) / cells, x = (s - 0.5f) * span;
                        float y = -arch * Mathf.Pow(2f * (s - 0.5f), 2);
                        float t = j / (float)chordSegs, z = (0.5f - t) * chord;
                        y += 0.28f * Mathf.Sin(t * Mathf.PI) * (1f - 0.4f * t);        // aerofoil hump, thicker at the front
                        v.Add(new Vector3(x, y, z)); n.Add(new Vector3(-x * 0.2f, 1, 0).normalized); uv.Add(new Vector2(s, t));
                    }
                var tl = tris[c % 2];
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < chordSegs; j++)
                    {
                        int a = start + i * (chordSegs + 1) + j, b = a + chordSegs + 1;
                        tl.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                    }
            }
            var mesh = new Mesh { name = "Canopy" }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2; mesh.SetTriangles(tris[0], 0); mesh.SetTriangles(tris[1], 1); mesh.RecalculateBounds();
            var path = $"{Root}/Materials/Canopy.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}

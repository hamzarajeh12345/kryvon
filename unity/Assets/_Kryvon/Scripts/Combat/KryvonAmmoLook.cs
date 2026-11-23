// Ammo on the ground looks like ammunition, not like a crate: a small loose pile of real rounds of that calibre,
// built from code once per calibre (shared meshes, two shared PBR materials):
//   5.56 mm  – slim bottle-neck brass case, copper bullet          7.62 mm – longer, wider case
//   9 mm     – short straight case, round nose                      .45 ACP – short and fat
//   12 gauge – red plastic hull with a brass head                    .300 Magnum – long belted case, pointed bullet
// The rounds lie on their side in a loose cluster (one or two standing), so the calibre reads at a glance. The old box
// model stays as the (invisible) pick-up collider.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public static class KryvonAmmoLook
    {
        struct Cal { public float caseR, caseL, neckR, neckL, bulletL; public bool shell, round; public int count; }
        static Cal Spec(AmmoType t) => t switch
        {
            AmmoType.A556 => new Cal { caseR = 0.0048f, caseL = 0.036f, neckR = 0.0032f, neckL = 0.006f, bulletL = 0.015f, count = 7 },
            AmmoType.A762 => new Cal { caseR = 0.0060f, caseL = 0.039f, neckR = 0.0043f, neckL = 0.007f, bulletL = 0.018f, count = 6 },
            AmmoType.A9mm => new Cal { caseR = 0.0050f, caseL = 0.019f, neckR = 0.0050f, neckL = 0f, bulletL = 0.009f, round = true, count = 8 },
            AmmoType.A45 => new Cal { caseR = 0.0060f, caseL = 0.023f, neckR = 0.0060f, neckL = 0f, bulletL = 0.010f, round = true, count = 7 },
            AmmoType.Gauge12 => new Cal { caseR = 0.0105f, caseL = 0.068f, neckR = 0.0105f, neckL = 0f, bulletL = 0f, shell = true, count = 5 },
            _ => new Cal { caseR = 0.0068f, caseL = 0.063f, neckR = 0.0045f, neckL = 0.008f, bulletL = 0.022f, count = 5 },   // .300 Magnum
        };

        static readonly Dictionary<AmmoType, (Mesh casing, Mesh tip)> meshes = new();
        static Material brass, copper, hull, lead;
        static Material Mat(Color c, float metal, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (!sh) return null;
            var m = new Material(sh); m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth); return m;
        }

        // ---------- meshes: lathe profiles (radius along the length), 14 sides ----------
        static Mesh Lathe(List<Vector2> prof, int sides = 14)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i < prof.Count; i++)
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    v.Add(dir * prof[i].x + Vector3.forward * prof[i].y);
                    // normal from the profile slope
                    var p0 = prof[Mathf.Max(0, i - 1)]; var p1 = prof[Mathf.Min(prof.Count - 1, i + 1)];
                    float dr = p1.x - p0.x, dz = p1.y - p0.y; var nn = (dir * dz - Vector3.forward * dr).normalized; if (nn == Vector3.zero) nn = dir; n.Add(nn);
                }
            int row = sides + 1;
            for (int i = 0; i < prof.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                    tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);    // clockwise from outside = front face in Unity
                }
            var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds(); return m;
        }
        static (Mesh, Mesh) Build(AmmoType t)
        {
            if (meshes.TryGetValue(t, out var mm)) return mm;
            var c = Spec(t);
            // casing: closed base, rim groove, body, shoulder, neck
            var cas = new List<Vector2> { new(0, 0), new(c.caseR * 1.02f, 0), new(c.caseR * 1.02f, c.caseL * 0.05f), new(c.caseR * 0.85f, c.caseL * 0.07f), new(c.caseR * 0.85f, c.caseL * 0.1f), new(c.caseR, c.caseL * 0.12f) };
            float body = c.caseL - c.neckL;
            if (c.neckL > 0) { cas.Add(new(c.caseR * 0.97f, body - c.caseL * 0.08f)); cas.Add(new(c.neckR, body)); cas.Add(new(c.neckR, c.caseL)); }
            else cas.Add(new(c.caseR, c.caseL));
            if (c.shell) { cas.Add(new(c.caseR * 0.9f, c.caseL)); cas.Add(new(0, c.caseL)); }      // crimped top of the shotgun hull
            var casing = Lathe(cas);
            // bullet: ogive (pointed) or round nose
            Mesh tip = null;
            if (!c.shell)
            {
                float r = c.neckR * 0.98f, L = c.bulletL; var b = new List<Vector2> { new(0, c.caseL - L * 0.25f), new(r, c.caseL - L * 0.25f), new(r, c.caseL + L * 0.15f) };
                int steps = 6;
                for (int i = 1; i <= steps; i++)
                {
                    float k = i / (float)steps;
                    float rr = c.round ? r * Mathf.Sqrt(Mathf.Max(0, 1 - k * k)) : r * Mathf.Pow(1 - k, 0.75f);
                    b.Add(new(rr, c.caseL + L * 0.15f + k * L * 0.85f));
                }
                tip = Lathe(b);
            }
            meshes[t] = (casing, tip); return meshes[t];
        }

        /// replace the crate look of an ammo pick-up by a pile of rounds of its calibre
        public static void Dress(KryvonLoot l)
        {
            if (!l || l.kind != LootKind.Ammo || l.transform.Find("Rounds")) return;
            brass ??= Mat(new Color(0.86f, 0.66f, 0.3f), 1f, 0.72f);
            copper ??= Mat(new Color(0.78f, 0.42f, 0.24f), 1f, 0.66f);
            lead ??= Mat(new Color(0.42f, 0.42f, 0.44f), 0.8f, 0.5f);
            hull ??= Mat(new Color(0.62f, 0.07f, 0.06f), 0f, 0.45f);
            if (!brass) return;                       // no lit shader in this build: keep the old look
            var (casing, tip) = Build(l.ammoType); var spec = Spec(l.ammoType);
            // hide the box, keep its collider for picking up
            foreach (var r in l.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var root = new GameObject("Rounds").transform; root.SetParent(l.transform, false);
            // find the floor under the pick-up (the box sat a little above it)
            var at = l.transform.position;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(at + Vector3.up * 0.3f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(l.transform) && h.distance < best) { best = h.distance; at = h.point; }
            root.position = at; root.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            // in-world scale: real rounds are tiny, so the pile is shown 2.2× bigger to read from a few metres away (like every BR)
            root.localScale = Vector3.one * 2.2f / Mathf.Max(0.0001f, l.transform.lossyScale.x);
            var rng = new System.Random(l.GetEntityId().GetHashCode());
            int n = spec.count;
            for (int i = 0; i < n; i++)
            {
                var r = new GameObject("Round").transform; r.SetParent(root, false);
                bool standing = i >= n - 2 && !spec.shell;
                float spread = spec.caseR * 2.4f;
                if (standing) { r.localPosition = new Vector3((i - n + 1.5f) * spread * 1.1f, 0, spec.caseL * 0.4f); r.localRotation = Quaternion.Euler(-90, 0, 0); }
                else
                {
                    r.localPosition = new Vector3((i - n * 0.5f) * spread + (float)rng.NextDouble() * spread * 0.4f, spec.caseR, (float)(rng.NextDouble() - 0.5) * spec.caseL * 0.5f);
                    r.localRotation = Quaternion.Euler(0, (float)(rng.NextDouble() - 0.5) * 30f + (i % 2 == 0 ? 0 : 180), 0);
                }
                var cm = r.gameObject.AddComponent<MeshFilter>(); cm.sharedMesh = casing;
                var cr = r.gameObject.AddComponent<MeshRenderer>(); cr.sharedMaterial = spec.shell ? hull : brass; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (spec.shell)
                {   // the brass head of the shotgun shell
                    var head = new GameObject("Head").transform; head.SetParent(r, false); head.localScale = new Vector3(1.04f, 1.04f, 0.28f);
                    head.gameObject.AddComponent<MeshFilter>().sharedMesh = casing; var hr = head.gameObject.AddComponent<MeshRenderer>(); hr.sharedMaterial = brass; hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if (tip)
                {
                    var t = new GameObject("Bullet").transform; t.SetParent(r, false);
                    t.gameObject.AddComponent<MeshFilter>().sharedMesh = tip; var tr = t.gameObject.AddComponent<MeshRenderer>();
                    tr.sharedMaterial = spec.round ? lead : copper; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }
    }
}

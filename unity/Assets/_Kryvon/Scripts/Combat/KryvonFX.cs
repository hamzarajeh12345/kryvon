// Runtime particle effects. Everything is built from code, with textures generated once at start-up:
//   puff  – a soft, noisy cloud (smoke, dust, fireball)
//   flame – a tall teardrop with a hot core and a ragged edge (fire tongues)
//   spark – a small hot dot (embers, sparks, debris)
// Fire and flashes use additive blending (they glow and add light); smoke and dust use normal alpha. Colour changes with
// age (white-yellow → orange → red → soot), particles grow, spin and drift with turbulence, and point lights flicker.
// (Before this the particles had no texture at all, so every flame and puff drew as a flat square.)
using UnityEngine;

namespace Kryvon
{
    public static class KryvonFX
    {
        // ---------- textures and materials ----------
        static Material baseMat, alphaMat, addMat, flameMat, sparkMat;
        static Material Base() => baseMat ? baseMat : baseMat = Resources.Load<Material>("KryvonParticle");
        static float Fbm(float x, float y) { float v = 0, a = 0.5f, f = 1; for (int i = 0; i < 4; i++) { v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i); a *= 0.5f; f *= 2.1f; } return v; }
        static Texture2D PuffTex()
        {
            const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1, r = Mathf.Sqrt(u * u + v * v);
                float n = Fbm(u * 2.4f + 5, v * 2.4f + 9);
                float a = Mathf.Clamp01(1f - r * (1.05f + (0.5f - n) * 0.9f)); a = a * a * (3 - 2 * a);
                float shade = Mathf.Lerp(0.72f, 1f, n);             // a little light and dark inside the cloud
                t.SetPixel(x, y, new Color(shade, shade, shade, a));
            }
            t.Apply(true); return t;
        }
        static Texture2D FlameTex()
        {
            const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S;          // v: 0 bottom → 1 tip
                float width = Mathf.Lerp(0.75f, 0.05f, Mathf.Pow(v, 0.8f)) * (0.85f + 0.3f * Fbm(v * 4f, 3.3f));
                float edge = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.01f, width));
                float n = Fbm(u * 3f + 2, v * 5f);
                float a = Mathf.Clamp01(edge * 1.6f - (1 - n) * 0.5f) * Mathf.Clamp01(v * 6f) * Mathf.Clamp01((1 - v) * 1.4f);
                float core = Mathf.Clamp01(edge * 1.3f - 0.4f);                     // hotter (whiter) middle
                t.SetPixel(x, y, new Color(1f, Mathf.Lerp(0.75f, 1f, core), Mathf.Lerp(0.5f, 1f, core), a));
            }
            t.Apply(true); return t;
        }
        static Texture2D SparkTex()
        {
            const int S = 32; var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1, r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(1f - r); a = Mathf.Pow(a, 2.2f); t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            t.Apply(true); return t;
        }
        static Material Make(Texture tex, bool additive)
        {
            var b = Base(); if (!b) return null;
            var m = new Material(b);
            m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
            if (additive) { m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); }
            else { m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); }
            m.SetFloat("_ZWrite", 0);
            m.renderQueue = 3000 + (additive ? 10 : 0);
            return m;
        }
        /// shared glow materials for world markers (loot rings and beams)
        public static Material GlowMat { get { Ensure(); return addMat; } }
        public static Material BeamMat { get { Ensure(); return sparkMat; } }
        public static Material SoftMat { get { Ensure(); return alphaMat; } }
        static Material ringMat;
        /// a thin glowing ring (store pedestal, markers)
        public static Material RingMat
        {
            get
            {
                if (ringMat) return ringMat; Ensure();
                const int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1, r = Mathf.Sqrt(u * u + v * v);
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.035f, 2f)), inner = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.012f, 2f)) * 0.45f, fill = Mathf.Clamp01(0.86f - r) * 0.12f;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(ring + inner + fill)));
                }
                t.Apply(true); ringMat = Make(t, true); return ringMat;
            }
        }
        /// slow sparkles drifting up around an object (max-level skins in the store)
        public static void Sparkle(Vector3 p, float radius, Color c, int layer)
        {
            Ensure();
            var ps = Emit(p, new P { burst = 10, duration = 0.05f, life0 = 0.8f, life1 = 1.6f, speed0 = 0.05f, speed1 = 0.25f, size0 = 0.015f, size1 = 0.04f, upward = 0.25f, noise = 0.2f, radius = radius,
                color = G((new Color(c.r, c.g, c.b, 0f), 0), (new Color(Mathf.Min(1, c.r + 0.3f), Mathf.Min(1, c.g + 0.3f), Mathf.Min(1, c.b + 0.3f), 1f), 0.2f), (new Color(c.r, c.g, c.b, 0f), 1)), mat = sparkMat });
            ps.gameObject.layer = layer;
        }
        static void Ensure()
        {
            if (alphaMat) return;
            var puff = PuffTex(); alphaMat = Make(puff, false); addMat = Make(puff, true);
            flameMat = Make(FlameTex(), true); sparkMat = Make(SparkTex(), true);
        }

        // ---------- one emitter ----------
        struct P
        {
            public int burst; public float rate, duration; public bool loop;
            public float life0, life1, speed0, speed1, size0, size1, grow, gravity, noise, radius, upward, spin;
            public Gradient color; public Material mat; public bool stretch, flatShape;
        }
        static Gradient G(params (Color c, float t)[] keys)
        {
            var g = new Gradient(); var ck = new GradientColorKey[keys.Length]; var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) { ck[i] = new GradientColorKey(keys[i].c, keys[i].t); ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t); }
            g.SetKeys(ck, ak); return g;
        }
        static ParticleSystem Emit(Vector3 at, P p, Transform parent = null)
        {
            Ensure();
            var g = new GameObject("FX"); g.transform.position = at; if (parent) g.transform.SetParent(parent, true);
            g.transform.rotation = Quaternion.Euler(-90, 0, 0);                 // cone/shape faces up
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = Mathf.Max(0.05f, p.duration); m.loop = p.loop;
            m.startLifetime = new ParticleSystem.MinMaxCurve(p.life0, p.life1); m.startSpeed = new ParticleSystem.MinMaxCurve(p.speed0, p.speed1);
            m.startSize = new ParticleSystem.MinMaxCurve(p.size0, p.size1); m.gravityModifier = p.gravity;
            m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); m.startColor = Color.white;
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.stopAction = p.loop ? ParticleSystemStopAction.None : ParticleSystemStopAction.Destroy;
            m.maxParticles = 600; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var e = ps.emission; e.rateOverTime = p.rate; if (p.burst > 0) e.SetBursts(new[] { new ParticleSystem.Burst(0, p.burst) });
            var sh = ps.shape; sh.enabled = true;
            if (p.flatShape) { sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = p.radius; sh.arc = 360; }
            else { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = Mathf.Max(0.01f, p.radius); }
            var col = ps.colorOverLifetime; col.enabled = true; col.color = p.color;
            if (p.grow > 0f && p.grow != 1f)
            {   // size over life: 1 → grow (fast at first, then easing out, like real smoke and fire)
                var sz = ps.sizeOverLifetime; sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(0.3f, 1f + (p.grow - 1f) * 0.6f), new Keyframe(1, p.grow)));
            }
            if (p.upward != 0) { var f = ps.forceOverLifetime; f.enabled = true; f.space = ParticleSystemSimulationSpace.World; f.y = new ParticleSystem.MinMaxCurve(p.upward); }
            if (p.noise > 0) { var n = ps.noise; n.enabled = true; n.strength = p.noise; n.frequency = 0.6f; n.scrollSpeed = 0.4f; n.quality = ParticleSystemNoiseQuality.Medium; }
            if (p.spin != 0) { var r = ps.rotationOverLifetime; r.enabled = true; r.z = new ParticleSystem.MinMaxCurve(-p.spin, p.spin); }
            var rend = g.GetComponent<ParticleSystemRenderer>(); rend.sharedMaterial = p.mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
            if (p.stretch) { rend.renderMode = ParticleSystemRenderMode.Stretch; rend.velocityScale = 0.06f; rend.lengthScale = 1.5f; }
            rend.sortingFudge = p.mat == alphaMat ? 10 : 0;                      // glow draws over the smoke it sits in
            ps.Play(); return ps;
        }
        static Light FlashLight(Vector3 at, Color c, float intensity, float range, float time)
        {
            var l = new GameObject("FXLight").AddComponent<Light>(); l.transform.position = at; l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.gameObject.AddComponent<FXLightFade>().Init(intensity, time); return l;
        }
        class FXLightFade : MonoBehaviour
        {
            float i0, t0, life; Light l;
            public void Init(float i, float t) { i0 = i; life = t; t0 = Time.time; l = GetComponent<Light>(); }
            void Update() { float k = (Time.time - t0) / life; if (k >= 1) { Destroy(gameObject); return; } l.intensity = i0 * (1 - k) * (1 - k); }
        }
        /// a light that flickers like fire until destroyed
        class Flicker : MonoBehaviour
        {
            public float baseI = 3f; Light l; float seed;
            void Awake() { l = GetComponent<Light>(); seed = Random.value * 100; }
            void Update() { if (l) l.intensity = baseI * (0.75f + 0.5f * Mathf.PerlinNoise(seed, Time.time * 9f)); }
        }

        // ---------- colour ramps ----------
        static Gradient FireRamp => G((new Color(1f, 0.95f, 0.75f, 0f), 0f), (new Color(1f, 0.82f, 0.4f, 0.95f), 0.08f), (new Color(1f, 0.45f, 0.1f, 0.85f), 0.4f), (new Color(0.7f, 0.15f, 0.04f, 0.4f), 0.75f), (new Color(0.2f, 0.05f, 0.02f, 0f), 1f));
        static Gradient SootRamp(float a = 0.75f) => G((new Color(0.25f, 0.22f, 0.2f, 0f), 0f), (new Color(0.16f, 0.14f, 0.13f, a), 0.15f), (new Color(0.22f, 0.21f, 0.2f, a * 0.6f), 0.6f), (new Color(0.3f, 0.3f, 0.3f, 0f), 1f));
        static Gradient SparkRamp => G((new Color(1f, 0.95f, 0.7f, 1f), 0f), (new Color(1f, 0.6f, 0.2f, 1f), 0.5f), (new Color(0.9f, 0.25f, 0.05f, 0f), 1f));

        // ---------- the effects ----------
        public static void Blood(Vector3 p, int n)
        {
            Ensure();
            Emit(p, new P { burst = n, duration = 0.1f, life0 = 0.35f, life1 = 0.7f, speed0 = 1.2f, speed1 = 3.2f, size0 = 0.05f, size1 = 0.13f, grow = 1f, gravity = 1.2f, radius = 0.1f, color = G((new Color(0.5f, 0.02f, 0.02f, 0.95f), 0), (new Color(0.35f, 0.01f, 0.01f, 0.9f), 0.6f), (new Color(0.3f, 0, 0, 0), 1)), mat = alphaMat });
            Emit(p, new P { burst = 3, duration = 0.1f, life0 = 0.25f, life1 = 0.45f, speed0 = 0.3f, speed1 = 0.8f, size0 = 0.25f, size1 = 0.45f, grow = 1.8f, radius = 0.05f, color = G((new Color(0.55f, 0.03f, 0.03f, 0.6f), 0), (new Color(0.4f, 0.02f, 0.02f, 0f), 1)), mat = alphaMat });
        }
        public static void Dust(Vector3 p)
        {
            Ensure();
            Emit(p, new P { burst = 14, duration = 0.1f, life0 = 1.2f, life1 = 2.2f, speed0 = 0.6f, speed1 = 2f, size0 = 0.5f, size1 = 1.1f, grow = 2.2f, gravity = -0.02f, noise = 0.3f, radius = 0.3f, spin = 0.5f, color = G((new Color(0.72f, 0.66f, 0.54f, 0f), 0), (new Color(0.72f, 0.66f, 0.54f, 0.55f), 0.1f), (new Color(0.75f, 0.7f, 0.6f, 0f), 1)), mat = alphaMat });
        }
        /// light grey wisp (damaged engine, gun barrels)
        public static void Smoke(Vector3 p)
        {
            Ensure();
            Emit(p, new P { burst = 3, duration = 0.1f, life0 = 2.5f, life1 = 3.5f, speed0 = 0.3f, speed1 = 0.8f, size0 = 0.6f, size1 = 1f, grow = 3f, upward = 0.5f, noise = 0.4f, radius = 0.2f, spin = 0.4f, color = SootRamp(0.55f), mat = alphaMat });
        }
        /// a full explosion: flash, fireball, sparks, soot column, dust ring, light
        public static void Explosion(Vector3 p, float scale = 1f)
        {
            Ensure();
            KryvonShellShock.Near(p, scale);                 // ringing ears, muffled world, dark edges
            foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))       // the blast tears helmets off close by
                if (h.IsAlive && (h.transform.position + Vector3.up * 1.6f - p).sqrMagnitude < 3.5f * 3.5f * scale * scale) h.BlowOffHelmet();
            FlashLight(p + Vector3.up * 0.5f, new Color(1f, 0.7f, 0.35f), 9f * scale, 18f * scale, 0.5f);
            Emit(p, new P { burst = 2, duration = 0.05f, life0 = 0.12f, life1 = 0.18f, speed0 = 0, speed1 = 0, size0 = 5f * scale, size1 = 6f * scale, grow = 1.4f, radius = 0.01f, color = G((new Color(1f, 0.95f, 0.8f, 1f), 0), (new Color(1f, 0.7f, 0.3f, 0f), 1)), mat = addMat });
            Emit(p, new P { burst = (int)(26 * scale), duration = 0.1f, life0 = 0.5f, life1 = 1.1f, speed0 = 2f * scale, speed1 = 6.5f * scale, size0 = 1.4f * scale, size1 = 2.6f * scale, grow = 1.9f, gravity = -0.15f, noise = 0.8f, radius = 0.4f * scale, spin = 1.2f, color = FireRamp, mat = addMat });
            Emit(p, new P { burst = (int)(40 * scale), duration = 0.1f, life0 = 0.6f, life1 = 1.6f, speed0 = 7f, speed1 = 18f * scale, size0 = 0.04f, size1 = 0.1f, grow = 0.5f, gravity = 1.1f, radius = 0.2f, color = SparkRamp, mat = sparkMat, stretch = true });
            Emit(p + Vector3.up * 0.5f, new P { burst = (int)(18 * scale), duration = 0.1f, life0 = 3.5f, life1 = 6.5f, speed0 = 1f, speed1 = 3.5f * scale, size0 = 1.8f * scale, size1 = 3.2f * scale, grow = 2.6f, upward = 0.6f, noise = 0.6f, radius = 0.6f * scale, spin = 0.3f, color = SootRamp(0.85f), mat = alphaMat });
            Emit(p + Vector3.up * 0.1f, new P { burst = (int)(20 * scale), duration = 0.1f, life0 = 1.2f, life1 = 2.4f, speed0 = 4f * scale, speed1 = 7f * scale, size0 = 0.8f, size1 = 1.5f * scale, grow = 2.5f, gravity = 0.05f, noise = 0.3f, radius = 0.5f, spin = 0.3f, flatShape = true, color = G((new Color(0.62f, 0.56f, 0.46f, 0f), 0), (new Color(0.62f, 0.56f, 0.46f, 0.5f), 0.1f), (new Color(0.6f, 0.55f, 0.48f, 0f), 1)), mat = alphaMat });
        }
        /// kept for older callers: a frag / vehicle blast
        public static void Fire(Vector3 p) => Explosion(p, 1f);
        /// one small lick of flame (molotov in flight, burning debris)
        public static void Flame(Vector3 p)
        {
            Ensure();
            Emit(p, new P { burst = 3, duration = 0.05f, life0 = 0.35f, life1 = 0.7f, speed0 = 0.4f, speed1 = 1.2f, size0 = 0.4f, size1 = 0.8f, grow = 0.6f, upward = 1.5f, noise = 0.6f, radius = 0.15f, color = FireRamp, mat = flameMat });
        }

        /// burning ground (molotov, wreck): tall flames, embers, a soot column and a flickering light, until destroyed
        public static GameObject GroundFire(Vector3 at, float radius, float seconds, Transform parent = null)
        {
            Ensure();
            var root = new GameObject("GroundFire"); root.transform.position = at; if (parent) root.transform.SetParent(parent, true);
            float area = Mathf.Max(0.3f, radius * radius);
            // fire tongues: rise and shrink, a teardrop texture so they read as flames, not blobs
            Emit(at, new P { rate = 26 * area, loop = true, duration = 1f, life0 = 0.5f, life1 = 1.0f, speed0 = 0.6f, speed1 = 1.6f, size0 = 0.7f, size1 = 1.5f, grow = 0.55f, upward = 2.2f, noise = 0.7f, radius = radius, flatShape = true, color = FireRamp, mat = flameMat }, root.transform);
            // glow at the base
            Emit(at + Vector3.up * 0.15f, new P { rate = 8 * area, loop = true, duration = 1f, life0 = 0.4f, life1 = 0.7f, speed0 = 0.1f, speed1 = 0.3f, size0 = 1.2f, size1 = 2f, grow = 1.2f, radius = radius * 0.8f, flatShape = true, spin = 0.8f, color = G((new Color(1f, 0.6f, 0.2f, 0f), 0), (new Color(1f, 0.5f, 0.15f, 0.35f), 0.3f), (new Color(0.8f, 0.2f, 0.05f, 0f), 1)), mat = addMat }, root.transform);
            // embers
            Emit(at, new P { rate = 10 * area, loop = true, duration = 1f, life0 = 1.2f, life1 = 2.5f, speed0 = 1f, speed1 = 3f, size0 = 0.03f, size1 = 0.07f, grow = 0.6f, upward = 1.2f, noise = 1.2f, radius = radius, flatShape = true, color = SparkRamp, mat = sparkMat }, root.transform);
            // black smoke
            Emit(at + Vector3.up * 1.2f, new P { rate = 4 * Mathf.Sqrt(area), loop = true, duration = 1f, life0 = 4f, life1 = 7f, speed0 = 0.5f, speed1 = 1.2f, size0 = 1.2f, size1 = 2.2f, grow = 3.5f, upward = 0.9f, noise = 0.5f, radius = radius * 0.6f, spin = 0.3f, color = SootRamp(0.7f), mat = alphaMat }, root.transform);
            var l = new GameObject("FireLight").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.transform.localPosition = Vector3.up * 1.0f;
            l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.18f); l.range = 6f + radius * 3f; l.shadows = LightShadows.None;
            l.gameObject.AddComponent<Flicker>().baseI = 2.5f + radius;
            if (seconds > 0) root.AddComponent<FireLife>().Init(seconds);
            return root;
        }
        /// lets the flames die down (emission off, light fades) instead of vanishing
        class FireLife : MonoBehaviour
        {
            float end; bool stopping;
            public void Init(float s) { end = Time.time + s; }
            void Update()
            {
                if (!stopping && Time.time >= end)
                {
                    stopping = true;
                    foreach (var ps in GetComponentsInChildren<ParticleSystem>()) { var m = ps.main; m.loop = false; ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
                    Destroy(gameObject, 7f);
                }
                if (stopping) foreach (var f in GetComponentsInChildren<Flicker>()) f.baseI = Mathf.MoveTowards(f.baseI, 0, Time.deltaTime * 3f);
            }
        }

        /// smoke grenade: a thick white wall that builds up, hangs, and thins out
        public static GameObject SmokeScreen(Vector3 at, float seconds)
        {
            Ensure();
            var root = new GameObject("SmokeScreen"); root.transform.position = at;
            Emit(at + Vector3.up * 0.3f, new P { rate = 9, loop = true, duration = 1f, life0 = 9f, life1 = 13f, speed0 = 0.8f, speed1 = 2.2f, size0 = 2.5f, size1 = 4f, grow = 2.4f, gravity = -0.01f, noise = 0.35f, radius = 0.6f, spin = 0.15f,
                color = G((new Color(0.9f, 0.9f, 0.88f, 0f), 0), (new Color(0.88f, 0.88f, 0.86f, 0.92f), 0.08f), (new Color(0.85f, 0.85f, 0.84f, 0.85f), 0.7f), (new Color(0.85f, 0.85f, 0.84f, 0f), 1)), mat = alphaMat }, root.transform);
            // the hissing canister at the start
            Emit(at, new P { rate = 18, loop = true, duration = 1f, life0 = 0.8f, life1 = 1.4f, speed0 = 2f, speed1 = 4f, size0 = 0.3f, size1 = 0.6f, grow = 3f, noise = 0.6f, radius = 0.05f, color = G((new Color(1, 1, 1, 0.8f), 0), (new Color(0.9f, 0.9f, 0.9f, 0f), 1)), mat = alphaMat }, root.transform);
            root.AddComponent<FireLife>().Init(seconds);
            return root;
        }
        /// stun grenade: a white burst and a very bright, short light
        public static void Flash(Vector3 p)
        {
            Ensure();
            FlashLight(p + Vector3.up * 0.4f, Color.white, 40f, 25f, 0.2f);
            Emit(p, new P { burst = 1, duration = 0.05f, life0 = 0.15f, life1 = 0.15f, size0 = 6f, size1 = 6f, grow = 1.5f, radius = 0.01f, color = G((new Color(1, 1, 1, 1), 0), (new Color(1, 1, 1, 0), 1)), mat = addMat });
            Emit(p, new P { burst = 25, duration = 0.05f, life0 = 0.3f, life1 = 0.7f, speed0 = 4f, speed1 = 10f, size0 = 0.03f, size1 = 0.06f, gravity = 0.6f, radius = 0.1f, color = SparkRamp, mat = sparkMat, stretch = true });
            Emit(p, new P { burst = 6, duration = 0.05f, life0 = 2f, life1 = 3f, speed0 = 0.3f, speed1 = 1f, size0 = 0.8f, size1 = 1.3f, grow = 2.5f, noise = 0.3f, radius = 0.2f, color = SootRamp(0.4f), mat = alphaMat });
        }
        public static void SmokeCloud(Vector3 p, float k) { /* replaced by SmokeScreen */ }
        public static void Trail(Vector3 p, bool fire)
        {
            Ensure();
            Emit(p, new P { burst = 1, duration = 0.05f, life0 = fire ? 0.3f : 0.5f, life1 = fire ? 0.5f : 0.8f, speed0 = 0.1f, speed1 = 0.3f, size0 = fire ? 0.2f : 0.18f, size1 = fire ? 0.35f : 0.25f, grow = fire ? 0.6f : 2f, radius = 0.02f, color = fire ? FireRamp : G((new Color(0.85f, 0.85f, 0.85f, 0.5f), 0), (new Color(0.85f, 0.85f, 0.85f, 0f), 1)), mat = fire ? flameMat : alphaMat });
        }
        /// metal on metal / concrete: a quick spray of sparks (vehicle crashes, bullet hits on steel)
        public static void Sparks(Vector3 p, Vector3 normal, int n = 14)
        {
            Ensure();
            var ps = Emit(p, new P { burst = n, duration = 0.05f, life0 = 0.2f, life1 = 0.5f, speed0 = 3f, speed1 = 8f, size0 = 0.025f, size1 = 0.05f, gravity = 1f, radius = 0.05f, color = SparkRamp, mat = sparkMat, stretch = true });
            if (normal != Vector3.zero) ps.transform.rotation = Quaternion.LookRotation(normal);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 40;
        }

        /// GLACIER level 7 kill effect: a burst of ice shards, frost mist and a cold flash
        public static void IceBurst(Vector3 p)
        {
            Ensure();
            FlashLight(p, new Color(0.55f, 0.85f, 1f), 6f, 8f, 0.6f);
            Emit(p, new P { burst = 40, duration = 0.05f, life0 = 0.5f, life1 = 1.2f, speed0 = 3f, speed1 = 9f, size0 = 0.04f, size1 = 0.12f, gravity = 0.9f, radius = 0.3f, spin = 8f, color = G((new Color(0.9f, 0.98f, 1f, 1f), 0), (new Color(0.5f, 0.85f, 1f, 0.9f), 0.6f), (new Color(0.4f, 0.75f, 1f, 0f), 1)), mat = sparkMat, stretch = true });
            Emit(p, new P { burst = 12, duration = 0.05f, life0 = 1.2f, life1 = 2.2f, speed0 = 0.5f, speed1 = 2f, size0 = 0.8f, size1 = 1.4f, grow = 2.2f, noise = 0.3f, radius = 0.4f, spin = 0.4f, color = G((new Color(0.85f, 0.95f, 1f, 0f), 0), (new Color(0.8f, 0.93f, 1f, 0.55f), 0.15f), (new Color(0.8f, 0.93f, 1f, 0f), 1)), mat = alphaMat });
            Emit(p, new P { burst = 1, duration = 0.05f, life0 = 0.25f, life1 = 0.25f, size0 = 3.5f, size1 = 3.5f, grow = 1.6f, radius = 0.01f, color = G((new Color(0.7f, 0.92f, 1f, 0.9f), 0), (new Color(0.5f, 0.8f, 1f, 0f), 1)), mat = addMat });
        }

        /// a helmet / vest giving way: hard fragments flung out with sparks and a puff of fibre dust
        public static void ArmorShatter(Vector3 p, bool helmet)
        {
            Ensure();
            Sparks(p, Vector3.up, 16);
            Emit(p, new P { burst = helmet ? 14 : 18, duration = 0.05f, life0 = 0.9f, life1 = 1.5f, speed0 = 2.5f, speed1 = 5.5f, size0 = 0.03f, size1 = 0.08f, gravity = 1.6f, radius = 0.12f, spin = 10f,
                color = G((new Color(0.25f, 0.27f, 0.22f, 1f), 0), (new Color(0.2f, 0.22f, 0.18f, 1f), 0.85f), (new Color(0.2f, 0.22f, 0.18f, 0f), 1)), mat = alphaMat });
            Emit(p, new P { burst = 5, duration = 0.05f, life0 = 0.8f, life1 = 1.4f, speed0 = 0.4f, speed1 = 1.2f, size0 = 0.25f, size1 = 0.45f, grow = 2f, noise = 0.3f, radius = 0.1f, color = SootRamp(0.45f), mat = alphaMat });
        }
        static readonly System.Collections.Generic.Queue<GameObject> drops = new();
        /// a blood drop on the ground that soaks in and fades (pooled, at most 40 on the map)
        public static void BloodDrop(Vector3 p, Vector3 n)
        {
            Ensure();
            GameObject g;
            if (drops.Count >= 40) { g = drops.Dequeue(); if (!g) g = null; } else g = null;
            if (!g)
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(g.GetComponent<Collider>()); g.name = "BloodDrop";
                var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = alphaMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                g.AddComponent<DropFade>();
            }
            g.transform.SetPositionAndRotation(p + n * 0.01f, Quaternion.LookRotation(-n) * Quaternion.Euler(0, 0, Random.Range(0, 360f)));
            g.transform.localScale = Vector3.one * Random.Range(0.12f, 0.24f);
            g.GetComponent<DropFade>().Restart(); drops.Enqueue(g);
        }
        class DropFade : MonoBehaviour
        {
            float t0; MeshRenderer r; MaterialPropertyBlock b;
            public void Restart() { t0 = Time.time; enabled = true; gameObject.SetActive(true); }
            void Update()
            {
                r ??= GetComponent<MeshRenderer>(); b ??= new MaterialPropertyBlock();
                float k = (Time.time - t0) / 9f; if (k >= 1f) { gameObject.SetActive(false); enabled = false; return; }
                b.SetColor("_BaseColor", new Color(0.32f, 0.02f, 0.02f, 0.85f * (1f - k * k))); r.SetPropertyBlock(b);
            }
        }

        // ---------- gunfire ----------
        /// muzzle flash: a hot star of fire out of the barrel (additive, a frame or two) and a few fast sparks
        public static void MuzzleFlash(Vector3 at, Vector3 dir)
        {
            Ensure();
            var f = Emit(at, new P { burst = 3, duration = 0.02f, life0 = 0.035f, life1 = 0.06f, speed0 = 2f, speed1 = 6f, size0 = 0.25f, size1 = 0.45f, grow = 1.3f, radius = 0.01f, color = G((new Color(1f, 0.95f, 0.75f, 1f), 0), (new Color(1f, 0.6f, 0.2f, 0f), 1)), mat = flameMat });
            f.transform.rotation = Quaternion.LookRotation(dir); var sh = f.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12;
            var s = Emit(at, new P { burst = 4, duration = 0.02f, life0 = 0.06f, life1 = 0.14f, speed0 = 10f, speed1 = 22f, size0 = 0.015f, size1 = 0.03f, radius = 0.01f, color = SparkRamp, mat = sparkMat, stretch = true });
            s.transform.rotation = Quaternion.LookRotation(dir); var sh2 = s.shape; sh2.shapeType = ParticleSystemShapeType.Cone; sh2.angle = 8;
        }
        /// a spent brass case flicked out of the ejection port, tumbling down
        public static void Shell(Vector3 at, Vector3 right)
        {
            Ensure();
            var s = Emit(at, new P { burst = 1, duration = 0.02f, life0 = 0.6f, life1 = 0.8f, speed0 = 2.2f, speed1 = 3.2f, size0 = 0.035f, size1 = 0.045f, gravity = 1.6f, radius = 0.005f, spin = 20f,
                color = G((new Color(0.95f, 0.75f, 0.35f, 1f), 0), (new Color(0.8f, 0.6f, 0.25f, 1f), 0.85f), (new Color(0.8f, 0.6f, 0.25f, 0f), 1)), mat = alphaMat });
            s.transform.rotation = Quaternion.LookRotation(right + Vector3.up * 0.8f); var sh = s.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 15;
            var col = s.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = 0.35f; col.dampen = 0.4f; col.quality = ParticleSystemCollisionQuality.Low;
        }
        /// thin smoke curling up out of a hot barrel after a long burst
        public static void BarrelSmoke(Vector3 at)
        {
            Ensure();
            Emit(at, new P { burst = 1, duration = 0.02f, life0 = 1f, life1 = 1.6f, speed0 = 0.05f, speed1 = 0.2f, size0 = 0.06f, size1 = 0.1f, grow = 4f, upward = 0.35f, noise = 0.15f, radius = 0.01f, spin = 0.6f, color = G((new Color(0.85f, 0.85f, 0.85f, 0f), 0), (new Color(0.8f, 0.8f, 0.8f, 0.35f), 0.2f), (new Color(0.8f, 0.8f, 0.8f, 0f), 1)), mat = alphaMat });
        }
        /// a bullet hitting the world: dust and chips on stone / sand, sparks on metal
        public static void Impact(Vector3 p, Vector3 n, bool metal)
        {
            Ensure();
            if (metal) { Sparks(p, n, 10); return; }
            var d = Emit(p + n * 0.02f, new P { burst = 5, duration = 0.02f, life0 = 0.5f, life1 = 1f, speed0 = 0.8f, speed1 = 2.2f, size0 = 0.12f, size1 = 0.25f, grow = 2.5f, gravity = 0.15f, noise = 0.2f, radius = 0.02f, spin = 0.5f, color = G((new Color(0.72f, 0.65f, 0.52f, 0.75f), 0), (new Color(0.72f, 0.66f, 0.55f, 0f), 1)), mat = alphaMat });
            d.transform.rotation = Quaternion.LookRotation(n); var sh = d.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25;
            var c = Emit(p + n * 0.02f, new P { burst = 8, duration = 0.02f, life0 = 0.3f, life1 = 0.6f, speed0 = 2f, speed1 = 4f, size0 = 0.015f, size1 = 0.035f, gravity = 1.5f, radius = 0.01f, color = G((new Color(0.45f, 0.4f, 0.33f, 1f), 0), (new Color(0.4f, 0.36f, 0.3f, 0f), 1)), mat = alphaMat });
            c.transform.rotation = Quaternion.LookRotation(n); var sh2 = c.shape; sh2.shapeType = ParticleSystemShapeType.Cone; sh2.angle = 35;
        }

        /// sandstorm: dust clouds and grains blowing past the camera (follows it)
        public static ParticleSystem Wind(Color c)
        {
            Ensure();
            var ps = Emit(Vector3.zero, new P { rate = 26, loop = true, duration = 1f, life0 = 2f, life1 = 3.5f, speed0 = 0f, speed1 = 0.5f, size0 = 2.5f, size1 = 5f, grow = 1.6f, noise = 0.4f, radius = 14f, spin = 0.3f,
                color = G((new Color(c.r, c.g, c.b, 0f), 0), (new Color(c.r, c.g, c.b, 0.32f), 0.25f), (new Color(c.r, c.g, c.b, 0f), 1)), mat = alphaMat });
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(7f, 11f); v.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.6f); v.z = new ParticleSystem.MinMaxCurve(1f, 3f);
            var grains = Emit(Vector3.zero, new P { rate = 120, loop = true, duration = 1f, life0 = 0.6f, life1 = 1.2f, speed0 = 0f, speed1 = 0.2f, size0 = 0.015f, size1 = 0.03f, radius = 8f, color = G((new Color(c.r, c.g, c.b, 0f), 0), (new Color(c.r + 0.1f, c.g + 0.1f, c.b + 0.1f, 0.8f), 0.2f), (new Color(c.r, c.g, c.b, 0f), 1)), mat = alphaMat, stretch = true }, ps.transform);
            var v2 = grains.velocityOverLifetime; v2.enabled = true; v2.space = ParticleSystemSimulationSpace.World; v2.x = new ParticleSystem.MinMaxCurve(14f, 20f);
            return ps;
        }
        /// victory: rockets climb and burst into gold, crimson, white and olive sparks above the winner
        public static void Fireworks(Vector3 around) { Ensure(); new GameObject("Fireworks").AddComponent<FireworkShow>().center = around; }
        class FireworkShow : MonoBehaviour
        {
            public Vector3 center; float next; int left = 9;
            static readonly Color[] cols = { new(1f, 0.85f, 0.35f), new(0.95f, 0.3f, 0.25f), new(1f, 1f, 1f), new(0.55f, 0.85f, 0.5f), new(0.45f, 0.75f, 1f) };
            void Update()
            {
                if (Time.unscaledTime < next) return;
                if (left-- <= 0) { Destroy(gameObject, 4f); enabled = false; return; }
                next = Time.unscaledTime + Random.Range(0.35f, 0.8f);
                var at = center + new Vector3(Random.Range(-30f, 30f), Random.Range(28f, 48f), Random.Range(-30f, 30f));
                var c = cols[Random.Range(0, cols.Length)];
                FlashLight(at, c, 8f, 60f, 0.6f);
                Emit(at, new P { burst = 90, duration = 0.05f, life0 = 1.2f, life1 = 2.2f, speed0 = 9f, speed1 = 15f, size0 = 0.18f, size1 = 0.32f, gravity = 0.35f, radius = 0.2f,
                    color = G((new Color(1, 1, 1, 1), 0), (new Color(c.r, c.g, c.b, 1f), 0.15f), (new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 0f), 1)), mat = sparkMat, stretch = true });
                Emit(at, new P { burst = 2, duration = 0.05f, life0 = 0.25f, life1 = 0.35f, size0 = 10f, size1 = 12f, grow = 1.5f, radius = 0.01f, color = G((new Color(c.r, c.g, c.b, 0.8f), 0), (new Color(c.r, c.g, c.b, 0f), 1)), mat = addMat });
            }
        }

        /// boots hitting the ground after the parachute: a ring of dust thrown out low and wide
        public static void LandingDust(Vector3 p)
        {
            Ensure();
            Emit(p + Vector3.up * 0.1f, new P { burst = 22, duration = 0.05f, life0 = 1.2f, life1 = 2.2f, speed0 = 3f, speed1 = 6f, size0 = 0.6f, size1 = 1.2f, grow = 2.6f, gravity = 0.05f, noise = 0.3f, radius = 0.4f, flatShape = true, spin = 0.4f,
                color = G((new Color(0.7f, 0.63f, 0.5f, 0f), 0), (new Color(0.7f, 0.63f, 0.5f, 0.55f), 0.1f), (new Color(0.72f, 0.66f, 0.55f, 0f), 1)), mat = alphaMat });
            CameraShake.Kick(0.25f);
        }

        /// a swimmer's hand going in: a white spray thrown up and a flat ring of foam
        public static void Splash(Vector3 p, float k = 1f)
        {
            Ensure();
            Emit(p, new P { burst = (int)(14 * k) + 4, duration = 0.05f, life0 = 0.35f, life1 = 0.7f, speed0 = 1.5f * k, speed1 = 3.2f * k, size0 = 0.06f, size1 = 0.16f, grow = 1.4f, gravity = 1f, radius = 0.12f, upward = 0.8f,
                color = G((new Color(0.95f, 0.98f, 1f, 0.9f), 0), (new Color(0.9f, 0.95f, 1f, 0f), 1)), mat = alphaMat });
            Foam(p, 0.6f * k);
        }
        /// foam on the water surface (wake behind a swimmer, ripples around someone treading water)
        public static void Foam(Vector3 p, float k = 1f)
        {
            Ensure();
            Emit(p, new P { burst = 3, duration = 0.05f, life0 = 1.4f, life1 = 2.4f, speed0 = 0.25f, speed1 = 0.7f * k, size0 = 0.5f * k, size1 = 0.9f * k, grow = 2.2f, gravity = 0f, radius = 0.25f * k, flatShape = true, spin = 0.3f,
                color = G((new Color(0.92f, 0.96f, 1f, 0f), 0), (new Color(0.92f, 0.96f, 1f, 0.42f), 0.12f), (new Color(0.9f, 0.95f, 1f, 0f), 1)), mat = alphaMat });
        }

        static Material tracerMat;
        /// a short bright streak along a bullet's path (used for the bots' shots)
        public static void Tracer(Vector3 from, Vector3 to)
        {
            if (!tracerMat) { var m = Resources.Load<Material>("KryvonUnlit"); if (!m) return; tracerMat = new Material(m); tracerMat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.45f)); }
            var g = new GameObject("Tracer"); var l = g.AddComponent<LineRenderer>(); l.sharedMaterial = tracerMat; l.positionCount = 2; l.widthMultiplier = 0.03f;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; var dir = (to - from).normalized; float d = Vector3.Distance(from, to);
            l.SetPosition(0, from + dir * Mathf.Min(2f, d * 0.2f)); l.SetPosition(1, from + dir * Mathf.Min(d, 14f + d * 0.4f));
            Object.Destroy(g, 0.05f);
        }
        public static void Glass(Vector3 p)
        {
            Ensure();
            Emit(p, new P { burst = 40, duration = 0.05f, life0 = 0.6f, life1 = 1.2f, speed0 = 1.5f, speed1 = 3.5f, size0 = 0.03f, size1 = 0.08f, gravity = 1.4f, radius = 0.4f, spin = 6f, color = G((new Color(0.85f, 0.95f, 1f, 0.9f), 0), (new Color(0.8f, 0.9f, 0.95f, 0.7f), 0.7f), (new Color(0.8f, 0.9f, 0.95f, 0f), 1)), mat = sparkMat });
        }
    }
}

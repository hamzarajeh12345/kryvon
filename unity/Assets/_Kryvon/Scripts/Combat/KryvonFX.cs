// Small runtime particle effects: blood burst, dust puff, glass shards.
using UnityEngine;

namespace Kryvon
{
    public static class KryvonFX
    {
        static Material mat;
        static Material Mat() => mat ? mat : mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        static void Burst(Vector3 p, int count, Color c, float speed, float size, float life, float gravity)
        {
            var g = new GameObject("FX"); g.transform.position = p;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = 0.2f; m.loop = false; m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            m.startColor = c; m.gravityModifier = gravity; m.stopAction = ParticleSystemStopAction.Destroy; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = gr;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat();
            ps.Play();
        }
        public static void Blood(Vector3 p, int n) => Burst(p, n, new Color(0.45f, 0.02f, 0.02f, 0.95f), 3.2f, 0.12f, 0.7f, 1.2f);
        public static void Dust(Vector3 p) => Burst(p, 30, new Color(0.7f, 0.64f, 0.52f, 0.6f), 1.6f, 0.5f, 1.4f, -0.05f);
        public static void Smoke(Vector3 p) => Burst(p, 3, new Color(0.9f, 0.12f, 0.08f, 0.55f), 0.8f, 1.6f, 3.5f, -0.08f);
        public static void Fire(Vector3 p) { Burst(p, 60, new Color(1f, 0.55f, 0.15f, 0.9f), 6f, 0.9f, 0.9f, -0.2f); Burst(p, 40, new Color(0.15f, 0.13f, 0.12f, 0.8f), 3f, 2.2f, 3.5f, -0.15f); }
        public static void Glass(Vector3 p) => Burst(p, 45, new Color(0.8f, 0.9f, 0.95f, 0.85f), 3.5f, 0.07f, 1.1f, 1.4f);
    }
}

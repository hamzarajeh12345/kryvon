// Muzzle flash light + slight random pitch on each shot, and a bullet-impact puff with sound.
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(WeaponController))]
    public class KryvonMuzzleFX : MonoBehaviour
    {
        [SerializeField] Light flash;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip impactHard, impactBody;
        WeaponController weapons; float flashT;

        void Awake()
        {
            weapons = GetComponent<WeaponController>();
            weapons.OnShot += _ =>
            {
                flashT = 0.05f;
                if (source) source.pitch = Random.Range(0.94f, 1.06f);
                var cam = Camera.main;
                if (cam && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
                {
                    bool body = hit.collider.GetComponentInParent<Health>() != null;
                    var clip = body ? impactBody : impactHard;
                    if (clip) AudioSource.PlayClipAtPoint(clip, hit.point, 0.8f);
                    if (!body) Puff(hit.point, hit.normal);
                }
            };
        }

        void Update()
        {
            if (!flash) return;
            flashT -= Time.deltaTime;
            flash.enabled = flashT > 0f;
            if (flash.enabled) flash.intensity = Random.Range(3f, 6f);
        }

        static Material puffMat;
        static void Puff(Vector3 p, Vector3 n)
        {
            var g = new GameObject("Impact");
            g.transform.position = p + n * 0.02f; g.transform.rotation = Quaternion.LookRotation(n);
            var ps = g.AddComponent<ParticleSystem>();
            var main = ps.main; main.duration = 0.3f; main.loop = false; main.startLifetime = 0.5f; main.startSpeed = 2.5f; main.startSize = 0.18f;
            main.startColor = new Color(0.75f, 0.68f, 0.56f, 0.8f); main.gravityModifier = 0.4f; main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25; sh.radius = 0.02f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) });
            col.color = gr;
            if (puffMat == null) puffMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = puffMat;
            ps.Play();
        }
    }
}

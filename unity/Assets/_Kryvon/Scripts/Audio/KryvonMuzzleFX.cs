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
        WeaponController weapons; float flashT, heat, lastShot, nextSmoke; Vector3 lastMuzzle;

        void Awake()
        {
            weapons = GetComponent<WeaponController>();
            weapons.OnShot += _ =>
            {
                flashT = 0.05f;
                if (source) source.pitch = Random.Range(0.94f, 1.06f);
                // fire out of the barrel, a brass case out of the side, and heat building up for the smoke after the burst
                var cam0 = Camera.main;
                var muzzle = flash ? flash.transform.position : transform.position + transform.forward * 0.8f + Vector3.up * 1.4f;
                var aim = cam0 ? cam0.transform.forward : transform.forward;
                KryvonFX.MuzzleFlash(muzzle, aim);
                KryvonFX.Shell(muzzle - aim * 0.45f + Vector3.up * 0.03f, cam0 ? cam0.transform.right : transform.right);
                heat = Mathf.Min(heat + 1f, 12f); lastShot = Time.time; lastMuzzle = muzzle;
                var cam = Camera.main;
                if (cam && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
                {
                    bool body = hit.collider.GetComponentInParent<Health>() != null;
                    var clip = body ? impactBody : impactHard;
                    if (clip) AudioSource.PlayClipAtPoint(clip, hit.point, 0.8f);
                    if (!body) KryvonFX.Impact(hit.point, hit.normal, hit.collider.GetComponentInParent<KryvonVehicle>() || hit.collider.name.ToLowerInvariant().Contains("metal") || hit.collider.name.ToLowerInvariant().Contains("container"));
                }
            };
        }

        void Update()
        {
            // a hot barrel smokes for a moment after a long burst (6+ shots)
            if (Time.time - lastShot > 0.25f && heat >= 6f && Time.time > nextSmoke && Time.time - lastShot < 1.8f)
            { nextSmoke = Time.time + 0.12f; KryvonFX.BarrelSmoke(flash ? flash.transform.position : lastMuzzle); }
            if (Time.time - lastShot > 1.8f) heat = 0;
            if (!flash) return;
            flashT -= Time.deltaTime;
            flash.enabled = flashT > 0f;
            // warm, bright and short: it lights the hands, the gun and the wall next to you for a frame or two
            if (flash.enabled) { flash.intensity = Random.Range(5f, 8f); flash.range = 7f; flash.color = new Color(1f, 0.72f, 0.38f); }
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
            if (puffMat == null) puffMat = Resources.Load<Material>("KryvonParticle");
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = puffMat;
            ps.Play();
        }
    }
}

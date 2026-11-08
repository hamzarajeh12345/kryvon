// Footsteps that follow the player's speed and the surface underfoot (asphalt/stone = hard, anything else = dirt).
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(PlayerController))]
    public class KryvonFootsteps : MonoBehaviour
    {
        [SerializeField] AudioClip[] hard, dirt;
        [SerializeField] AudioSource source;
        [SerializeField] float strideWalk = 0.62f, strideRun = 0.36f;
        PlayerController body; float timer;

        void Awake()
        {
            body = GetComponent<PlayerController>();
            if (source == null) { source = gameObject.AddComponent<AudioSource>(); source.spatialBlend = 0.6f; source.playOnAwake = false; }
            body.OnLanded += v => { if (v > 3f) Step(1.3f); };
        }

        void Update()
        {
            if (!body.IsGrounded || !body.IsMoving) { timer = 0.05f; return; }
            float speed = body.PlanarVelocity.magnitude;
            float stride = Mathf.Lerp(strideWalk, strideRun, Mathf.InverseLerp(1.8f, 6.4f, speed));
            if (body.Stance == Stance.Crouch) stride *= 1.25f;
            timer -= Time.deltaTime;
            if (timer <= 0f) { Step(body.Stance == Stance.Stand ? Mathf.Lerp(0.45f, 1f, speed / 6.4f) : 0.3f); timer = stride; }
        }

        void Step(float vol)
        {
            bool isHard = false;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var r = hit.collider.GetComponent<Renderer>();
                var n = (r && r.sharedMaterial ? r.sharedMaterial.name : hit.collider.name).ToLowerInvariant();
                isHard = n.Contains("asphalt") || n.Contains("pave") || n.Contains("curb") || n.Contains("road") || n.Contains("stone") || n.Contains("building");
            }
            var set = isHard ? hard : dirt;
            if (set == null || set.Length == 0) return;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.PlayOneShot(set[Random.Range(0, set.Length)], vol);
        }
    }
}

// TargetRespawn – training dummy: turns red when hit, comes back to full health a few seconds after dying.
using System.Collections;
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(Health))]
    public class TargetRespawn : MonoBehaviour
    {
        [SerializeField] float respawnSeconds = 3f;
        [SerializeField] bool strafe;
        [SerializeField] float strafeDistance = 3f, strafeSpeed = 2.5f;

        Health health;
        Renderer[] renderers;
        Vector3 home;
        Color baseColor = Color.white;

        void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0) baseColor = renderers[0].material.color;
            home = transform.position;
            health.OnDamaged += (_, _) => StartCoroutine(Flash());
            health.OnDied += (_, _) => StartCoroutine(Respawn());
        }

        void Update()
        {
            if (strafe && health.IsAlive) transform.position = home + transform.right * Mathf.Sin(Time.time * strafeSpeed / strafeDistance) * strafeDistance;
        }

        IEnumerator Flash()
        {
            foreach (var r in renderers) r.material.color = Color.red;
            yield return new WaitForSeconds(0.08f);
            foreach (var r in renderers) r.material.color = health.IsAlive ? baseColor : Color.gray;
        }

        IEnumerator Respawn()
        {
            foreach (var r in renderers) r.enabled = false;
            yield return new WaitForSeconds(respawnSeconds);
            health.Respawn();
            foreach (var r in renderers) { r.enabled = true; r.material.color = baseColor; }
        }
    }
}

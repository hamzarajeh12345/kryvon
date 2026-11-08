// Flare gun: press G (if you picked one up) to fire a red flare; a supply crate with top-tier loot
// parachutes down nearby a few seconds later. Open it with F like a grave crate.
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class KryvonAirdrop : MonoBehaviour
    {
        [SerializeField] GameObject crateTemplate;      // disabled: crate model + KryvonDeathBox + "Inventory" + "Chute"
        [SerializeField] AudioClip flareSound, planeSound;
        [SerializeField] float delay = 9f, fallSpeed = 7f;
        KryvonGear gear;

        void Awake() => gear = GetComponent<KryvonGear>() ?? gameObject.AddComponent<KryvonGear>();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.gKey.wasPressedThisFrame || gear.flares <= 0 || !crateTemplate) return;
            gear.flares--;
            StartCoroutine(Drop(transform.position + transform.forward * 25f));
        }

        IEnumerator Drop(Vector3 at)
        {
            if (flareSound) AudioSource.PlayClipAtPoint(flareSound, transform.position, 1f);
            // the flare: a bright red light rising, trailing red smoke
            var f = new GameObject("Flare"); f.transform.position = transform.position + Vector3.up * 2f;
            var l = f.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.08f); l.range = 40f; l.intensity = 8f;
            for (float t = 0; t < 2.5f; t += Time.deltaTime) { f.transform.position += Vector3.up * 45f * Time.deltaTime; KryvonFX.Smoke(f.transform.position); yield return null; }
            for (float t = 0; t < 4f; t += Time.deltaTime) { l.intensity = Mathf.Lerp(8, 0, t / 4f); yield return null; }
            Destroy(f);
            if (planeSound) AudioSource.PlayClipAtPoint(planeSound, at + Vector3.up * 60f, 1f);
            yield return new WaitForSeconds(Mathf.Max(0, delay - 6.5f));

            var crate = Instantiate(crateTemplate, at + Vector3.up * 120f, Quaternion.identity); crate.name = "Supply Drop"; crate.SetActive(true);
            var chute = crate.transform.Find("Chute");
            float groundY = at.y;
            if (Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) groundY = hit.point.y;
            while (crate.transform.position.y > groundY + 0.05f)
            {
                var p = crate.transform.position; p.y = Mathf.Max(groundY, p.y - fallSpeed * Time.deltaTime);
                crate.transform.position = p; crate.transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f) * 4f, Time.time * 10f, Mathf.Cos(Time.time) * 4f);
                yield return null;
            }
            crate.transform.rotation = Quaternion.Euler(0, crate.transform.eulerAngles.y, 0);
            if (chute) Destroy(chute.gameObject);
            KryvonFX.Dust(crate.transform.position + Vector3.up * 0.3f);
            var box = crate.GetComponentInChildren<KryvonDeathBox>(true); var inv = crate.transform.Find("Inventory");
            if (box && inv) box.Fill(inv, "Supply Drop");
            // red smoke marks the crate for a while
            for (float t = 0; t < 40f; t += 0.15f) { KryvonFX.Smoke(crate.transform.position + Vector3.up * 1.2f); yield return new WaitForSeconds(0.15f); }
        }
    }
}

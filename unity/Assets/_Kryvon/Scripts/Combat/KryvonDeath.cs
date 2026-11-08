// What happens when someone dies: death animation, blood burst, kill feed, then the body becomes a grave
// with a loot crate holding everything they carried (open it with F).
using System.Collections;
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(Health))]
    public class KryvonDeath : MonoBehaviour
    {
        [SerializeField] GameObject grave;               // tombstone + crate, disabled until death
        [SerializeField] Transform inventoryTemplate;    // disabled loot items cloned into the crate
        [SerializeField] float graveDelay = 2.6f;
        [SerializeField] float respawnAfter = 25f;       // training targets come back; 0 = never
        [SerializeField] AudioClip deathSound;
        Health health; Animator anim; Renderer[] body; Collider[] hit;
        Vector3 home; Quaternion homeRot;

        void Awake()
        {
            health = GetComponent<Health>(); anim = GetComponentInChildren<Animator>();
            home = transform.position; homeRot = transform.rotation;
            health.OnDied += (_, info) => StartCoroutine(Die(info));
            var tr = GetComponent<TargetRespawn>(); if (tr) tr.enabled = false;   // we handle respawn
        }

        void SetDead(bool v)
        {
            if (anim == null) return;
            foreach (var p in anim.parameters) if (p.name == "Dead" && p.type == AnimatorControllerParameterType.Bool) anim.SetBool("Dead", v);
        }

        IEnumerator Die(DamageInfo info)
        {
            SetDead(true);
            KryvonFX.Blood(transform.position + Vector3.up * 1.2f, info.Headshot ? 40 : 24);
            if (deathSound) AudioSource.PlayClipAtPoint(deathSound, transform.position, 0.9f);
            string killer = info.Source ? (info.Source.CompareTag("Player") ? "You" : info.Source.name) : info.Cause;
            KryvonKillFeed.Push($"{killer}  ->  {name}{(info.Headshot ? "  [HEADSHOT]" : "")}", info.Source && info.Source.CompareTag("Player"));
            hit = GetComponentsInChildren<Collider>();
            foreach (var c in hit) if (c.GetComponent<HitBox>()) c.enabled = false;
            yield return new WaitForSeconds(graveDelay);

            body = GetComponentsInChildren<Renderer>();
            foreach (var r in body) if (!grave || !r.transform.IsChildOf(grave.transform)) r.enabled = false;
            if (grave)
            {
                grave.transform.SetParent(null, true);
                grave.transform.position = transform.position; grave.transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                grave.SetActive(true);
                var box = grave.GetComponentInChildren<KryvonDeathBox>(true);
                if (box && inventoryTemplate) box.Fill(inventoryTemplate, name);
                KryvonFX.Dust(grave.transform.position + Vector3.up * 0.3f);
            }
            if (respawnAfter <= 0) yield break;
            yield return new WaitForSeconds(respawnAfter);
            if (grave) { grave.SetActive(false); grave.transform.SetParent(transform, true); }
            transform.SetPositionAndRotation(home, homeRot);
            foreach (var r in body) r.enabled = true;
            foreach (var c in hit) c.enabled = true;
            SetDead(false);
            health.Respawn();
        }
    }
}

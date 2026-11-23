// One AI opponent. Spawned by KryvonBots, it rides the jet, jumps somewhere along the flight line, opens its chute,
// then: loots nearby items → moves into the safe zone → fights anyone it can see (the player or other bots).
// Aim is deliberately human: reaction time, spread that grows with distance and target speed, bursts, reloads.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class KryvonBot : MonoBehaviour
    {
        public enum State { Plane, Falling, Loot, Move, Fight, Dead }
        public State S { get; private set; } = State.Plane;
        /// on the player's team (duo / squad): never shoots the player or other mates, follows the player
        public Health Hp => hp;
        public bool Mate => hp && player0 && hp.teamId == player0.teamId;
        static Health player0;
        public float jumpAt;                       // 0..1 along the flight line

        [SerializeField] AudioClip shotSound;
        [SerializeField] string gunName = "AK-47";
        [SerializeField] float damage = 19f, roundsPerMinute = 600f, range = 140f;
        [SerializeField] int magazine = 30;

        CharacterController cc; Health hp; Animator anim; Renderer[] rends; GameObject chute;
        Vector3 goal, lastPos; float openAt = 130f, think, stuckT, fallSpeed, fireT, reactT, burstLeft, reloadT, lootT, strafe = 1f, strafeT;
        int ammo; int loots; Health target; KryvonLoot lootGoal;
        static readonly List<KryvonBot> all = new(); public static IReadOnlyList<KryvonBot> All => all;
        static KryvonLoot[] lootCache; static Health player; static SafeZone zone;
        bool hasSpeed, hasAim;

        void Awake()
        {
            if (!GetComponent<KryvonGunHold>()) gameObject.AddComponent<KryvonGunHold>();
            if (!GetComponent<KryvonScopeGlare>()) gameObject.AddComponent<KryvonScopeGlare>();
            cc = GetComponent<CharacterController>(); hp = GetComponent<Health>(); anim = GetComponentInChildren<Animator>();
            rends = GetComponentsInChildren<Renderer>(true);
            ammo = magazine;
            if (anim) foreach (var p in anim.parameters) { if (p.name == "Speed") hasSpeed = true; if (p.name == "Aiming") hasAim = true; }
            hp.OnDamaged += (_, info) => { if (info.Source && info.Source != gameObject) { var h = info.Source.GetComponent<Health>(); if (h && h.IsAlive && h.teamId != hp.teamId && loots > 0 && S != State.Plane && S != State.Falling) { target = h; reactT = Mathf.Min(reactT, 0.25f); S = State.Fight; } } };
            hp.OnDied += (_, _) => { S = State.Dead; if (chute) Destroy(chute); cc.enabled = false; };
        }
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// fight this target right away (used by the trailer and by scripted moments)
        public Health Target => target;
        public bool IsSniper => range >= 250f;
        /// a long-range bolt-action instead of the rifle (some bots in every match): far sight, slow heavy shots
        public void ArmSniper() { gunName = Random.value < 0.5f ? "M24" : "Kar98k"; damage = 58f; roundsPerMinute = 42f; range = 330f; magazine = 5; }
        /// 0 = clumsy recruit … 1 = veteran: reaction time, accuracy, headshot rate and strafing all scale with it
        [HideInInspector] public float skill = 0.5f;
        public void Engage(Health t) { if (!t || S == State.Dead || S == State.Plane || S == State.Falling) return; target = t; reactT = 0.4f; S = State.Fight; }

        public void Board()
        {
            S = State.Plane; cc.enabled = false; Show(false);
        }
        void Show(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                bool grave = false; for (var t = r.transform; t && t != transform; t = t.parent) if (t.name == "Grave" || t.name == "Inventory") { grave = true; break; }
                if (!grave) r.enabled = on;
            }
        }

        public void Jump(Vector3 from, GameObject chuteTemplate, Color a, Color b)
        {
            transform.position = from; S = State.Falling; fallSpeed = 14f + Random.value * 6f; openAt = 90f + Random.value * 40f; Show(true);
            if (chuteTemplate)
            {
                chute = Instantiate(chuteTemplate, transform); chute.SetActive(false);
                chute.transform.localPosition = new Vector3(0, 6.2f, 0.3f); chute.transform.localRotation = Quaternion.identity;
                foreach (var c in chute.GetComponentsInChildren<Collider>()) Destroy(c);
                var r = chute.GetComponent<Renderer>(); if (r) { var ms = r.materials; if (ms.Length > 0) ms[0].SetColor("_BaseColor", a); if (ms.Length > 1) ms[1].SetColor("_BaseColor", b); }
            }
            // a landing spot: a random loot item near the line (bots spread out like people do)
            Cache();
            var spot = lootCache.Length > 0 ? lootCache[Random.Range(0, lootCache.Length)] : null;
            for (int i = 0; i < 12 && lootCache.Length > 0; i++)
            {
                var c = lootCache[Random.Range(0, lootCache.Length)]; if (!c) continue;
                if (spot == null || Flat(c.transform.position - from) < Flat(spot.transform.position - from)) spot = c;
            }
            goal = spot ? spot.transform.position : from + Random.insideUnitSphere * 200f;
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        static void Cache()
        {
            if (lootCache == null || lootCache.Length == 0) lootCache = FindObjectsByType<KryvonLoot>();
            if (!player) { var p = GameObject.FindGameObjectWithTag("Player"); if (p) player = p.GetComponent<Health>(); }
            player0 = player;
            if (!zone) zone = FindAnyObjectByType<SafeZone>();
        }

        float Ground(out Vector3 p)
        {
            p = transform.position - Vector3.up * 999f;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var h, 2000f, ~0, QueryTriggerInteraction.Ignore)) { p = h.point; return h.distance - 0.5f; }
            return 999f;
        }

        void Update()
        {
            if (S == State.Dead || S == State.Plane) return;
            float dt = Time.deltaTime;
            if (S == State.Falling) { Fall(dt); return; }

            think -= dt;
            if (think <= 0) { think = 0.25f + Random.value * 0.1f; Think(); }
            if (S == State.Fight) Fight(dt); else Walk(dt, 1f);
            if (anim && hasSpeed) anim.SetFloat("Speed", cc.velocity.magnitude, 0.12f, dt);
            if (anim && hasAim) anim.SetBool("Aiming", S == State.Fight);
        }

        void Fall(float dt)
        {
            float h = Ground(out var g);
            if (chute && !chute.activeSelf && h < openAt) chute.SetActive(true);
            float down = chute && chute.activeSelf ? 2.2f : fallSpeed;
            var to = goal - transform.position; to.y = 0;
            var hv = to.magnitude > 2f ? to.normalized * Mathf.Min(chute && chute.activeSelf ? 13f : 30f, to.magnitude) : Vector3.zero;
            var p = transform.position + (hv + Vector3.down * down) * dt;
            if (hv.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(hv), 3f * dt);
            if (p.y <= g.y + 0.05f || h < 0.3f)
            {
                p.y = g.y + 0.05f; transform.position = p; if (chute) Destroy(chute);
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                cc.enabled = true; S = State.Loot; lastPos = p; return;
            }
            transform.position = p;
        }

        void Think()
        {
            Cache();
            // enemies in sight
            if (S != State.Fight || target == null || !target.IsAlive)
            {
                target = null;
                var t = loots > 0 ? See() : null; if (t) { target = t; reactT = (0.35f + Random.value * 0.45f) * Mathf.Lerp(1.7f, 0.5f, skill); S = State.Fight; return; }
                if (S == State.Fight) S = State.Move;
            }
            else if (!CanSee(target, range * 1.2f)) { S = State.Move; goal = target.transform.position; target = null; return; }

            if (S == State.Fight) return;
            // mates stay with the player
            if (Mate && player0.IsAlive && !KryvonDrop.Active && Flat(transform.position - player0.transform.position) > 22f)
            { var o = Random.insideUnitCircle * 6f; goal = player0.transform.position + new Vector3(o.x, 0, o.y); S = State.Move; return; }
            // zone first
            if (zone && (zone.Running || zone.Landing))
            {
                var c = zone.NextCenter; float r = zone.NextRadius * 0.8f;
                if (Flat(transform.position - c) > r && (loots >= 3 || !zone.IsInside(transform.position) || zone.Timer < 40f))
                {
                    if (S != State.Move || Flat(goal - c) > r) goal = new Vector3(c.x + Random.Range(-r, r) * 0.5f, transform.position.y, c.z + Random.Range(-r, r) * 0.5f);
                    S = State.Move; return;
                }
            }
            // loot
            if (S == State.Loot || loots < 6)
            {
                if (!lootGoal || !lootGoal.gameObject.activeInHierarchy)
                {
                    lootGoal = null; float best = 45f;
                    for (int i = 0; i < 40; i++)
                    {
                        var l = lootCache[Random.Range(0, lootCache.Length)]; if (!l || !l.gameObject.activeInHierarchy) continue;
                        float d = Flat(l.transform.position - transform.position); if (d < best) { best = d; lootGoal = l; }
                    }
                }
                if (lootGoal) { goal = lootGoal.transform.position; S = State.Loot; return; }
            }
            if (S != State.Move || Flat(goal - transform.position) < 3f)
            {
                var c = zone ? zone.NextCenter : Vector3.zero; float r = zone ? zone.NextRadius * 0.7f : 200f;
                goal = new Vector3(c.x + Random.Range(-r, r), transform.position.y, c.z + Random.Range(-r, r)); S = State.Move;
            }
        }

        Health See()
        {
            Health best = null; float bd = range * KryvonWeather.Visibility * KryvonDayNight.SightMul;    // a sandstorm or haze shortens sight
            if (player && player.IsAlive && !KryvonDrop.Active && player.teamId != hp.teamId) { float d = Vector3.Distance(player.transform.position, transform.position); if (d < bd && CanSee(player, bd)) { best = player; bd = d; } }
            foreach (var b in all)
            {
                if (b == this || b.hp.teamId == hp.teamId || b.S == State.Dead || b.S == State.Plane || b.S == State.Falling) continue;
                float d = Vector3.Distance(b.transform.position, transform.position); if (d >= bd) continue;
                if (CanSee(b.hp, d + 1)) { best = b.hp; bd = d; }
            }
            return best;
        }

        Vector3 Eye => transform.position + Vector3.up * 1.6f;
        bool CanSee(Health h, float maxD)
        {
            if (!h || !h.IsAlive) return false;
            var to = h.transform.position + Vector3.up * 1.2f - Eye; float d = to.magnitude; if (d > maxD) return false;
            // sight cone (wide when close, they "hear" you)
            if (d > 20f && Vector3.Dot(transform.forward, to / d) < 0.2f) return false;
            // smoke blocks sight
            foreach (var s in KryvonThrowables.Smokes) if (s.until > Time.time && DistToSegment(s.p + Vector3.up, Eye, Eye + to) < 4.5f) return false;
            if (Physics.Raycast(Eye, to / d, out var hit, d, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(h.transform) || hit.collider.GetComponent<HitBox>()?.owner == h;
            return true;
        }
        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b) { var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude)); return Vector3.Distance(p, a + ab * t); }

        void Walk(float dt, float speedMul)
        {
            if (!cc.enabled) return;
            var to = goal - transform.position; to.y = 0; float d = to.magnitude;
            if (S == State.Loot && lootGoal && d < 1.6f)
            {
                lootT += dt; if (lootT > 0.8f) { lootT = 0; loots++; ammo = magazine; lootGoal.gameObject.SetActive(false); lootGoal = null; think = 0; }
                cc.SimpleMove(Vector3.zero); return;
            }
            var dir = d > 0.5f ? to / d : Vector3.zero;
            // feelers: steer around walls
            if (dir != Vector3.zero && Physics.SphereCast(transform.position + Vector3.up * 0.9f, 0.3f, dir, out _, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var l = Quaternion.Euler(0, -55, 0) * dir; var r = Quaternion.Euler(0, 55, 0) * dir;
                bool lb = Physics.Raycast(transform.position + Vector3.up * 0.9f, l, 2f), rb = Physics.Raycast(transform.position + Vector3.up * 0.9f, r, 2f);
                dir = !lb ? l : !rb ? r : -dir;
            }
            float speed = (S == State.Move && d > 30f ? 3.8f : 2.6f) * speedMul;   // same pace as the player
            cc.SimpleMove(dir * speed);
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * dt);
            // stuck? pick something else
            stuckT += dt;
            if (stuckT > 3f) { if (Vector3.Distance(transform.position, lastPos) < 1f) { goal = transform.position + Random.insideUnitSphere.normalized * 25f; lootGoal = null; S = State.Move; } stuckT = 0; lastPos = transform.position; }
        }

        void Fight(float dt)
        {
            if (!target || !target.IsAlive) { S = State.Move; target = null; return; }
            var to = target.transform.position - transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * dt);
            // strafe a little, close in if far, back off if very close
            strafeT -= dt; if (strafeT <= 0) { strafeT = 0.8f + Random.value * 1.5f; strafe = Random.value < 0.5f ? -1 : 1; }
            float d = to.magnitude;
            var move = transform.right * strafe * Mathf.Lerp(1.4f, 3.1f, skill) + (d > 45f ? to.normalized * 3f : d < 8f ? -to.normalized * 2f : Vector3.zero);
            if (cc.enabled) cc.SimpleMove(move);

            if (reloadT > 0) { reloadT -= dt; if (reloadT <= 0) ammo = magazine; return; }
            if (reactT > 0) { reactT -= dt; return; }
            fireT -= dt; if (fireT > 0) return;
            if (burstLeft <= 0) { burstLeft = Random.Range(3, 8); fireT = 0.35f + Random.value * 0.5f; return; }
            fireT = 60f / roundsPerMinute; burstLeft--; ammo--;
            if (ammo <= 0) { reloadT = 2.6f; burstLeft = 0; }
            Shoot(d);
        }

        void Shoot(float d)
        {
            var muzzle = Eye + transform.forward * 0.6f - Vector3.up * 0.2f;
            if (shotSound) KryvonAudio.PlayAt(shotSound, muzzle, 0.9f);              // muffled behind walls, duller far away
            if (!Mate) KryvonMinimap.Gunshot(transform.position);
            if (Camera.main && (Camera.main.transform.position - muzzle).sqrMagnitude < 120f * 120f) KryvonFX.MuzzleFlash(muzzle, (target.transform.position + Vector3.up * 1.2f - muzzle).normalized);
            KryvonFX.Tracer(muzzle, target.transform.position + Vector3.up * 1.2f + Random.insideUnitSphere * Mathf.Lerp(0.2f, 2.5f, d / range));
            // hit chance: distance, target moving, target crouched behind cover is handled by the raycast in CanSee
            var mover = target.GetComponent<CharacterController>();
            float moving = mover ? Mathf.Clamp01(mover.velocity.magnitude / 6f) : 0f;
            float p = Mathf.Clamp(0.5f - d / 260f - moving * 0.18f, 0.06f, 0.5f) * Mathf.Lerp(0.6f, 1.4f, skill);
            if (target == player) p *= 0.75f;                 // be fair to the human
            if (Random.value > p || !CanSee(target, range * 1.3f)) return;
            bool head = Random.value < Mathf.Lerp(0.04f, 0.2f, skill);
            target.TakeDamage(new DamageInfo { Amount = damage * 0.5f * (head ? 2.2f : 1f), Headshot = head,   /* bots deal half of what a player deals */ Source = gameObject, Cause = gunName });
        }
    }
}

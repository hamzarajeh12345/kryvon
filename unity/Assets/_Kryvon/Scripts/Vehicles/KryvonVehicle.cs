// Arcade vehicle: F to get in/out, WASD to drive, Space handbrake, Shift boost. Follows the ground, stops on walls.
// The model faces +Z. Uses no Rigidbody so it never tips over or jitters.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class KryvonVehicle : MonoBehaviour
    {
        public static readonly List<KryvonVehicle> All = new();
        public string displayName = "Car";
        [SerializeField] float maxSpeed = 30f, accel = 9f, brake = 22f, reverse = 8f, turnRate = 70f, length = 4.5f, width = 1.9f;
        [SerializeField] Vector3 seat = new(-0.4f, 0.55f, 0.2f);
        [SerializeField] bool lean;                 // motorcycles lean into turns
        [SerializeField] AudioClip engine, crash;
        public float Speed { get; private set; }
        public GameObject Driver { get; private set; }
        AudioSource eng; float yaw, roll; Collider[] own; readonly RaycastHit[] hits = new RaycastHit[8];

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Awake()
        {
            yaw = transform.eulerAngles.y; own = GetComponentsInChildren<Collider>();
            if (engine) { eng = gameObject.AddComponent<AudioSource>(); eng.clip = engine; eng.loop = true; eng.spatialBlend = 1f; eng.volume = 0f; eng.minDistance = 4; eng.maxDistance = 80; eng.Play(); }
        }

        bool IsOwn(Collider c) { foreach (var o in own) if (o == c) return true; return Driver && c.transform.IsChildOf(Driver.transform); }

        public void Enter(GameObject player)
        {
            if (Driver) return;
            Driver = player;
            foreach (var b in new Behaviour[] { player.GetComponent<PlayerController>(), player.GetComponent<WeaponController>() }) if (b) b.enabled = false;
            var cc0 = player.GetComponent<CharacterController>(); if (cc0) cc0.enabled = false;
            player.transform.SetParent(transform, true);
            player.transform.localPosition = seat; player.transform.localRotation = Quaternion.identity;
            SetDriving(player, true);
        }
        public void Exit()
        {
            if (!Driver) return;
            var p = Driver; Driver = null;
            p.transform.SetParent(null, true);
            var side = transform.position - transform.right * (width / 2 + 1.0f);
            if (Physics.Raycast(side + Vector3.up * 3, Vector3.down, out var g, 8f, ~0, QueryTriggerInteraction.Ignore)) side.y = g.point.y + 0.05f;
            p.transform.SetPositionAndRotation(side, Quaternion.Euler(0, transform.eulerAngles.y, 0));
            var cc1 = p.GetComponent<CharacterController>(); if (cc1) cc1.enabled = true;
            foreach (var b in new Behaviour[] { p.GetComponent<PlayerController>(), p.GetComponent<WeaponController>() }) if (b) b.enabled = true;
            SetDriving(p, false);
        }
        static void SetDriving(GameObject p, bool on)
        {
            var an = p.GetComponentInChildren<Animator>(); if (!an) return;
            foreach (var par in an.parameters) if (par.name == "Driving" && par.type == AnimatorControllerParameterType.Bool) an.SetBool("Driving", on);
        }

        void Update()
        {
            float throttle = 0, steer = 0; bool hand = false, boost = false;
            if (Driver)
            {
                var input = Driver.GetComponent<PlayerInputReader>();
                var mv = input ? input.Move : Vector2.zero;
                throttle = mv.y; steer = mv.x;
                var kb = Keyboard.current; hand = kb != null && kb.spaceKey.isPressed; boost = input && input.SprintHeld;
            }
            float top = maxSpeed * (boost ? 1.18f : 1f), dt = Time.deltaTime;
            if (throttle > 0) Speed = Speed < 0 ? Speed + brake * dt : Mathf.Min(top, Speed + accel * throttle * dt * (boost ? 1.3f : 1f));
            else if (throttle < 0) Speed = Speed > 0 ? Speed - brake * dt : Mathf.Max(-reverse, Speed + accel * throttle * dt);
            else Speed = Mathf.MoveTowards(Speed, 0, (Driver ? 3f : 12f) * dt);
            if (hand) Speed = Mathf.MoveTowards(Speed, 0, brake * 1.4f * dt);
            float grip = Mathf.Clamp01(Mathf.Abs(Speed) / 6f);
            yaw += steer * turnRate * grip * Mathf.Sign(Speed) * dt * (hand ? 1.5f : 1f);

            // walls and other vehicles stop us
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward; float step = Speed * dt;
            if (Mathf.Abs(step) > 0.0001f)
            {
                var dir = fwd * Mathf.Sign(step);
                int n = Physics.BoxCastNonAlloc(transform.position + Vector3.up * 0.9f, new Vector3(width / 2, 0.45f, 0.2f), dir, hits, Quaternion.Euler(0, yaw, 0), length / 2 + Mathf.Abs(step), ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (IsOwn(hits[i].collider) || hits[i].collider is TerrainCollider) continue;
                    var h = hits[i].collider.GetComponentInParent<Health>();
                    if (h && Mathf.Abs(Speed) > 8f) h.TakeDamage(new DamageInfo { Amount = Mathf.Abs(Speed) * 4f, Source = Driver, Cause = displayName });
                    if (crash && Mathf.Abs(Speed) > 6f) AudioSource.PlayClipAtPoint(crash, hits[i].point, Mathf.Clamp01(Mathf.Abs(Speed) / 25f));
                    Speed = -Speed * 0.25f; step = 0; break;
                }
            }
            var pos = transform.position + fwd * step;
            // follow the ground: sample front and back for pitch, left and right for roll
            float GroundY(Vector3 p)
            {
                int n = Physics.RaycastNonAlloc(p + Vector3.up * 4f, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MinValue;
                for (int i = 0; i < n; i++) if (!IsOwn(hits[i].collider) && hits[i].point.y < p.y + 2.5f && hits[i].point.y > best) best = hits[i].point.y;
                return best == float.MinValue ? p.y : best;
            }
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            float yf = GroundY(pos + fwd * length * 0.4f), yb = GroundY(pos - fwd * length * 0.4f), yl = GroundY(pos - right * width * 0.4f), yr = GroundY(pos + right * width * 0.4f);
            pos.y = Mathf.Lerp(transform.position.y, (yf + yb + yl + yr) / 4f, 1f - Mathf.Exp(-12f * dt));
            float pitch = -Mathf.Atan2(yf - yb, length * 0.8f) * Mathf.Rad2Deg;
            float rollT = lean ? -steer * 22f * grip : Mathf.Atan2(yl - yr, width * 0.8f) * Mathf.Rad2Deg;
            roll = Mathf.Lerp(roll, rollT, 1f - Mathf.Exp(-6f * dt));
            transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, roll));

            if (eng) { eng.volume = Driver ? 0.35f + 0.4f * Mathf.Abs(Speed) / maxSpeed : 0f; eng.pitch = 0.7f + 1.1f * Mathf.Abs(Speed) / maxSpeed; }
        }
    }
}

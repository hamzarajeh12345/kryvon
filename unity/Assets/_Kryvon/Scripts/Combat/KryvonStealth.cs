// Stealth: how visible a player is (0 = invisible, 1 = fully visible). Bots and name tags read Visibility.
//   Ghillie suit (airdrop): prone/crouched off the road → 15 % visible; otherwise 60 %.
//   Mud camouflage (H, 3 s roll, lasts 90 s): earth-coloured; −30 % next to walls and cover.
//   Shadow cloak (X, back-mounted device, 3 charges): ~30 % visible for 7 s; firing or sprinting breaks it; 45 s cooldown.
//   Shadow masking: indoors and out of the sun → −50 %, and the character is shaded darker.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kryvon
{
    public class KryvonStealth : MonoBehaviour
    {
        public static readonly Dictionary<Health, KryvonStealth> ByHealth = new();
        public static float VisibilityOf(Health h) => h && ByHealth.TryGetValue(h, out var s) ? s.Visibility : 1f;

        [SerializeField] Material ghillieMat, cloakMat;
        [SerializeField] AudioClip cloakHum, cloakBreak, mudSound;
        public bool HasGhillie { get; private set; }
        public int cloakCharges;
        public float Visibility { get; private set; } = 1f;
        public float MudLeft { get; private set; }
        public float CloakLeft { get; private set; }
        public float CloakCooldown { get; private set; }
        public bool Indoors { get; private set; }
        public float RollProgress { get; private set; }      // mud roll 0..1 while rolling

        Health hp; PlayerController body; PlayerInputReader input; AudioSource hum;
        Renderer[] rends; Material[][] originals; readonly List<GameObject> strands = new();
        MaterialPropertyBlock mpb; float rollT = -1, envTick;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), GltfColor = Shader.PropertyToID("baseColorFactor");

        void Awake()
        {
            hp = GetComponent<Health>(); body = GetComponent<PlayerController>(); input = GetComponent<PlayerInputReader>();
            if (hp) ByHealth[hp] = this;
            mpb = new MaterialPropertyBlock();
        }
        void OnDestroy() { if (hp) ByHealth.Remove(hp); }

        Renderer[] Body()
        {
            // the active character model only (not guns, graves, lights)
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r is SkinnedMeshRenderer || r.GetComponentInParent<Animator>()) if (!r.name.StartsWith("Strand")) list.Add(r);
            return list.ToArray();
        }

        // ---------- ghillie ----------
        public void EquipGhillie()
        {
            if (HasGhillie || !ghillieMat) { HasGhillie = true; return; }
            HasGhillie = true;
            var an = GetComponentInChildren<Animator>();
            var bones = new List<(Transform t, int n, float len)>();
            if (an && an.isHuman)
            {
                foreach (var (b, n, l) in new[] { (HumanBodyBones.Head, 26, 0.32f), (HumanBodyBones.UpperChest, 46, 0.55f), (HumanBodyBones.Chest, 40, 0.55f), (HumanBodyBones.Spine, 30, 0.5f),
                    (HumanBodyBones.LeftUpperArm, 16, 0.35f), (HumanBodyBones.RightUpperArm, 16, 0.35f), (HumanBodyBones.LeftUpperLeg, 18, 0.45f), (HumanBodyBones.RightUpperLeg, 18, 0.45f), (HumanBodyBones.Hips, 30, 0.5f) })
                { var t = an.GetBoneTransform(b); if (t) bones.Add((t, n, l)); }
            }
            else bones.Add((transform, 160, 0.6f));
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var rng = new System.Random(name.GetHashCode() ^ (int)(Time.realtimeSinceStartup * 1000));
            foreach (var (t, n, len) in bones)
                for (int i = 0; i < n; i++)
                {
                    var g = new GameObject("Strand"); g.transform.SetParent(t, false);
                    var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.4f - 0.2f;
                    g.transform.localPosition = dir.normalized * 0.11f + Vector3.up * (float)(rng.NextDouble() * 0.12 - 0.06);
                    g.transform.rotation = Quaternion.LookRotation(t.TransformDirection(dir), Vector3.down) * Quaternion.Euler(Random.Range(-30, 30), Random.Range(0, 360), 0);
                    float s = len * (0.6f + (float)rng.NextDouble() * 0.6f);
                    g.transform.localScale = new Vector3(s * 0.35f, s, 1) / Mathf.Max(0.001f, t.lossyScale.x);
                    g.AddComponent<MeshFilter>().sharedMesh = quad;
                    var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = ghillieMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                    strands.Add(g);
                }
        }

        // ---------- cloak ----------
        void Cloak(bool on)
        {
            rends ??= Body();
            if (on)
            {
                if (!cloakMat) return;
                originals = new Material[rends.Length][];
                for (int i = 0; i < rends.Length; i++) { originals[i] = rends[i].sharedMaterials; var m = new Material[originals[i].Length]; for (int k = 0; k < m.Length; k++) m[k] = cloakMat; rends[i].sharedMaterials = m; }
                foreach (var s in strands) s.SetActive(false);
                if (cloakHum) { hum ??= gameObject.AddComponent<AudioSource>(); hum.clip = cloakHum; hum.loop = true; hum.spatialBlend = 1; hum.volume = 0.35f; hum.pitch = 2.2f; hum.Play(); }
                CloakLeft = 7f; cloakCharges--;
            }
            else
            {
                if (originals != null) for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].sharedMaterials = originals[i];
                originals = null; foreach (var s in strands) s.SetActive(true);
                if (hum) hum.Stop();
                if (CloakLeft > 0.2f && cloakBreak) AudioSource.PlayClipAtPoint(cloakBreak, transform.position, 0.8f);
                CloakLeft = 0; CloakCooldown = 45f;
            }
        }

        void Update()
        {
            var kb = Keyboard.current; float dt = Time.deltaTime;
            bool driving = body && !body.enabled;

            // mud roll
            if (kb != null && kb.hKey.wasPressedThisFrame && rollT < 0 && !driving && body && body.IsGrounded) { rollT = 0; if (mudSound) AudioSource.PlayClipAtPoint(mudSound, transform.position, 0.7f); }
            if (rollT >= 0)
            {
                rollT += dt; RollProgress = rollT / 3f;
                if (body) body.SpeedMultiplier = 0.1f;
                if (driving || (input && input.FirePressed)) { rollT = -1; RollProgress = 0; }
                else if (rollT >= 3f) { rollT = -1; RollProgress = 0; MudLeft = 90f; KryvonFX.Dust(transform.position + Vector3.up * 0.4f); }
            }
            if (MudLeft > 0) MudLeft -= dt;

            // shadow cloak
            if (CloakCooldown > 0) CloakCooldown -= dt;
            if (kb != null && kb.xKey.wasPressedThisFrame && CloakLeft <= 0 && CloakCooldown <= 0 && cloakCharges > 0 && !driving) Cloak(true);
            if (CloakLeft > 0)
            {
                CloakLeft -= dt;
                bool broke = input && (input.FirePressed || input.FireHeld || input.SprintHeld);
                if (broke || CloakLeft <= 0) Cloak(false);
            }

            // environment checks a few times a second
            envTick -= dt;
            if (envTick <= 0)
            {
                envTick = 0.25f;
                var p = transform.position + Vector3.up * 1.2f;
                bool roof = Physics.Raycast(p, Vector3.up, 14f, ~0, QueryTriggerInteraction.Ignore);
                var sun = RenderSettings.sun ? -RenderSettings.sun.transform.forward : Vector3.up;
                bool shaded = Physics.Raycast(p, sun, 80f, ~0, QueryTriggerInteraction.Ignore);
                Indoors = roof && shaded;
            }

            // visibility
            float v = 1f;
            bool low = body && body.Stance != Stance.Stand;
            bool onRoad = false;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var g, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            { var n = g.collider.name.ToLowerInvariant(); onRoad = n.Contains("street") || n.Contains("road") || n.Contains("asphalt"); }
            if (HasGhillie) v *= low && !onRoad && !Indoors ? 0.15f : 0.6f;
            if (MudLeft > 0 && Physics.CheckSphere(transform.position + Vector3.up * 0.8f, 1.6f, ~0, QueryTriggerInteraction.Ignore) && NearCover()) v *= 0.7f;
            if (Indoors) v *= 0.5f;
            if (CloakLeft > 0) v *= 0.3f;
            if (input && input.SprintHeld) v = Mathf.Min(1f, v * 1.4f);
            if (input && input.FireHeld) v = Mathf.Max(v, 0.85f);     // muzzle flash gives you away
            Visibility = Mathf.Lerp(Visibility, v, 1f - Mathf.Exp(-6f * dt));

            // look: mud colour and indoor shading on the body (not while cloaked)
            if (CloakLeft <= 0)
            {
                rends ??= Body();
                var tint = Color.white;
                if (MudLeft > 0) tint *= new Color(0.62f, 0.5f, 0.38f);
                if (Indoors) tint *= 0.55f;
                tint.a = 1;
                foreach (var r in rends)
                {
                    if (!r) continue;
                    r.GetPropertyBlock(mpb); mpb.SetColor(BaseColor, tint); mpb.SetColor(GltfColor, tint); r.SetPropertyBlock(mpb);
                }
            }
        }

        bool NearCover()
        {
            var c = Physics.OverlapSphere(transform.position + Vector3.up * 0.8f, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var x in c) if (!x.transform.IsChildOf(transform) && !(x is TerrainCollider)) return true;
            return false;
        }
    }
}

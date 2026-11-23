// Emotes: B (or the ☺ touch button) plays the equipped emote for 4 s when standing still on the ground.
// Procedural – the humanoid bones are posed on top of the idle animation (runs after the gun-hold pose), the gun is hidden.
//   SALUTE · DABKE (linked arms, stomp-kick) · SWORD DANCE (arm raised, sway) · ARABIC COFFEE (pour + offer) · NASHAMA CHEER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Kryvon
{
    [DefaultExecutionOrder(1000)]
    public class KryvonEmote : MonoBehaviour
    {
        public static KryvonEmote Mine;
        public bool Playing => t >= 0;
        float t = -1; string id; bool tap; Animator an; PlayerController pc; readonly List<Renderer> hid = new();
        public void Touch() => tap = true;
        const float Len = 4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Hook(); SceneManager.sceneLoaded += (_, _) => Hook(); }
        static void Hook()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p && p.GetComponent<PlayerController>() && !p.GetComponent<KryvonEmote>()) Mine = p.AddComponent<KryvonEmote>();
        }

        void Start() { an = GetComponentInChildren<Animator>(); pc = GetComponent<PlayerController>(); }

        void Update()
        {
            var kb = Keyboard.current; bool press = (kb != null && kb.bKey.wasPressedThisFrame) || tap; tap = false;
            bool canPlay = pc && pc.IsGrounded && !pc.IsMoving && !KryvonDrop.Active;
            if (press && !Playing && canPlay) Begin();
            else if (Playing && ((pc && pc.IsMoving) || (t += Time.deltaTime) > Len)) Stop();
        }

        void Begin()
        {
            var c = KryvonSkins.Equipped(CosKind.Emote) ?? KryvonSkins.Get("emote_salute"); id = c != null ? c.id : "emote_salute"; t = 0;
            hid.Clear();
            foreach (var tr in GetComponentsInChildren<Transform>()) if (tr.name.StartsWith("Gun_")) foreach (var r in tr.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hid.Add(r); }
        }
        void Stop() { t = -1; foreach (var r in hid) if (r) r.enabled = true; hid.Clear(); }
        void OnDisable() => Stop();

        void Rot(HumanBodyBones b, Vector3 euler, float w)
        {
            var tr = an.GetBoneTransform(b); if (!tr) return;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, tr.localRotation * Quaternion.Euler(euler), w);
        }

        void LateUpdate()
        {
            if (!Playing || !an || !an.isHuman) return;
            float w = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((Len - t) * 4f);      // blend in / out
            float s = Mathf.Sin(t * Mathf.PI * 2f * 1.6f), c2 = Mathf.Sin(t * Mathf.PI * 2f * 3.2f);
            switch (id)
            {
                case "emote_dabke":   // shoulders level, arms out to the neighbours, stomp-kick on the beat
                    Rot(HumanBodyBones.LeftUpperArm, new Vector3(0, 0, 55), w); Rot(HumanBodyBones.RightUpperArm, new Vector3(0, 0, -55), w);
                    Rot(HumanBodyBones.LeftLowerArm, new Vector3(0, -30, 0), w); Rot(HumanBodyBones.RightLowerArm, new Vector3(0, 30, 0), w);
                    Rot(HumanBodyBones.RightUpperLeg, new Vector3(-Mathf.Max(0, s) * 55f, 0, 0), w); Rot(HumanBodyBones.RightLowerLeg, new Vector3(Mathf.Max(0, s) * 60f, 0, 0), w);
                    Rot(HumanBodyBones.LeftUpperLeg, new Vector3(-Mathf.Max(0, -s) * 25f, 0, 0), w);
                    Rot(HumanBodyBones.Spine, new Vector3(0, s * 8f, 0), w); break;
                case "emote_ardah":   // right arm high (the sword), left on the chest, the body sways to the drum
                    Rot(HumanBodyBones.RightUpperArm, new Vector3(0, 0, -150 + c2 * 10f), w); Rot(HumanBodyBones.RightLowerArm, new Vector3(0, 0, -15), w);
                    Rot(HumanBodyBones.LeftUpperArm, new Vector3(-20, 0, 40), w); Rot(HumanBodyBones.LeftLowerArm, new Vector3(0, -110, 0), w);
                    Rot(HumanBodyBones.Spine, new Vector3(0, 0, s * 9f), w); Rot(HumanBodyBones.Head, new Vector3(-10, s * 10f, 0), w); break;
                case "emote_dallah":  // left hand holds the cup, the right tilts the pot, then both offer forward
                    float pour = Mathf.Clamp01(t / 2.5f);
                    Rot(HumanBodyBones.RightUpperArm, new Vector3(-60, 0, -20), w); Rot(HumanBodyBones.RightHand, new Vector3(0, 0, -50 * Mathf.Sin(pour * Mathf.PI)), w);
                    Rot(HumanBodyBones.LeftUpperArm, new Vector3(-50, 0, 20), w); Rot(HumanBodyBones.LeftLowerArm, new Vector3(0, -40, 0), w);
                    Rot(HumanBodyBones.Spine, new Vector3(t > 2.5f ? 15f : 0f, 0, 0), w); break;
                case "emote_victory": // both arms up, little jumps of joy
                    Rot(HumanBodyBones.LeftUpperArm, new Vector3(0, 0, 150 + c2 * 10f), w); Rot(HumanBodyBones.RightUpperArm, new Vector3(0, 0, -150 - c2 * 10f), w);
                    Rot(HumanBodyBones.Head, new Vector3(-15, 0, 0), w); Rot(HumanBodyBones.Spine, new Vector3(-8 * Mathf.Abs(c2), 0, 0), w); break;
                default:              // salute: right hand to the brow
                    Rot(HumanBodyBones.RightUpperArm, new Vector3(-30, 40, -95), w); Rot(HumanBodyBones.RightLowerArm, new Vector3(0, 130, 0), w);
                    Rot(HumanBodyBones.RightHand, new Vector3(0, 0, -20), w); break;
            }
        }
    }
}

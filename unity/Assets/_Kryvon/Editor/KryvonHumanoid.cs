// Humanoid retargeting: imports the Quaternius (CC0) animation library as Humanoid, builds a Humanoid avatar for
// Hyper3D characters (Mixamo-style bone names), and a two-layer controller: legs = locomotion, upper body = weapon.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Kryvon.EditorTools
{
    class KryvonUALImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("ThirdParty/Quaternius")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
        }
        void OnPreprocessAnimation()
        {
            if (!assetPath.Contains("ThirdParty/Quaternius")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                bool loop = c.name.Contains("Loop") || c.name.EndsWith("Idle");
                c.loopTime = loop; c.loopPose = loop;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;   // in place
                c.keepOriginalOrientation = false; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = false;
            }
            mi.clipAnimations = clips;
        }
    }

    public static class KryvonHumanoid
    {
        const string Root = "Assets/_Kryvon";
        const string UAL = Root + "/ThirdParty/Quaternius/UAL1_Standard.fbx";

        static readonly (string human, string bone)[] Map =
        {
            ("Hips","Hips"),("Spine","Spine"),("Chest","Spine1"),("UpperChest","Spine2"),("Neck","Neck"),("Head","Head"),
            ("LeftShoulder","LeftShoulder"),("LeftUpperArm","LeftArm"),("LeftLowerArm","LeftForeArm"),("LeftHand","LeftHand"),
            ("RightShoulder","RightShoulder"),("RightUpperArm","RightArm"),("RightLowerArm","RightForeArm"),("RightHand","RightHand"),
            ("LeftUpperLeg","LeftUpLeg"),("LeftLowerLeg","LeftLeg"),("LeftFoot","LeftFoot"),("LeftToes","LeftToeBase"),
            ("RightUpperLeg","RightUpLeg"),("RightLowerLeg","RightLeg"),("RightFoot","RightFoot"),("RightToes","RightToeBase"),
        };

        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(UAL) != null;

        public static Dictionary<string, AnimationClip> Clips() =>
            AssetDatabase.LoadAllAssetsAtPath(UAL).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name).ToDictionary(g => g.Key, g => g.First());

        /// builds (or loads) a humanoid avatar for a Hyper3D character glb, from a clean unscaled instance
        public static Avatar BuildAvatar(string ch)
        {
            var path = $"{Root}/Animations/{ch}_Avatar.asset";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Characters/{ch}.glb");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
            var anim = inst.GetComponentInChildren<Animator>();
            var root = anim ? anim.gameObject : inst;
            var all = root.GetComponentsInChildren<Transform>();
            EnforceTPose(root.transform, all);
            var desc = new HumanDescription
            {
                human = Map.Where(m => all.Any(t => t.name == m.bone)).Select(m => new HumanBone { humanName = m.human, boneName = m.bone, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = all.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f, armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(root, desc);
            Object.DestroyImmediate(inst);
            if (!avatar.isValid || !avatar.isHuman) { Debug.LogWarning($"KRYVON: humanoid avatar for {ch} invalid"); return null; }
            avatar.name = ch + "_Avatar";
            System.IO.Directory.CreateDirectory(Root + "/Animations");
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(avatar, path);
            return avatar;
        }

        // Humanoid avatars must be described in a T-pose: straighten arms out to the sides, legs straight down, spine up
        static void EnforceTPose(Transform root, Transform[] all)
        {
            Transform F(string n) => all.FirstOrDefault(t => t.name == n);
            void Aim(Transform bone, Transform child, Vector3 dir)
            {
                if (bone == null || child == null) return;
                var cur = child.position - bone.position; if (cur.sqrMagnitude < 1e-8f) return;
                bone.rotation = Quaternion.FromToRotation(cur, dir) * bone.rotation;
            }
            Vector3 right = root.right, up = root.up;
            Aim(F("Hips"), F("Spine"), up); Aim(F("Spine"), F("Spine1"), up); Aim(F("Spine1"), F("Spine2"), up); Aim(F("Spine2"), F("Neck"), up); Aim(F("Neck"), F("Head"), up);
            foreach (var (side, dir) in new[] { ("Left", -right), ("Right", right) })
            {
                Aim(F(side + "Shoulder"), F(side + "Arm"), dir);
                Aim(F(side + "Arm"), F(side + "ForeArm"), dir);
                Aim(F(side + "ForeArm"), F(side + "Hand"), dir);
                Aim(F(side + "Hand"), F(side + "HandMiddle1"), dir);
                Aim(F(side + "UpLeg"), F(side + "Leg"), -up);
                Aim(F(side + "Leg"), F(side + "Foot"), -up);
            }
        }

        static AvatarMask UpperMask()
        {
            var path = $"{Root}/Animations/UpperBody.mask";
            AssetDatabase.DeleteAsset(path);
            var m = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) m.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (var p in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                m.SetHumanoidBodyPartActive(p, true);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static RuntimeAnimatorController BuildController()
        {
            var c = Clips(); if (c.Count == 0) return null;
            AnimationClip C(string n) => c.TryGetValue(n, out var x) ? x : null;
            var path = $"{Root}/Animations/HumanoidSoldier.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var (n, t) in new[] { ("Speed", AnimatorControllerParameterType.Float), ("Stance", AnimatorControllerParameterType.Int), ("Dead", AnimatorControllerParameterType.Bool),
                ("Knocked", AnimatorControllerParameterType.Bool), ("Aiming", AnimatorControllerParameterType.Bool), ("Sprinting", AnimatorControllerParameterType.Bool),
                ("Jump", AnimatorControllerParameterType.Trigger), ("Fire", AnimatorControllerParameterType.Trigger), ("Reload", AnimatorControllerParameterType.Trigger), ("Driving", AnimatorControllerParameterType.Bool) })
                ctrl.AddParameter(n, t);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });

            // ---- base layer: legs and whole body ----
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState Tree(string name, params (AnimationClip m, float at)[] kids)
            {
                var st = ctrl.CreateBlendTreeInController(name, out var tree, 0);
                tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
                foreach (var k in kids) if (k.m) tree.AddChild(k.m, k.at);
                return st;
            }
            var loco = Tree("Locomotion", (C("Idle_Loop"), 0f), (C("Walk_Loop"), 1.9f), (C("Jog_Fwd_Loop"), 4.8f), (C("Sprint_Loop"), 6.4f));
            var crouch = Tree("Crouch", (C("Crouch_Idle_Loop"), 0f), (C("Crouch_Fwd_Loop"), 2.4f));
            var air = sm.AddState("Air"); air.motion = C("Jump_Loop");
            var land = sm.AddState("Land"); land.motion = C("Jump_Land");
            var dead = sm.AddState("Dead"); dead.motion = C("Death01");
            var drive = sm.AddState("Drive"); drive.motion = C("Driving_Loop");
            sm.defaultState = loco;
            AnimatorStateTransition T(AnimatorState a, AnimatorState b, float d = 0.18f) { var t = a.AddTransition(b); t.hasExitTime = false; t.duration = d; return t; }
            T(loco, crouch, 0.25f).AddCondition(AnimatorConditionMode.NotEqual, 0, "Stance");
            T(crouch, loco, 0.25f).AddCondition(AnimatorConditionMode.Equals, 0, "Stance");
            foreach (var st in new[] { loco, crouch }) T(st, air, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Jump");
            T(air, land, 0.08f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var back = land.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.6f; back.duration = 0.15f;
            var d1 = sm.AddAnyStateTransition(dead); d1.AddCondition(AnimatorConditionMode.If, 0, "Dead"); d1.canTransitionToSelf = false; d1.duration = 0.15f;
            var dr = sm.AddAnyStateTransition(drive); dr.AddCondition(AnimatorConditionMode.If, 0, "Driving"); dr.canTransitionToSelf = false;
            T(drive, loco).AddCondition(AnimatorConditionMode.IfNot, 0, "Driving");

            // ---- upper body: always holding the weapon ----
            ctrl.AddLayer(new AnimatorControllerLayer { name = "UpperBody", defaultWeight = 1f, avatarMask = UpperMask(), stateMachine = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy } });
            var layers = ctrl.layers; var up = layers[1].stateMachine; AssetDatabase.AddObjectToAsset(up, ctrl);
            var ready = up.AddState("Ready"); ready.motion = C("Pistol_Idle_Loop");
            var aim = up.AddState("Aim"); aim.motion = C("Pistol_Aim_Neutral");
            var shoot = up.AddState("Shoot"); shoot.motion = C("Pistol_Shoot"); shoot.speed = 1.6f;
            var reload = up.AddState("Reload"); reload.motion = C("Pistol_Reload");
            var run = up.AddState("Sprint"); run.motion = C("Sprint_Loop");
            up.defaultState = ready;
            T(ready, aim, 0.12f).AddCondition(AnimatorConditionMode.If, 0, "Aiming");
            T(aim, ready, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Aiming");
            foreach (var st in new[] { ready, aim, run }) { T(st, shoot, 0.04f).AddCondition(AnimatorConditionMode.If, 0, "Fire"); T(st, reload, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Reload"); }
            var sb = shoot.AddTransition(aim); sb.hasExitTime = true; sb.exitTime = 0.8f; sb.duration = 0.1f;
            var rb = reload.AddTransition(ready); rb.hasExitTime = true; rb.exitTime = 0.95f; rb.duration = 0.15f;
            T(ready, run, 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Sprinting");
            T(run, ready, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Sprinting");
            ctrl.layers = layers;
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        /// pose an Animator with a clip at time t in edit mode (works for humanoid clips, unlike AnimationClip.SampleAnimation)
        public static void Pose(Animator an, AnimationClip clip, float t)
        {
            if (an == null || clip == null) return;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            var graph = PlayableGraph.Create("KryvonPose");
            var output = AnimationPlayableOutput.Create(graph, "pose", an);
            var cp = AnimationClipPlayable.Create(graph, clip); cp.SetApplyFootIK(true);
            output.SetSourcePlayable(cp);
            AnimationMode.BeginSampling();
            AnimationMode.SamplePlayableGraph(graph, 0, t);
            AnimationMode.EndSampling();
            graph.Destroy();
        }
        /// leave animation-mode sampling: bones go back to the bind pose
        public static void EndPose() { if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); }

        public static void ReimportUAL()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(UAL);
            mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                bool loop = c.name.Contains("Loop") || c.name.EndsWith("Idle");
                c.loopTime = loop; c.loopPose = loop; c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = false; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = false;
            }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
            var probe = Clips().Values.FirstOrDefault();
            Debug.Log($"KRYVON: UAL reimported, human motion = {(probe != null && probe.isHumanMotion)}");
        }

        public static void Diagnose()
        {
            var sb = new System.Text.StringBuilder();
            var player = GameObject.Find("Player"); var ans = player.GetComponentsInChildren<Animator>();
            sb.AppendLine("animators: " + ans.Length);
            foreach (var an in ans) sb.AppendLine($" {an.name} avatar={(an.avatar ? an.avatar.name : "null")} human={(an.avatar && an.avatar.isHuman)} valid={(an.avatar && an.avatar.isValid)} ctrl={(an.runtimeAnimatorController ? an.runtimeAnimatorController.name : "null")} enabled={an.enabled} armR={an.GetBoneTransform(HumanBodyBones.RightUpperArm)?.name}");
            var c = Clips(); sb.AppendLine("names: " + string.Join(", ", c.Keys)); sb.AppendLine("clips: " + c.Count + " aim human=" + (c.TryGetValue("Pistol_Aim_Neutral", out var aim) && aim.isHumanMotion));
            var a0 = ans[0]; var arm = a0.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (arm && aim) { var r0 = arm.rotation; Pose(a0, aim, 0.1f); sb.AppendLine("arm rot change deg: " + Quaternion.Angle(r0, arm.rotation)); EndPose(); sb.AppendLine("after end: " + Quaternion.Angle(r0, arm.rotation)); }
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "diag.txt"), sb.ToString());
        }

        public static void CapturePoses()
        {
            var player = GameObject.Find("Player"); var an = player.GetComponentInChildren<Animator>(); var cam = Camera.main; var c = Clips();
            var dir = System.IO.Directory.GetCurrentDirectory(); float fov = cam.fieldOfView; cam.fieldOfView = 40;
            var p = player.transform.position;
            foreach (var (clip, t, name, off) in new[] { ("Pistol_Idle_Loop", 0.3f, "Pose_ready", new Vector3(2.2f, 1.4f, 2.4f)), ("Pistol_Aim_Neutral", 0.1f, "Pose_aim", new Vector3(2.4f, 1.5f, 1.2f)),
                ("Jog_Fwd_Loop", 0.25f, "Pose_run", new Vector3(2.8f, 1.3f, 0.5f)), ("Crouch_Fwd_Loop", 0.3f, "Pose_crouch", new Vector3(2.5f, 1.1f, 1.5f)) })
            {
                if (c.TryGetValue(clip, out var cl)) Pose(an, cl, t);
                cam.transform.position = p + off; cam.transform.LookAt(p + Vector3.up * 1.0f);
                KryvonShowcase.Capture(System.IO.Path.Combine(dir, name + ".png"), 960, 960);
            }
            EndPose();
            cam.fieldOfView = fov;
        }

        /// gun in the right hand, aligned with the character's aim direction while posed in the pistol aim
        public static GameObject AttachGunAimed(GameObject model, Animator an, string gun, float length)
        {
            var clips = Clips(); var aimClip = clips.TryGetValue("Pistol_Aim_Neutral", out var a) ? a : null;
            var hand = an.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return null;
            if (aimClip) Pose(an, aimClip, 0.1f);
            var pivot = KryvonPlay.AttachGun(model, gun, length);   // aligns to the character's forward at the hand's current (aimed) pose
            EndPose();
            return pivot;
        }
    }
}

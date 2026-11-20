using System;
using System.Linq;
using UnityEngine;

namespace Riftbound
{
    [DefaultExecutionOrder(850)]
    public sealed class ProceduralActorVisual : MonoBehaviour
    {
        [SerializeField] GameObject model;
        [SerializeField] Animator source;
        [SerializeField] float heightScale = 1f;

        [SerializeField] Transform stagedDisplay;

        Transform display, pelvis;
        Animator displayAnimator;
        Joint[] joints;
        Wrist[] wrists;
        Vector3 previousPosition;
        Vector3 pelvisRest, sourcePelvisRest;
        bool movingParameter;
        Renderer[] originals;

        // Parent joints come first so each child receives the current parent pose.
        static readonly (string bone, string end, HumanBodyBones human, HumanBodyBones humanEnd)[] Body = {
            ("hips", "spine", HumanBodyBones.Hips, HumanBodyBones.Spine),
            ("spine", "head", HumanBodyBones.Spine, HumanBodyBones.Head),
            ("upper_arm.R", "forearm.R", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            ("forearm.R", "hand.R", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            ("upper_arm.L", "forearm.L", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            ("forearm.L", "hand.L", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            ("thigh.R", "shin.R", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
            ("shin.R", "foot.R", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            ("thigh.L", "shin.L", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
            ("shin.L", "foot.L", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot)
        };

        struct Joint
        {
            public Transform from, to, target;
            public Quaternion restRotation;
            public Vector3 axis;
        }

        struct Wrist
        {
            public Joint forearm;
            public Transform hand;
            public Quaternion driverRest, handRest, driverToKit;

            public void Copy()
            {
                var current = Quaternion.Inverse(forearm.from.rotation) * forearm.to.rotation;
                var movement = current * Quaternion.Inverse(driverRest);
                hand.localRotation = driverToKit * movement * Quaternion.Inverse(driverToKit) * handRest;
            }
        }

        public static ProceduralActorVisual Attach(GameObject owner, Animator driver, GameObject prefab, float scale)
        {
            if (!owner || !prefab) return null;
            var visual = owner.AddComponent<ProceduralActorVisual>();
            visual.Configure(driver, prefab, scale);
            visual.CreateDisplay();
            return visual;
        }

        public void Configure(Animator driver, GameObject prefab, float scale)
        {
            source = driver;
            model = prefab;
            heightScale = scale;
        }

        public Animator Driver => source;
        public Transform StagedDisplay => stagedDisplay;

#if UNITY_EDITOR
        public void StageDisplay(Transform preview)
        {
            stagedDisplay = preview;
            var parent = source ? source.transform : transform;
            var originalMeshes = FindOriginalMeshes(parent);
            SetDisplayTransform(preview, parent);
            foreach (var renderer in originalMeshes)
            {
                if (renderer.transform.IsChildOf(preview)) continue;
                renderer.enabled = false;
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
#endif

        void Start() => CreateDisplay();

        void CreateDisplay()
        {
            if (!model || display) return;
            // Keep the driver underneath its existing owner. That preserves state, foot planting,
            // strafe orientation and network animation even when its meshes are hidden.
            var parent = source ? source.transform : transform;
            originals = FindOriginalMeshes(parent);
            var oldBones = source ? source.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>();
            var instance = stagedDisplay ? stagedDisplay.gameObject : Instantiate(model, parent, false);
            instance.name = "Emberford Body";
            display = instance.transform;
            if (!stagedDisplay) SetDisplayTransform(display, parent);
            foreach (var npc in instance.GetComponentsInChildren<TownNPC>(true))
            {
                npc.enabled = false;
                if (Application.isPlaying) Destroy(npc);
                else DestroyImmediate(npc);
            }
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!skin)
            {
                if (Application.isPlaying) Destroy(instance);
                else DestroyImmediate(instance);
                return;
            }

            displayAnimator = instance.GetComponentInChildren<Animator>();
            var bindings = Body.Select(pair =>
            {
                var first = FindBone(source, oldBones, pair.bone, pair.human);
                var second = FindBone(source, oldBones, pair.end, pair.humanEnd);
                var target = Array.Find(skin.bones, b => b && b.name == pair.bone);
                var end = Array.Find(skin.bones, b => b && b.name == pair.end);
                return first && second && target && end && first != second
                    ? new Joint { from = first, to = second, target = target, restRotation = target.localRotation,
                        axis = target.InverseTransformDirection(end.position - target.position).normalized }
                    : default;
            }).Where(j => j.target).ToArray();
            if (bindings.Length >= 8 && source.runtimeAnimatorController)
            {
                joints = bindings;
                wrists = bindings.Where(j => j.target.name.StartsWith("forearm.", StringComparison.Ordinal))
                    .Select(j =>
                    {
                        var hand = Array.Find(skin.bones, b => b && b.parent == j.target && b.name.StartsWith("hand.", StringComparison.Ordinal));
                        if (!hand) return default;
                        var align = Quaternion.FromToRotation(j.to.position - j.from.position, hand.position - j.target.position);
                        return new Wrist
                        {
                            forearm = j, hand = hand, handRest = hand.localRotation,
                            driverRest = Quaternion.Inverse(j.from.rotation) * j.to.rotation,
                            driverToKit = Quaternion.Inverse(j.target.rotation) * align * j.from.rotation
                        };
                    }).Where(w => w.hand).ToArray();
                if (displayAnimator) displayAnimator.enabled = false;
                source.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (bindings[0].target.name == "hips")
                {
                    pelvis = bindings[0].target;
                    pelvisRest = pelvis.localPosition;
                    sourcePelvisRest = source.transform.InverseTransformPoint(bindings[0].from.position);
                }
            }
            movingParameter = displayAnimator && displayAnimator.parameters.Any(
                p => p.type == AnimatorControllerParameterType.Bool && p.name == "Moving");
            foreach (var renderer in originals) renderer.forceRenderingOff = true;
            previousPosition = transform.position;
            CopyPose();
        }

        Renderer[] FindOriginalMeshes(Transform parent) =>
            parent.GetComponentsInChildren<Renderer>(true)
                .Where(r => (!stagedDisplay || !r.transform.IsChildOf(stagedDisplay)) &&
                    (r is SkinnedMeshRenderer || r is MeshRenderer && !r.GetComponent<TextMesh>())).ToArray();

        void SetDisplayTransform(Transform body, Transform parent)
        {
            body.SetParent(parent, false);
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            var scale = parent.lossyScale;
            float size = Mathf.Max(.35f, heightScale);
            body.localScale = new Vector3(size / Mathf.Max(.01f, Mathf.Abs(scale.x)),
                size / Mathf.Max(.01f, Mathf.Abs(scale.y)), size / Mathf.Max(.01f, Mathf.Abs(scale.z)));
            foreach (var child in body.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = parent.gameObject.layer;

            // The imported skeleton's hips provide a facing axis independent of FBX axis conversion.
            var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!skin) return;
            var right = Array.Find(skin.bones, b => b && b.name == "thigh.R");
            var left = Array.Find(skin.bones, b => b && b.name == "thigh.L");
            if (!right || !left) return;
            var forward = Vector3.ProjectOnPlane(Vector3.Cross(right.position - left.position, parent.up), parent.up);
            if (forward.sqrMagnitude > .001f)
                body.localRotation = Quaternion.Euler(0, Vector3.SignedAngle(forward, parent.forward, parent.up), 0);
        }
        static Transform FindBone(Animator animator, Transform[] bones, string kitName, HumanBodyBones body)
        {
            if (!animator) return null;
            if (animator.isHuman) return animator.GetBoneTransform(body);
            string human = body.ToString();
            string alternate = kitName switch
            {
                "hips" => "pelvis", "spine" => "chest",
                "upper_arm.R" => "rightarm", "upper_arm.L" => "leftarm",
                "forearm.R" => "rightforearm", "forearm.L" => "leftforearm",
                "thigh.R" => "rightupleg", "thigh.L" => "leftupleg",
                "shin.R" => "rightleg", "shin.L" => "leftleg",
                _ => kitName.Replace(".", "")
            };
            return Array.Find(bones, t =>
            {
                string clean = t.name.Substring(t.name.LastIndexOf(':') + 1).Replace("_", "").Replace(".", "");
                return string.Equals(clean, human, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(clean, alternate, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t.name, kitName, StringComparison.OrdinalIgnoreCase);
            });
        }

        void LateUpdate()
        {
            if (!display) return;
            if (joints != null) CopyPose();
            else if (movingParameter)
            {
                var position = transform.position;
                var delta = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
                displayAnimator.SetBool("Moving", delta.sqrMagnitude > Mathf.Pow(Time.deltaTime * .12f, 2));
                previousPosition = position;
            }
        }

        void CopyPose()
        {
            if (joints == null || !source) return;
            if (pelvis)
            {
                var current = source.transform.InverseTransformPoint(joints[0].from.position);
                var displacement = source.transform.TransformVector(current - sourcePelvisRest);
                pelvis.localPosition = pelvisRest + pelvis.parent.InverseTransformVector(Vector3.ClampMagnitude(displacement, .65f));
            }
            foreach (var joint in joints)
            {
                if (!joint.from || !joint.to) continue;
                var direction = joint.to.position - joint.from.position;
                joint.target.localRotation = joint.restRotation;
                if (direction.sqrMagnitude > .0001f)
                    joint.target.rotation = Quaternion.FromToRotation(joint.target.rotation * joint.axis, direction) * joint.target.rotation;
            }
            foreach (var wrist in wrists) wrist.Copy();
        }
    }
}

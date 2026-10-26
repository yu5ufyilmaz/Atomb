using UnityEngine;

namespace StarterAssets
{
    // Opt-in on the laboratory copy. Authored interaction states never enter this solver.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator), typeof(CharacterController))]
    public sealed class PlayerFootwork : MonoBehaviour
    {
        [Header("Ground contact (world metres)")]
        [Range(.1f, .4f)] public float groundReach = .3f;
        [Range(0, .25f)] public float pelvisDropLimit = .2f;
        [Header("Standing turns")]
        [Range(60, 180)] public float standingTurnSpeed = 120;
        [Range(.2f, .5f)] public float stepDuration = .28f;
        [Range(.03f, .12f)] public float stepHeight = .06f;

        private static readonly int Locomotion = Animator.StringToHash("Base Layer.Blend Tree");
        private Animator animator;
        private CharacterController movement;
        private UnityEngine.CharacterController capsule;
        private StarterAssetsInputs inputs;
        private Foot left, right, swinging;
        private Vector3 previousPosition;
        private float previousYaw, pelvisOffset;
        private bool calibrated, tracking;
        private Foot nextFoot;

        public bool IsApplying { get; private set; }
        public float LeftSoleOffset => left != null ? left.sole : 0;
        public float RightSoleOffset => right != null ? right.sole : 0;
        public bool CanApply
        {
            get
            {
                var game = GameManager.Instance;
                if (!isActiveAndEnabled || animator == null || !animator.enabled || !animator.isHuman
                    || animator.speed <= 0 || movement == null || !movement.enabled || movement.IsFrozen
                    || !capsule.enabled || !movement.Grounded || game == null || !game.isGameStarted
                    || game.isGamePaused || game.activeInteraction != null) return false;
                return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Locomotion
                    && (!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash == Locomotion);
            }
        }

        private sealed class Foot
        {
            public AvatarIKGoal goal;
            public Transform bone, hip;
            public float sole, restHeight, length, weight, plantYaw, phase;
            public Vector3 animated, animatedSupport, goalOffset, anchor, start, landing, normal, anchorNormal, landingNormal;
            public Quaternion animatedRotation, goalRotation, rotation, startRotation, landingRotation;
            public bool supported, animatedSupported, planted;
            public Vector3 support;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>(); movement = GetComponent<CharacterController>();
            capsule = GetComponent<UnityEngine.CharacterController>(); inputs = GetComponent<StarterAssetsInputs>();
            if (!animator.isHuman) { enabled = false; return; }
            left = CreateFoot(AvatarIKGoal.LeftFoot, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, animator.leftFeetBottomHeight);
            right = CreateFoot(AvatarIKGoal.RightFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, animator.rightFeetBottomHeight);
            nextFoot = right;
        }

        private Foot CreateFoot(AvatarIKGoal goal, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones ankle, float sole)
        {
            var hip = animator.GetBoneTransform(upper); var knee = animator.GetBoneTransform(lower);
            var bone = animator.GetBoneTransform(ankle);
            return new Foot { goal = goal, bone = bone, hip = hip,
                sole = sole * Mathf.Abs(transform.lossyScale.y),
                restHeight = bone.position.y - transform.position.y,
                length = Vector3.Distance(hip.position, knee.position) + Vector3.Distance(knee.position, bone.position) };
        }

        private void Update() { if (!CanApply) ResetContact(); }
        private void OnDisable() => ResetContact();

        private void ResetContact()
        {
            IsApplying = tracking = false; swinging = null; pelvisOffset = 0;
            if (left != null) { left.planted = false; left.weight = 0; }
            if (right != null) { right.planted = false; right.weight = 0; }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || left == null) return;
            if (!CanApply)
            {
                ResetContact(); ClearWeights(left); ClearWeights(right); return;
            }
            float yaw = transform.eulerAngles.y;
            if (tracking && ((transform.position - previousPosition).sqrMagnitude > .16f
                || Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw)) > 100)) ResetContact();
            tracking = true; previousPosition = transform.position; previousYaw = yaw;
            ReadAnimation(left); ReadAnimation(right);
            CalibrateSoles();
            bool moving = inputs != null && inputs.move.sqrMagnitude > .01f
                || new Vector2(capsule.velocity.x, capsule.velocity.z).sqrMagnitude > .01f;
            ReadPlantedContact(left, moving); ReadPlantedContact(right, moving);
            float leftPlant = PlantWeight(left, moving), rightPlant = PlantWeight(right, moving);

            // A lower stair cannot be reached by stretching a straight leg. Bend the stance instead.
            float lower = 0;
            if (left.supported && leftPlant > .5f) lower = Mathf.Min(lower, left.support.y - left.animated.y);
            if (right.supported && rightPlant > .5f) lower = Mathf.Min(lower, right.support.y - right.animated.y);
            pelvisOffset = Mathf.Lerp(pelvisOffset, Mathf.Clamp(lower, -pelvisDropLimit, 0), 1 - Mathf.Exp(-14 * Time.deltaTime));
            animator.bodyPosition += Vector3.up * pelvisOffset;

            if (moving)
            {
                left.planted = right.planted = false; swinging = null;
            }
            else
            {
                Plant(left, yaw); Plant(right, yaw);
                if (swinging == null)
                {
                    Foot other = nextFoot == left ? right : left;
                    if (NeedsStep(nextFoot, yaw)) StartStep(nextFoot, yaw);
                    else if (NeedsStep(other, yaw)) StartStep(other, yaw);
                }
                AdvanceStep();
            }
            ApplyFoot(left, moving, leftPlant); ApplyFoot(right, moving, rightPlant);
            IsApplying = left.weight > .01f || right.weight > .01f;
        }

        private void ReadAnimation(Foot foot)
        {
            foot.animated = foot.bone.position; foot.animatedRotation = foot.bone.rotation;
            foot.goalOffset = Quaternion.Inverse(foot.animatedRotation) * (animator.GetIKPosition(foot.goal) - foot.animated);
            foot.goalRotation = Quaternion.Inverse(foot.animatedRotation) * animator.GetIKRotation(foot.goal);
            foot.supported = Ground(foot, foot.animated, out foot.support, out foot.normal);
            foot.animatedSupport = foot.support; foot.animatedSupported = foot.supported;
        }

        private bool Ground(Foot foot, Vector3 point, out Vector3 support, out Vector3 normal)
        {
            support = point; normal = Vector3.up;
            int mask = movement.GroundLayers.value & ~(1 << gameObject.layer);
            if (!Physics.Raycast(point + Vector3.up * .4f, Vector3.down, out var hit, .4f + groundReach + foot.sole,
                mask, QueryTriggerInteraction.Ignore) || hit.collider.transform.IsChildOf(transform)
                || Vector3.Angle(hit.normal, Vector3.up) > capsule.slopeLimit) return false;
            support = hit.point + hit.normal * foot.sole; normal = hit.normal;
            return Mathf.Abs(support.y - point.y) <= groundReach;
        }

        private void CalibrateSoles()
        {
            if (calibrated || !left.supported || !right.supported || inputs.move.sqrMagnitude > .01f) return;
            float leftFloor = left.support.y - left.normal.y * left.sole;
            float rightFloor = right.support.y - right.normal.y * right.sole;
            float l = left.animated.y - leftFloor, r = right.animated.y - rightFloor;
            if (Mathf.Abs(leftFloor - rightFloor) > .02f || l < .025f || r < .025f || l > .2f || r > .2f) return;
            left.sole = l; right.sole = r;
            left.restHeight = left.animated.y - transform.position.y;
            right.restHeight = right.animated.y - transform.position.y;
            left.supported = Ground(left, left.animated, out left.support, out left.normal);
            right.supported = Ground(right, right.animated, out right.support, out right.normal);
            calibrated = true;
        }

        private float PlantWeight(Foot foot, bool moving)
        {
            if (!foot.supported) return 0;
            if (!moving) return 1;
            float lift = foot.animated.y - transform.position.y - foot.restHeight;
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.035f, .14f, lift));
        }

        private void ReadPlantedContact(Foot foot, bool moving)
        {
            if (moving || !foot.planted) return;
            // A turning animated ankle may cross an edge while the planted foot still has support.
            // Cast at the contact point, not the ankle already offset along the normal.
            Vector3 point = foot == swinging ? foot.landing - foot.landingNormal * foot.sole
                : foot.anchor - foot.anchorNormal * foot.sole;
            foot.supported = Ground(foot, point, out foot.support, out foot.normal);
            if (!foot.supported) foot.planted = false;
        }

        private void Plant(Foot foot, float yaw)
        {
            if (foot.planted) return;
            if (!foot.supported) return;
            foot.anchor = foot.support; foot.rotation = SurfaceRotation(foot, foot.normal);
            foot.anchorNormal = foot.normal;
            foot.plantYaw = yaw; foot.planted = true;
        }

        private bool NeedsStep(Foot foot, float yaw) => foot.planted
            && (Mathf.Abs(Mathf.DeltaAngle(foot.plantYaw, yaw)) > 12
                || foot.animatedSupported && Vector3.ProjectOnPlane(foot.animatedSupport - foot.anchor, Vector3.up).sqrMagnitude > .003f);

        private void StartStep(Foot foot, float yaw)
        {
            // Land ahead of the continuing turn, rather than on a target already behind the body.
            float lead = Mathf.Clamp(Mathf.DeltaAngle(yaw, movement.IntendedLookYaw), -30, 30);
            Vector3 predicted = transform.position + Quaternion.AngleAxis(lead, Vector3.up) * (foot.animated - transform.position);
            if (!Ground(foot, predicted, out var landing, out var normal)) return;
            swinging = foot; foot.phase = 0; foot.start = foot.anchor; foot.startRotation = foot.rotation;
            foot.landing = landing;
            foot.landingNormal = normal;
            foot.landingRotation = Quaternion.FromToRotation(Vector3.up, normal)
                * Quaternion.AngleAxis(lead, Vector3.up) * foot.animatedRotation;
            foot.plantYaw = yaw + lead;
        }

        private void AdvanceStep()
        {
            if (swinging == null) return;
            var foot = swinging;
            foot.phase = Mathf.Min(1, foot.phase + Time.deltaTime / stepDuration);
            float t = Mathf.SmoothStep(0, 1, foot.phase);
            foot.anchor = Vector3.Lerp(foot.start, foot.landing, t) + Vector3.up * (Mathf.Sin(foot.phase * Mathf.PI) * stepHeight);
            foot.rotation = Quaternion.Slerp(foot.startRotation, foot.landingRotation, t);
            if (foot.phase < 1) return;
            foot.anchor = foot.landing; foot.anchorNormal = foot.landingNormal;
            nextFoot = foot == left ? right : left; swinging = null;
        }

        private Quaternion SurfaceRotation(Foot foot, Vector3 normal) =>
            Quaternion.FromToRotation(Vector3.up, normal) * foot.animatedRotation;

        private void ApplyFoot(Foot foot, bool moving, float plantWeight)
        {
            Vector3 target = foot.support;
            Quaternion rotation = SurfaceRotation(foot, foot.normal);
            float wanted = plantWeight;
            if (!moving && foot.planted)
            {
                target = foot.anchor; rotation = foot.rotation;
                if (foot != swinging && foot.supported) target.y = foot.support.y;
                if (Mathf.Abs(Mathf.DeltaAngle(foot.plantYaw, transform.eulerAngles.y)) > 65
                    || Vector3.ProjectOnPlane(target - foot.animated, Vector3.up).magnitude > .22f)
                    wanted = 0;
            }
            if (!foot.supported || Vector3.Distance(foot.hip.position + Vector3.up * pelvisOffset, target) > foot.length * 1.015f)
                wanted = 0;
            foot.weight = Mathf.MoveTowards(foot.weight, wanted, Time.deltaTime * 12);
            animator.SetIKPositionWeight(foot.goal, foot.weight);
            animator.SetIKRotationWeight(foot.goal, foot.weight);
            animator.SetIKPosition(foot.goal, target + rotation * foot.goalOffset);
            animator.SetIKRotation(foot.goal, rotation * foot.goalRotation);
        }

        private void ClearWeights(Foot foot)
        {
            animator.SetIKPositionWeight(foot.goal, 0); animator.SetIKRotationWeight(foot.goal, 0);
        }
    }
}

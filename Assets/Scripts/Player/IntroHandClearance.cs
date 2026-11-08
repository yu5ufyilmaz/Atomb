using UnityEngine;
using UnityEngine.Playables;

// Desk intro, left hand. After the Animator has posed the body:
//  - reach (reachStart..reachEnd): lifts the hand so the fingertips land on the ashtray rim instead of the desk;
//  - smoke (poseStart..poseEnd): the Maya clip puts the hand into the skull, so blend into the reference video's pose:
//    palm down, fingers pointing away, the hand just below and in front of the head camera (only its top
//    shows at the bottom of the view), cigarette clamped between index and middle fingers standing straight up.
// Only while the intro timeline plays.
public class IntroHandClearance : MonoBehaviour
{
    [Tooltip("Parmak ucunun küllük kenarına kaldırıldığı aralık (intro saniyesi) ve kenar yüksekliği (dünya y)")]
    public float reachStart = 6.9f, reachEnd = 7.7f, rimHeight = 1.37f;
    [Tooltip("İçme pozunun intro timeline'ında başladığı ve bittiği saniye; geçiş ramp kadar sürer")]
    public float poseStart = 7.9f, poseEnd = 10.1f, ramp = 0.35f;
    [Tooltip("İçme pozunda sigara kavrama noktasının kafa kamerasından ileri ve yukarı uzaklığı (m, eksi = aşağı), kameranın kendi eksenlerinde")]
    public float viewForward = 0.17f, viewHeight = -0.07f;
    [Tooltip("Filtre ucunun avuçtan taşması (m); IntroCigarette aynı değeri kullanır")]
    public float filterOut = 0.012f;
    [Tooltip("Parmak kıvırma yönü (+1/-1); ters kıvrılırsa işareti değiştir")]
    public float curlSign = 1;

    Animator animator;
    PlayableDirector intro;
    Transform view; // head camera root

    void Awake()
    {
        animator = GetComponent<Animator>();
        var menu = GetComponent<InGameMenuController>();
        intro = menu != null ? menu.introTimeline : null;
        view = menu != null ? menu.cameraTarget : null;
    }

    // Chest facing from the shoulders: the root transform's forward is not the body's during the intro clips.
    public static Vector3 Facing(Animator animator) => Vector3.Cross(Side(animator), Vector3.up).normalized;
    public static Vector3 Side(Animator animator) => (animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position - animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position).normalized;
    public static Vector3 SkullCentre(Animator animator) => animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.08f;

    // Left-hand grip frame: clamp point between index and middle fingers (proximal phalanges), finger direction,
    // and the palm normal (out of the palm).
    public static void Grip(Animator animator, out Vector3 clamp, out Vector3 fingers, out Vector3 palm)
    {
        Vector3 iP = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).position, iI = animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate).position;
        Vector3 mP = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position, mI = animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate).position;
        clamp = (iP + iI + mP + mI) / 4;
        fingers = (iI - iP + mI - mP).normalized;
        palm = Vector3.Cross((iP - mP).normalized, fingers).normalized; // left hand: (index side) x fingers = out of the palm (checked against the palm-down desk pose)
    }

    void LateUpdate()
    {
        if (animator == null || !animator.isHuman || intro == null || intro.state != PlayState.Playing) return;
        float time = (float)intro.time;

        float reach = Mathf.Min(Mathf.InverseLerp(reachStart, reachStart + 0.3f, time), Mathf.InverseLerp(reachEnd, reachEnd - 0.2f, time));
        if (reach > 0)
        {
            float lift = Mathf.Max(0, rimHeight - animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal).position.y);
            var wristBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            ReachWrist(wristBone.position + Vector3.up * (lift * reach));
        }

        float weight = Mathf.Min(Mathf.InverseLerp(poseStart, poseStart + ramp, time), Mathf.InverseLerp(poseEnd, poseEnd - ramp, time));
        if (weight > 0) Smoke(weight);
    }

    void Smoke(float weight)
    {
        var eye = view != null ? view : animator.GetBoneTransform(HumanBodyBones.Head);
        var fingersTarget = (eye.forward * 0.85f + eye.right * 0.35f).normalized; // away from the camera: the knuckles make the dome of the reference video
        var palmTarget = -eye.up; // palm down: the cigarette stands up out of the back of the hand
        var clampTarget = eye.position + eye.forward * viewForward + eye.up * viewHeight; // centred, just under the view
        var wristBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        Grip(animator, out var clamp, out var fingers, out var palm);
        var align = Quaternion.FromToRotation(fingers, fingersTarget);
        var palmNow = align * palm;
        var palmWanted = Vector3.ProjectOnPlane(palmTarget, fingersTarget).normalized;
        align = Quaternion.AngleAxis(Vector3.SignedAngle(palmNow, palmWanted, fingersTarget), fingersTarget) * align;
        align = Quaternion.Slerp(Quaternion.identity, align, weight);
        var goal = Vector3.Lerp(clamp, clampTarget, weight);
        var wristRotation = align * wristBone.rotation;
        for (int pass = 0; pass < 3; pass++) // swinging the arm turns the hand too: re-aim the wrist, then close the gap again
        {
            wristBone.rotation = wristRotation;
            Grip(animator, out clamp, out _, out _);
            ReachWrist(wristBone.position + (goal - clamp));
        }
        wristBone.rotation = wristRotation;

        // Index and middle finger parallel, so the cigarette sits between them instead of floating in a spread gap.
        var indexProximal = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal); var middleProximal = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        var indexDirection = animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate).position - indexProximal.position;
        var middleDirection = animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate).position - middleProximal.position;
        var mean = (indexDirection.normalized + middleDirection.normalized).normalized;
        indexProximal.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(indexDirection, mean), weight) * indexProximal.rotation;
        middleProximal.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(middleDirection, mean), weight) * middleProximal.rotation;

        // Loose fist as in the reference video: ring and little finger curl in, index and middle curl around the cigarette.
        var littleBone = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
        var curlAxis = Vector3.Cross(indexProximal.position - littleBone.position, mean).normalized * curlSign;
        Curl(HumanBodyBones.LeftIndexProximal, 75 * weight, curlAxis); Curl(HumanBodyBones.LeftMiddleProximal, 75 * weight, curlAxis);
        Curl(HumanBodyBones.LeftRingProximal, 70 * weight, curlAxis); Curl(HumanBodyBones.LeftLittleProximal, 80 * weight, curlAxis);
    }

    // Two-bone reach of the left wrist: open or close the elbow about its own bend axis until the shoulder-wrist
    // length fits, then swing the upper arm so the wrist lands on target.
    void ReachWrist(Vector3 target)
    {
        var shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var elbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        var wristBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Vector3 s = shoulder.position, e = elbow.position, wrist = wristBone.position;
        float a = Vector3.Distance(s, e), b = Vector3.Distance(e, wrist);
        float reach = Mathf.Clamp(Vector3.Distance(s, target), Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
        float current = Vector3.Angle(s - e, wrist - e);
        float wanted = Mathf.Acos(Mathf.Clamp((a * a + b * b - reach * reach) / (2 * a * b), -1, 1)) * Mathf.Rad2Deg;
        var axis = Vector3.Cross(s - e, wrist - e).normalized;
        elbow.rotation = Quaternion.AngleAxis(wanted - current, axis) * elbow.rotation;
        shoulder.rotation = Quaternion.FromToRotation(wristBone.position - s, target - s) * shoulder.rotation;
    }

    void Curl(HumanBodyBones proximal, float degrees, Vector3 axis)
    {
        var bone = animator.GetBoneTransform(proximal);
        if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        var next = animator.GetBoneTransform(proximal + 1); // intermediate
        if (next != null) next.rotation = Quaternion.AngleAxis(degrees * 0.8f, axis) * next.rotation;
    }
}

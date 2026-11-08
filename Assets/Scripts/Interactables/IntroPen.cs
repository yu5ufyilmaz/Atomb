using UnityEngine;
using UnityEngine.Playables;

// The pen of the desk intro. It lies on the desk (this object's own pose) on the title menu; at pickUpTime, while the head
// camera looks up and the hands are out of view, it goes into the player's right hand; from putDownTime it slides back
// onto the desk and stays there afterwards.
[DefaultExecutionOrder(50)] // after the animation has posed the hand
public class IntroPen : MonoBehaviour
{
    [Tooltip("Kopyalanacak kalem mesh'i (masadaki kalem modeli)")]
    public GameObject source;
    [Tooltip("Uç, mesh'in en uzun yerel ekseninin + yönünde mi")]
    public bool tipTowardPositive = true;
    public float pickUpTime = 8.6f, putDownTime = 14.35f;
    [Tooltip("Masadan ele kayma süresi (s); bakış yukarıdayken biter")]
    public float pickUpBlend = 0.3f;
    [Tooltip("Elden masadaki duruşa kayma süresi (s)")]
    public float putDownBlend = 0.6f;
    [Tooltip("Ucun başparmak ve işaret parmağı yastıklarından taşması (m)")]
    public float tipOut = 0.025f;
    [Tooltip("Kalem ucunun parmak yastıklarından el sırtı yönüne kaldırılması (m)")]
    public float liftOut = 0.005f;
    [Tooltip("Kalem gövdesinin başparmak-işaret boşluğunun ne kadar üstünden geçtiği (m), elin içinden geçmesin")]
    public float webClearance = 0.03f;
    [Tooltip("Parmak kıvırma yönü (+1/-1); parmaklar ters kıvrılırsa işareti değiştir")]
    public float curlSign = -1;

    Animator player;
    PlayableDirector intro;
    Vector3 restPosition; Quaternion restRotation;
    float halfLength, radius;

    void Awake()
    {
        restPosition = transform.position; restRotation = transform.rotation;
        var menu = FindFirstObjectByType<InGameMenuController>();
        intro = menu != null ? menu.introTimeline : null;
        player = menu != null ? menu.playerAnimator : null;

        // Copy of the desk pencil, centred on this object with the tip along +Z.
        var copy = Instantiate(source, transform);
        copy.name = "Pen";
        foreach (var component in copy.GetComponents<Component>())
            if (!(component is Transform || component is MeshFilter || component is MeshRenderer)) Destroy(component);
        foreach (Transform child in copy.transform) Destroy(child.gameObject);
        var scale = source.transform.lossyScale;
        var bounds = copy.GetComponent<MeshFilter>().sharedMesh.bounds;
        var extents = Vector3.Scale(bounds.extents, scale);
        int axis = extents.x > extents.y ? (extents.x > extents.z ? 0 : 2) : (extents.y > extents.z ? 1 : 2);
        var along = Vector3.zero; along[axis] = tipTowardPositive ? 1 : -1;
        halfLength = extents[axis];
        radius = (extents[(axis + 1) % 3] + extents[(axis + 2) % 3]) / 2;
        copy.transform.localScale = scale;
        copy.transform.localRotation = Quaternion.FromToRotation(along, Vector3.forward);
        copy.transform.localPosition = -(copy.transform.localRotation * Vector3.Scale(bounds.center, scale));
    }

    void LateUpdate()
    {
        float time = intro != null && intro.state == PlayState.Playing ? (float)intro.time : -1;
        if (player == null || time < pickUpTime || time >= putDownTime + putDownBlend)
        {
            transform.SetPositionAndRotation(restPosition, restRotation);
            return;
        }
        float pickUp = Mathf.InverseLerp(pickUpTime, pickUpTime + pickUpBlend, time);
        float putDown = Mathf.InverseLerp(putDownTime, putDownTime + putDownBlend, time);
        Grasp(Mathf.Min(pickUp, 1 - putDown));

        // Writing grip: pinched between the thumb and index pads, the body resting in the web between thumb and index
        // (bone positions are joints, so the pads lie half a distal segment past the distal joints).
        var thumbPad = Pad(HumanBodyBones.RightThumbDistal);
        var indexPad = Pad(HumanBodyBones.RightIndexDistal);
        var pinch = (thumbPad + indexPad) / 2;
        var web = Vector3.Lerp(player.GetBoneTransform(HumanBodyBones.RightIndexProximal).position, player.GetBoneTransform(HumanBodyBones.RightThumbIntermediate).position, 0.65f); // thumb side, not over the knuckles
        var back = Vector3.Cross(indexPad - thumbPad, web - pinch).normalized; // out of the back of the hand
        if (Vector3.Dot(back, Vector3.up) < 0) back = -back;
        var tipward = (pinch - (web + back * webClearance)).normalized; // leans back over the web, not through the hand
        var held = pinch + back * liftOut + tipward * (tipOut - halfLength);
        var heldRotation = Quaternion.LookRotation(tipward, back);
        transform.SetPositionAndRotation(
            Vector3.Lerp(Vector3.Lerp(restPosition, held, pickUp), restPosition, putDown),
            Quaternion.Slerp(Quaternion.Slerp(restRotation, heldRotation, pickUp), restRotation, putDown));
    }

    Vector3 Pad(HumanBodyBones distal)
    {
        var joint = player.GetBoneTransform(distal).position;
        return joint + (joint - player.GetBoneTransform(distal - 1).position) * 0.5f;
    }

    // The clip's right hand writes with open fingers: close them on the pen. Thumb and index pads meet a pen-width
    // apart, the middle finger bends under it, ring and little finger tuck into the palm.
    void Grasp(float weight)
    {
        if (weight <= 0) return;
        Transform Bone(HumanBodyBones bone) => player.GetBoneTransform(bone);
        var fingers = (Bone(HumanBodyBones.RightMiddleIntermediate).position - Bone(HumanBodyBones.RightMiddleProximal).position).normalized;
        var curlAxis = Vector3.Cross(Bone(HumanBodyBones.RightIndexProximal).position - Bone(HumanBodyBones.RightLittleProximal).position, fingers).normalized * curlSign;
        Curl(HumanBodyBones.RightMiddleProximal, 35 * weight, curlAxis);
        Curl(HumanBodyBones.RightRingProximal, 70 * weight, curlAxis);
        Curl(HumanBodyBones.RightLittleProximal, 80 * weight, curlAxis);

        var thumbPad = Pad(HumanBodyBones.RightThumbDistal);
        var indexPad = Pad(HumanBodyBones.RightIndexDistal);
        var meet = (thumbPad + indexPad) / 2;
        var gap = (indexPad - thumbPad).normalized * radius;
        Aim(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexDistal, meet + gap, weight);
        Aim(HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal, meet - gap, weight);
    }

    // Turns the finger at its root so its pad lands on the target.
    void Aim(HumanBodyBones root, HumanBodyBones distal, Vector3 target, float weight)
    {
        var bone = player.GetBoneTransform(root);
        var turn = Quaternion.FromToRotation(Pad(distal) - bone.position, target - bone.position);
        bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
    }

    void Curl(HumanBodyBones proximal, float degrees, Vector3 axis)
    {
        var bone = player.GetBoneTransform(proximal);
        if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        var next = player.GetBoneTransform(proximal + 1); // intermediate
        if (next != null) next.rotation = Quaternion.AngleAxis(degrees * 0.8f, axis) * next.rotation;
    }
}

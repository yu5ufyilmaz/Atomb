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

    Animator player;
    PlayableDirector intro;
    Vector3 restPosition; Quaternion restRotation;
    float halfLength;

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
        // Writing grip: pinched between the thumb and index pads, the body resting in the web between thumb and index
        // (bone positions are joints, so the pads lie half a distal segment past the distal joints).
        Vector3 Pad(HumanBodyBones distal)
        {
            var joint = player.GetBoneTransform(distal).position;
            return joint + (joint - player.GetBoneTransform(distal - 1).position) * 0.5f;
        }
        var thumbPad = Pad(HumanBodyBones.RightThumbDistal);
        var indexPad = Pad(HumanBodyBones.RightIndexDistal);
        var pinch = (thumbPad + indexPad) / 2;
        var web = Vector3.Lerp(player.GetBoneTransform(HumanBodyBones.RightIndexProximal).position, player.GetBoneTransform(HumanBodyBones.RightThumbIntermediate).position, 0.65f); // thumb side, not over the knuckles
        var back = Vector3.Cross(indexPad - thumbPad, web - pinch).normalized; // out of the back of the hand
        if (Vector3.Dot(back, Vector3.up) < 0) back = -back;
        var tipward = (pinch - (web + back * webClearance)).normalized; // leans back over the web, not through the hand
        var held = pinch + back * liftOut + tipward * (tipOut - halfLength);
        var heldRotation = Quaternion.LookRotation(tipward, back);
        float pickUp = Mathf.InverseLerp(pickUpTime, pickUpTime + pickUpBlend, time);
        float putDown = Mathf.InverseLerp(putDownTime, putDownTime + putDownBlend, time);
        transform.SetPositionAndRotation(
            Vector3.Lerp(Vector3.Lerp(restPosition, held, pickUp), restPosition, putDown),
            Quaternion.Slerp(Quaternion.Slerp(restRotation, heldRotation, pickUp), restRotation, putDown));
    }
}

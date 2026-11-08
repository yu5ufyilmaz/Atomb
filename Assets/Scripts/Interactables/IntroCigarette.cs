using UnityEngine;
using UnityEngine.Playables;

// The one cigarette of the desk intro. It rests lit on the ashtray rim while the title menu is up (this object's own
// pose), sits between the player's left index and middle fingers from pickUpTime to restTime of the intro timeline,
// and lies back on the rim afterwards, still burning (CigaretteEmber at the tip).
[DefaultExecutionOrder(50)] // after IntroHandClearance has posed the hand
public class IntroCigarette : MonoBehaviour
{
    [Tooltip("Kopyalanacak tekli sigara mesh'i (paketteki pCylinder)")]
    public GameObject source;
    [Tooltip("Filtre ucu kaynağın yerel +Y yönünde mi")]
    public bool filterTowardPositiveY = true;
    public float pickUpTime = 7.45f, restTime = 14.95f;
    [Tooltip("Dinlenme pozundan kavramaya kayma süresi (s)")]
    public float pickUpBlend = 0.35f;
    [Tooltip("Kaymadan sonra yatay duruştan içme yönüne dönme süresi (s)")]
    public float settleTime = 0.45f;
    [Tooltip("restTime'dan önce elden küllüğe geri kayma süresi (s)")]
    public float putDownBlend = 0.6f;
    [Tooltip("Filtre ucunun avuçtan taşması (m); IntroHandClearance.filterOut ile aynı")]
    public float filterOut = 0.012f;

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

        // Copy of the pack cigarette, axis along this object's +Z with the filter at -Z.
        var copy = Instantiate(source, transform);
        copy.name = "Cigarette";
        foreach (var component in copy.GetComponents<Component>())
            if (!(component is Transform || component is MeshFilter || component is MeshRenderer)) Destroy(component);
        foreach (Transform child in copy.transform) Destroy(child.gameObject);
        copy.transform.localScale = source.transform.lossyScale;
        copy.transform.localRotation = Quaternion.FromToRotation(filterTowardPositiveY ? Vector3.down : Vector3.up, Vector3.forward);
        copy.transform.localPosition = Vector3.zero;
        halfLength = copy.GetComponent<MeshFilter>().sharedMesh.bounds.extents.y * source.transform.lossyScale.y;
        var renderer = copy.GetComponent<MeshRenderer>();
        var materials = renderer.materials; // instances: the pack's body material is orange, ours is paper
        materials[0].SetColor("_BaseColor", new Color(0.92f, 0.9f, 0.86f));
        renderer.materials = materials;

        var ember = new GameObject("Ember").AddComponent<CigaretteEmber>();
        ember.transform.SetParent(transform, false);
        ember.transform.localPosition = Vector3.forward * halfLength;
    }

    void LateUpdate()
    {
        float time = intro != null && intro.state == PlayState.Playing ? (float)intro.time : -1;
        if (player == null || time < pickUpTime || time >= restTime)
        {
            transform.SetPositionAndRotation(restPosition, restRotation);
            return;
        }
        // Between index and middle finger, perpendicular to them: filter out of the palm, lit end out of the back of the hand.
        IntroHandClearance.Grip(player, out var clamp, out var fingers, out var palm);
        var grip = clamp + palm * (filterOut - halfLength);
        var gripRotation = Quaternion.LookRotation(-palm, fingers);
        float blend = Mathf.InverseLerp(pickUpTime, pickUpTime + pickUpBlend, time); // slides from the rim into the fingers
        // Picked up flat as it lay (the palm faces down here); it settles into the smoking hold while the hand rises.
        float settle = Mathf.InverseLerp(pickUpTime + pickUpBlend, pickUpTime + pickUpBlend + settleTime, time);
        float putDown = Mathf.InverseLerp(restTime - putDownBlend, restTime, time); // back onto the rim as the body leans away
        transform.SetPositionAndRotation(
            Vector3.Lerp(Vector3.Lerp(restPosition, grip, blend), restPosition, putDown),
            Quaternion.Slerp(Quaternion.Slerp(restRotation, gripRotation, settle), restRotation, putDown));
    }
}

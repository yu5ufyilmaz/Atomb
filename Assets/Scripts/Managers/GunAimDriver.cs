using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;

// Runs after the Animator has posed the rig and before the CinemachineBrain reads the camera,
// which a PlayableBehaviour cannot do.
[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class GunAimDriver : MonoBehaviour
{
    // In the 911pistol's space: the barrel bushing, with the bore along local -z.
    static readonly Vector3 Muzzle = new Vector3(0, .1156f, -.1368f);
    const int Passes = 2; // The shoulder is not at the muzzle, so one rotation lands short.
    const float FocusFieldOfView = 38; // Tunnel vision: at the rest lens the muzzle is a few pixels.
    const float BodyRadius = .12f;
    const float MinFurnitureSize = 1; // Hanging lamps and desk clutter must not stop him.
    const float MaxFurnitureSize = 8; // Building shells are walls: their colliders stop him, their bounds cover whole rooms.
    const float SurveyRadius = 4;
    const float CornerLookTime = .3f;
    // World metres from Guderian's foot bones down to his soles, measured on the baked mesh (Senzora/Doğrulama/Guderian Arka Jumpscare Kareleri).
    const float AnkleToSole = .125f, ToesToSole = .05f;

    public Transform Arm, Gun, Eye, Head;
    public Animator Body;
    public CinemachineVirtualCamera Lens;
    public float Weight, Focus, StartView;
    public Vector3 StartPosition;
    public Quaternion StartRotation;
    public LensSettings StartLens;
    public float FloorLift { get; private set; } // Last correction PlantFeet made, for the capture check.
    public bool StopAtWalls, Hunted;
    float cornerLook;
    Vector3 lastClear;
    bool crawling, cornered;
    LensSettings restLens;
    CinemachineBasicMultiChannelPerlin noise;
    float restNoise;
    List<Bounds> furniture;

    void OnEnable()
    {
        restLens = Lens.m_Lens;
        noise = Lens.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        restNoise = noise != null ? noise.m_AmplitudeGain : 0;
    }

    void OnDisable()
    {
        Lens.m_Lens = restLens;
        if (noise != null) noise.m_AmplitudeGain = restNoise;
    }

    void LateUpdate()
    {
        PlantFeet();
        KeepClearOfRoom();

        for (int pass = 0; pass < Passes && Weight > 0; pass++)
        {
            var aim = Quaternion.FromToRotation(-Gun.forward, Eye.position - Gun.TransformPoint(Muzzle));
            Arm.rotation = Quaternion.Slerp(Quaternion.identity, aim, Weight) * Arm.rotation;
        }
        // The mocap stands bladed and looks past the man on the floor; turn the face (CC_Base_Head looks down +z) onto him with the gun.
        if (Head != null && Weight > 0)
            Head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Head.forward, Eye.position - Head.position), Weight) * Head.rotation;
        var lens = restLens;
        lens.FieldOfView = Mathf.Lerp(restLens.FieldOfView, FocusFieldOfView, Focus);
        if (Focus > 0) Eye.rotation = Quaternion.Slerp(Eye.rotation, Quaternion.LookRotation(Gun.TransformPoint(Muzzle) - Eye.position, Eye.up), Focus); // Eye.up keeps the keyed roll (he lies tilted, shaking).
        // The keyed look assumes the full retreat, so a cornered player has to find Guderian himself.
        cornerLook = Mathf.MoveTowards(cornerLook, cornered && Hunted ? 1 : 0, Time.deltaTime / CornerLookTime);
        if (cornerLook > 0) Eye.rotation = Quaternion.Slerp(Eye.rotation, Quaternion.LookRotation(Arm.position - Eye.position, Eye.up), cornerLook * (1 - Focus));
        // Until the whip turn the screen stays the player's own frame (eye, view, lens, no handheld noise),
        // so the first thing that changes is the turn itself.
        if (StartView > 0)
        {
            Eye.SetPositionAndRotation(Vector3.Lerp(Eye.position, StartPosition, StartView), Quaternion.Slerp(Eye.rotation, StartRotation, StartView));
            lens = LensSettings.Lerp(lens, StartLens, StartView);
        }
        Lens.m_Lens = lens;
        if (noise != null) noise.m_AmplitudeGain = restNoise * (1 - StartView);
    }

    // The clip's floor is the player's root height, not the floor under Guderian: his lowest sole goes onto the floor below it, every frame.
    void PlantFeet()
    {
        if (Body == null) return;
        float lift = float.MaxValue;
        foreach (var (bone, sole) in new[] { (HumanBodyBones.LeftFoot, AnkleToSole), (HumanBodyBones.RightFoot, AnkleToSole), (HumanBodyBones.LeftToes, ToesToSole), (HumanBodyBones.RightToes, ToesToSole) })
        {
            var foot = Body.GetBoneTransform(bone);
            if (foot != null && FloorBelow(foot.position, out float floor)) lift = Mathf.Min(lift, foot.position.y - sole - floor);
        }
        FloorLift = lift == float.MaxValue ? 0 : -lift;
        Body.transform.position += Vector3.up * FloorLift;
    }

    bool FloorBelow(Vector3 point, out float height)
    {
        height = 0;
        var hits = Physics.RaycastAll(point + Vector3.up * .4f, Vector3.down, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .Where(hit => !hit.transform.IsChildOf(Body.transform) && !hit.transform.IsChildOf(Eye.root)).ToArray();
        if (hits.Length == 0) return false;
        height = point.y + .4f - hits.Min(hit => hit.distance);
        return true;
    }

    // The keys are the same in every room: he backs up until something is behind him or between him and Guderian,
    // then only falls in place.
    void KeepClearOfRoom()
    {
        if (!StopAtWalls) { crawling = cornered = false; return; }
        if (!crawling) { crawling = true; lastClear = Eye.position; SurveyFurniture(); }
        var toShooter = Arm.position - Eye.position;
        var sight = new Ray(Eye.position, toShooter);
        cornered = cornered || Physics.CheckSphere(Eye.position, BodyRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || Physics.Linecast(Eye.position, Arm.position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || furniture.Any(item => item.SqrDistance(Eye.position) < BodyRadius * BodyRadius || (item.IntersectRay(sight, out float distance) && distance < toShooter.magnitude));
        if (cornered) Eye.position = new Vector3(lastClear.x, Eye.position.y, lastClear.z);
        else lastClear = Eye.position;
    }

    // Desks have colliders only on part of what you can see, so sight has to go by the renderers.
    void SurveyFurniture()
    {
        furniture = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(item => item.enabled && !item.transform.IsChildOf(Gun)).Select(item => item.bounds)
            .Where(item => item.size.magnitude > MinFurnitureSize && item.size.magnitude < MaxFurnitureSize && !item.Contains(Eye.position) && item.SqrDistance(Eye.position) < SurveyRadius * SurveyRadius).ToList();
    }
}

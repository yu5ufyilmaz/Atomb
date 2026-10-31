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
    const float SurveyRadius = 4;
    const float CornerLookTime = .3f;

    public Transform Arm, Gun, Eye;
    public CinemachineVirtualCamera Lens;
    public float Weight, Focus;
    public bool StopAtWalls, Hunted;
    float cornerLook;
    Vector3 lastClear;
    bool crawling, cornered;
    float restFieldOfView;
    List<Bounds> furniture;

    void OnEnable() => restFieldOfView = Lens.m_Lens.FieldOfView;
    void OnDisable() => Lens.m_Lens.FieldOfView = restFieldOfView;

    void LateUpdate()
    {
        KeepClearOfRoom();

        for (int pass = 0; pass < Passes && Weight > 0; pass++)
        {
            var aim = Quaternion.FromToRotation(-Gun.forward, Eye.position - Gun.TransformPoint(Muzzle));
            Arm.rotation = Quaternion.Slerp(Quaternion.identity, aim, Weight) * Arm.rotation;
        }
        Lens.m_Lens.FieldOfView = Mathf.Lerp(restFieldOfView, FocusFieldOfView, Focus);
        if (Focus > 0) Eye.rotation = Quaternion.Slerp(Eye.rotation, Quaternion.LookRotation(Gun.TransformPoint(Muzzle) - Eye.position), Focus);
        // The keyed look assumes the full retreat, so a cornered player has to find Guderian himself.
        cornerLook = Mathf.MoveTowards(cornerLook, cornered && Hunted ? 1 : 0, Time.deltaTime / CornerLookTime);
        if (cornerLook > 0) Eye.rotation = Quaternion.Slerp(Eye.rotation, Quaternion.LookRotation(Arm.position - Eye.position), cornerLook * (1 - Focus));
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
            .Where(item => item.size.magnitude > MinFurnitureSize && !item.Contains(Eye.position) && item.SqrDistance(Eye.position) < SurveyRadius * SurveyRadius).ToList();
    }
}

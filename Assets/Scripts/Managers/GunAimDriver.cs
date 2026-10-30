using UnityEngine;

// Runs after the Animator has posed the arm, which a PlayableBehaviour cannot do.
[ExecuteAlways]
public class GunAimDriver : MonoBehaviour
{
    // In the 911pistol's space: the barrel bushing, with the bore along local -z.
    static readonly Vector3 Muzzle = new Vector3(0, .1156f, -.1368f);
    const int Passes = 2; // The shoulder is not at the muzzle, so one rotation lands short.

    public Transform Arm, Gun, Eye;
    public float Weight;

    void LateUpdate()
    {
        if (Weight <= 0) return;
        for (int pass = 0; pass < Passes; pass++)
        {
            var aim = Quaternion.FromToRotation(-Gun.forward, Eye.position - Gun.TransformPoint(Muzzle));
            Arm.rotation = Quaternion.Slerp(Quaternion.identity, aim, Weight) * Arm.rotation;
        }
    }
}

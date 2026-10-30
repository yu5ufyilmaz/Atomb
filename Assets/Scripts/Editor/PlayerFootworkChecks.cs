using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerController = StarterAssets.CharacterController;

// Observes the real Humanoid bones and colliders; no runtime component is required to compile this baseline.
public static class PlayerFootworkChecks
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const float FloorY = 20f;
    private static readonly Vector3 Center = new Vector3(1000, FloorY, 1000);
    private static bool running;

    [MenuItem("Senzora/Player Footwork/Validate Rapid Camera Turns")]
    public static void ValidateRapidCameraTurns()
    {
        Require(Application.isPlaying, "Run rapid camera turn checks in Play mode.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var animator = player.GetComponent<Animator>();
        var capsule = player.GetComponent<UnityEngine.CharacterController>();
        var inputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        var footwork = player.GetComponent<StarterAssets.PlayerFootwork>();
        Require(footwork != null && footwork.CanApply, "Rapid turn checks require grounded free locomotion.");
        var saved = new Snapshot(player, animator, capsule, inputs);
        var cameraInput = typeof(PlayerController).GetMethod("CameraRotation", Members);
        var lateUpdate = typeof(PlayerController).GetMethod("LateUpdate", Members);
        Action rotate = () => { cameraInput.Invoke(player, null); lateUpdate.Invoke(player, null); };
        try
        {
            Zero(inputs);
            foreach (float start in new[] { 0f, 350f, -350f })
            foreach (float turn in new[] { 20f, -20f, 180f, -180f, 270f, -270f, 720f, -720f })
            {
                player.transform.rotation = Quaternion.Euler(0, start, 0);
                player.ForceCameraRotation(start + turn, 0);
                rotate();
                float gap = Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, player.IntendedLookYaw));
                Require(gap <= 60.01f, $"Rapid {turn}-degree turn left a {gap:F1}-degree body/view gap.");
                Require(Mathf.Abs(Mathf.DeltaAngle(player.IntendedLookYaw,
                    player.CinemachineCameraTarget.transform.eulerAngles.y)) < .01f, "Body catch-up changed the requested camera yaw.");
                if (Mathf.Abs(turn) < 60)
                    Require(Mathf.Abs(Mathf.DeltaAngle(start, player.transform.eulerAngles.y)) < .01f,
                        "A small look turn bypassed normal smooth body rotation.");
            }

            foreach (var move in new[] { Vector2.zero, Vector2.up })
            foreach (float look in new[] { 2000f, -2000f })
            {
                inputs.move = move;
                inputs.look = new Vector2(look, 0);
                for (int frame = 0; frame < 8; frame++)
                {
                    float previousYaw = player.IntendedLookYaw;
                    rotate();
                    Require(Mathf.Abs(Mathf.DeltaAngle(previousYaw, player.IntendedLookYaw)) > .1f,
                        "Rapid look input did not reach the camera.");
                    Require(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,
                        player.CinemachineCameraTarget.transform.eulerAngles.y)) <= 60.01f,
                        "Repeated rapid look input let the view outrun the body.");
                }
            }
            Zero(inputs);

            player.SetFrozen(true, false, false);
            Quaternion body = player.transform.rotation;
            player.ForceCameraRotation(player.transform.eulerAngles.y + 180, 0);
            rotate();
            Require(Quaternion.Angle(body, player.transform.rotation) < .01f, "Free look rotated a frozen interaction body.");

            player.SetFrozen(true, true, false);
            Quaternion view = player.CinemachineCameraTarget.transform.rotation;
            inputs.look = new Vector2(500, 0);
            rotate();
            Require(Quaternion.Angle(view, player.CinemachineCameraTarget.transform.rotation) < .01f
                && Quaternion.Angle(body, player.transform.rotation) < .01f, "A full view lock moved the camera or body.");

            player.SetFrozen(false, false, false);
            Zero(inputs);
            animator.Play("Base Layer.SitIdle", 0, .4f);
            animator.Update(0);
            Require(!footwork.CanApply, "The sitting animation did not suspend footwork.");
            body = player.transform.rotation;
            player.ForceCameraRotation(player.transform.eulerAngles.y + 180, 0);
            rotate();
            Require(Quaternion.Angle(body, player.transform.rotation) < .01f, "Rapid-turn correction changed an authored sitting pose.");
            Debug.Log("PLAYER CAMERA TURN PASS: rapid left/right/wrapped turns stay within 60 degrees; camera yaw, small turns, frozen/full-lock and sitting poses preserved.");
        }
        finally { saved.Restore(); }
    }

    private static IEnumerator CheckRapidTurnFrameOrder(PlayerController player, StarterAssets.StarterAssetsInputs inputs)
    {
        var probe = player.gameObject.AddComponent<PlayerCameraTurnFrameProbe>();
        try
        {
            for (int frame = 0; frame < 12; frame++)
            {
                inputs.look = new Vector2(frame < 6 ? 90 : -90, 0);
                yield return new WaitForEndOfFrame();
                Require(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,
                    player.CinemachineCameraTarget.transform.eulerAngles.y)) <= 60.01f,
                    "The rendered view outran the body during a rapid turn.");
            }
            Require(probe.samples >= 10 && probe.maximumPostIkYaw < .1f,
                $"Camera catch-up moved the body after foot IK by {probe.maximumPostIkYaw:F2} degrees ({probe.samples} frames).");
            Debug.Log($"PLAYER CAMERA FRAME ORDER PASS: {probe.samples} rapid-input frames; post-IK body rotation {probe.maximumPostIkYaw:F3} degrees.");
        }
        finally { Zero(inputs); UnityEngine.Object.Destroy(probe); }
    }

    [MenuItem("Senzora/Player Footwork/Validate Play Footwork")]
    public static void Validate()
    {
        Require(Application.isPlaying && SceneManager.GetActiveScene().path == LaboratorySynthesisSetup.ScenePath,
            "Run the laboratory migration copy in Play mode first.");
        Require(!running && SaveManager.Instance != null && SaveManager.Instance.sessionOnly && !SaveManager.Instance.HasSaveFile(),
            "Footwork checks require an idle check runner and session-only saves.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Require(player != null && GameManager.Instance != null && GameManager.Instance.activeInteraction == null,
            "The real player must be available outside an interaction.");
        running = true;
        player.StartCoroutine(Run(player));
    }

    private static IEnumerator Run(PlayerController player)
    {
        Application.runInBackground = true;
        GameObject fixture = null;
        Snapshot saved = null;
        try
        {
            var game = GameManager.Instance;
            Require(!game.isGamePaused, "Resume gameplay before footwork checks.");
            if (!game.isGameStarted)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();
                Require(menu != null, "The original laboratory main menu is missing.");
                menu.PlayGame(); menu.ChooseTutorial(true);
                float deadline = Time.realtimeSinceStartup + 90;
                while (!game.isGameStarted && Time.realtimeSinceStartup < deadline) yield return null;
                Require(game.isGameStarted, "The original New Game / stand-up sequence did not finish.");
                yield return new WaitForSeconds(2);
            }
            var animator = player.GetComponent<Animator>();
            var capsule = player.GetComponent<UnityEngine.CharacterController>();
            var inputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
            Require(player.enabled && capsule != null && capsule.enabled && animator != null && animator.enabled && animator.isHuman,
                "The real Humanoid player and capsule must be enabled.");
            saved = new Snapshot(player, animator, capsule, inputs);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Require(left != null && right != null, "The Humanoid avatar has no mapped foot bones.");
            fixture = new GameObject("Footwork check fixture (runtime only)");
            var floorLeft = Box(fixture.transform, "Left floor", Center + new Vector3(-1, -.25f, 0), new Vector3(2, .5f, 4));
            var floorRight = Box(fixture.transform, "Right floor", Center + new Vector3(1, -.25f, 0), new Vector3(2, .5f, 4));
            var step = Box(fixture.transform, "Split 18cm step", Center, Vector3.one);
            step.SetActive(false);
            player.SetFrozen(false, false, false);
            inputs.cursorInputForLook = false;
            Teleport(player, capsule);
            yield return Settle(inputs, 1.5f);
            Require(player.Grounded, "The real player's grounded check did not detect the fixture.");
            bool flatLeftFound = Support(left, out var flatLeft);
            bool flatRightFound = Support(right, out var flatRight);
            Require(flatLeftFound && flatRightFound, "Flat diagnostic floor is missing.");
            float leftSole = left.position.y - flatLeft.point.y;
            float rightSole = right.position.y - flatRight.point.y;
            Debug.Log($"PLAYER FOOTWORK BASELINE: scale={player.transform.lossyScale}; humanScale={animator.humanScale:F4}; "
                + $"feetBottomHeight raw={animator.leftFeetBottomHeight:F4}/{animator.rightFeetBottomHeight:F4}; "
                + $"scaled={animator.leftFeetBottomHeight * player.transform.lossyScale.y:F4}/{animator.rightFeetBottomHeight * player.transform.lossyScale.y:F4}; "
                + $"bones={left.position:F4}/{right.position:F4}; flat ankle offsets={leftSole:F4}/{rightSole:F4}.");
            Require(leftSole > -.02f && leftSole < .3f && rightSole > -.02f && rightSole < .3f,
                "The flat idle pose cannot provide a credible anatomical sole-offset baseline.");

            ValidateRapidCameraTurns();
            yield return Settle(inputs, .2f);
            yield return CheckRapidTurnFrameOrder(player, inputs);
            Teleport(player, capsule);
            yield return Settle(inputs, 1);

            foreach (bool leftHigh in new[] { true, false })
            {
                step.transform.position = Center + new Vector3(leftHigh ? -1 : 1, .09f, 0);
                step.transform.localScale = new Vector3(2, .18f, 4);
                step.SetActive(true);
                Teleport(player, capsule);
                yield return Settle(inputs, 1.4f);
                bool leftFound = Support(left, out var leftHit);
                bool rightFound = Support(right, out var rightHit);
                Require(leftFound && rightFound, "A split-step support was not found.");
                Require(Mathf.Abs(leftHit.point.y - rightHit.point.y) > .15f,
                    "The split fixture did not put the two real feet on different surfaces.");
                CheckSole(left, leftSole, leftHit, leftHigh ? "left high" : "left low");
                CheckSole(right, rightSole, rightHit, leftHigh ? "right low" : "right high");
                Debug.Log("PLAYER FOOTWORK STEP PASS: " + (leftHigh ? "left" : "right") + " foot on 18cm step; both supported soles within 2.5cm.");
                if (leftHigh) yield return Capture(fixture.transform, player.transform, "Temp/player-footwork-step.png");
                step.SetActive(false);
            }

            Teleport(player, capsule);
            yield return Settle(inputs, 1);
            foreach (float turn in new[] { 90f, -90f, 180f })
            {
                Vector3 root = player.transform.position;
                float yaw = player.transform.eulerAngles.y;
                player.ForceCameraRotation(yaw + turn, 0);
                bool lifted = false, replanted = false;
                float until = Time.time + 2.2f;
                while (Time.time < until)
                {
                    Zero(inputs);
                    yield return new WaitForEndOfFrame();
                    Require(FlatDistance(root, player.transform.position) < .03f, "A stationary turn translated the player root.");
                    float gap = Mathf.Max(Gap(left, leftSole), Gap(right, rightSole));
                    if (gap > .025f) lifted = true;
                    if (lifted && Mathf.Abs(Gap(left, leftSole)) < .025f && Mathf.Abs(Gap(right, rightSole)) < .025f) replanted = true;
                }
                Require(lifted && replanted, $"A {turn:F0}-degree idle turn did not visibly lift and replant a foot.");
            }
            Debug.Log("PLAYER FOOTWORK TURN PASS: +/-90 and 180 degree view turns lift/replant real feet without root translation.");

            Teleport(player, capsule);
            yield return Settle(inputs, .5f);
            inputs.move = Vector2.up;
            yield return new WaitForSeconds(.45f);
            Require(animator.GetFloat("VelocityZ") > .2f, "Footwork suppressed the original walking animation input.");
            float stopDeadline = Time.time + 1.5f;
            do { Zero(inputs); yield return new WaitForEndOfFrame(); }
            while (Time.time < stopDeadline && (Mathf.Abs(animator.GetFloat("VelocityZ")) >= .1f || animator.GetFloat("Speed") >= .15f));
            Require(Mathf.Abs(animator.GetFloat("VelocityZ")) < .1f && animator.GetFloat("Speed") < .15f,
                $"Walking did not settle: velocityZ={animator.GetFloat("VelocityZ"):F3}, speed={animator.GetFloat("Speed"):F3}.");

            var slope = Box(fixture.transform, "Slope", Center - Vector3.up * .25f, new Vector3(5, .5f, 5));
            floorLeft.SetActive(false); floorRight.SetActive(false);
            foreach (float angle in new[] { 12f, 35f })
            {
                slope.transform.rotation = Quaternion.Euler(0, 0, angle);
                Teleport(player, capsule);
                yield return Settle(inputs, 1.4f);
                bool slopeLeftFound = Support(left, out var slopeLeft);
                bool slopeRightFound = Support(right, out var slopeRight);
                Require(slopeLeftFound && slopeRightFound, "Slope contacts missing.");
                CheckSole(left, leftSole, slopeLeft, "left slope"); CheckSole(right, rightSole, slopeRight, "right slope");
                Vector3 settledLeft = left.position, settledRight = right.position;
                yield return Settle(inputs, .7f);
                Require(Vector3.Distance(settledLeft, left.position) < .03f && Vector3.Distance(settledRight, right.position) < .03f,
                    "Standing feet drift or keep stepping on the slope.");
            }
            Debug.Log("PLAYER FOOTWORK SLOPE/WALK PASS: sloped supports and original walking/stop parameters.");
            slope.SetActive(false); floorLeft.SetActive(true); floorRight.SetActive(true);

            Teleport(player, capsule);
            yield return Settle(inputs, 1);
            floorLeft.SetActive(false);
            yield return Settle(inputs, .35f);
            Require(!Support(left, out _) && Support(right, out _), "The no-support fixture did not leave only the left foot unsupported.");
            Require(IsFinite(left.position) && left.position.y > FloorY - .5f, "An unsupported foot collapsed or retained a distant target.");
            floorLeft.SetActive(true);
            floorRight.SetActive(false);
            yield return Settle(inputs, .35f);
            Require(Support(left, out _) && !Support(right, out _), "The no-support fixture did not leave only the right foot unsupported.");
            Require(IsFinite(right.position) && right.position.y > FloorY - .5f, "An unsupported foot collapsed or retained a distant target.");
            floorRight.SetActive(true);

            player.SetFrozen(true, true, false);
            yield return Settle(inputs, .25f);
            CheckNotApplying(player, "frozen interaction pose");
            player.SetFrozen(false, false, false);
            animator.Play("Base Layer.SitIdle", 0, .4f);
            yield return Settle(inputs, .3f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.SitIdle"), "The special-pose gate check did not enter the existing sitting state.");
            CheckNotApplying(player, "existing SitIdle state");
            Debug.Log("PLAYER FOOTWORK PLAY PASS: independent split-step contacts, natural turn lifts, missing support and special-pose gates.");
        }
        finally
        {
            if (saved != null) saved.Restore();
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            running = false;
        }
    }

    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name; box.layer = 0; box.transform.SetParent(parent);
        box.transform.position = position; box.transform.localScale = scale;
        return box;
    }

    private static IEnumerator Capture(Transform fixture, Transform player, string path)
    {
        var observer = UnityEngine.Object.Instantiate(Camera.main);
        observer.tag = "Untagged"; observer.name = "Footwork inspection camera";
        observer.transform.SetParent(null);
        observer.GetComponent<Cinemachine.CinemachineBrain>().enabled = false;
        observer.GetComponent<AudioListener>().enabled = false;
        observer.transform.localScale = Vector3.one;
        observer.depth = Camera.main.depth + 10; observer.usePhysicalProperties = false; observer.fieldOfView = 55;
        observer.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>().volumeLayerMask = 0;
        observer.transform.position = player.position + new Vector3(2.1f, 1.1f, 3.1f);
        observer.transform.LookAt(player.position + Vector3.up * .7f);
        var lightObject = new GameObject("Footwork inspection light", typeof(Light));
        lightObject.transform.SetParent(fixture); lightObject.transform.rotation = Quaternion.Euler(45, -35, 0);
        var lamp = lightObject.GetComponent<Light>(); lamp.type = LightType.Directional;
        lightObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        lamp.intensity = 30;
        try
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
        }
        finally { UnityEngine.Object.Destroy(observer.gameObject); UnityEngine.Object.Destroy(lightObject); }
    }

    private static void Teleport(PlayerController player, UnityEngine.CharacterController capsule)
    {
        capsule.enabled = false;
        player.transform.SetPositionAndRotation(Center + Vector3.up * .5f, Quaternion.identity);
        player.ForceCameraRotation(0, 0);
        capsule.enabled = true;
        Physics.SyncTransforms();
    }

    private static IEnumerator Settle(StarterAssets.StarterAssetsInputs inputs, float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) { Zero(inputs); yield return new WaitForEndOfFrame(); }
    }

    private static void Zero(StarterAssets.StarterAssetsInputs inputs)
    {
        inputs.move = inputs.look = Vector2.zero;
        inputs.jump = inputs.sprint = inputs.interact = inputs.secondaryInteract = inputs.cancel = inputs.toggleNotebook = inputs.pause = false;
        inputs.scroll = Vector2.zero;
    }

    private static bool Support(Transform foot, out RaycastHit hit) => Physics.Raycast(
        foot.position + Vector3.up * .45f, Vector3.down, out hit, 1.15f, 1, QueryTriggerInteraction.Ignore);
    private static float Gap(Transform foot, float sole) => Support(foot, out var hit) ? foot.position.y - sole - hit.point.y : 0;
    private static void CheckSole(Transform foot, float sole, RaycastHit hit, string label)
    {
        float error = Vector3.Dot(foot.position - hit.point, hit.normal) - sole;
        Require(Mathf.Abs(error) < .025f, $"{label} foot sole misses its independent collider support by {error:F3}m.");
    }
    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    private static bool IsFinite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("PLAYER FOOTWORK CHECK: " + message); }

    private static void CheckNotApplying(PlayerController player, string label)
    {
        var component = player.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "PlayerFootwork");
        var property = component != null ? component.GetType().GetProperty("IsApplying", Members) : null;
        if (property != null && property.PropertyType == typeof(bool))
            Require(!(bool)property.GetValue(component), "Foot IK remained active during " + label + ".");
    }

    private sealed class Snapshot
    {
        private readonly PlayerController player;
        private readonly Animator animator;
        private readonly UnityEngine.CharacterController capsule;
        private readonly StarterAssets.StarterAssetsInputs inputs;
        private readonly Vector3 position, targetPosition;
        private readonly Quaternion rotation, targetRotation;
        private readonly string inputJson;
        private readonly float animatorSpeed;
        private readonly AnimatorStateInfo[] states;
        private readonly Dictionary<string, object> controllerValues = new Dictionary<string, object>();
        private readonly Dictionary<int, object> parameters = new Dictionary<int, object>();
        private readonly CursorLockMode cursorLock;
        private readonly bool cursorVisible;

        public Snapshot(PlayerController p, Animator a, UnityEngine.CharacterController c, StarterAssets.StarterAssetsInputs i)
        {
            player = p; animator = a; capsule = c; inputs = i;
            position = p.transform.position; rotation = p.transform.rotation;
            targetPosition = p.CinemachineCameraTarget.transform.localPosition;
            targetRotation = p.CinemachineCameraTarget.transform.localRotation;
            inputJson = JsonUtility.ToJson(i); animatorSpeed = a.speed;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            states = Enumerable.Range(0, a.layerCount).Select(a.GetCurrentAnimatorStateInfo).ToArray();
            foreach (var field in typeof(PlayerController).GetFields(Members))
                if (field.IsPrivate && (field.FieldType == typeof(float) || field.FieldType == typeof(bool)))
                    controllerValues[field.Name] = field.GetValue(p);
            foreach (var parameter in a.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float) parameters[parameter.nameHash] = a.GetFloat(parameter.nameHash);
                if (parameter.type == AnimatorControllerParameterType.Int) parameters[parameter.nameHash] = a.GetInteger(parameter.nameHash);
                if (parameter.type == AnimatorControllerParameterType.Bool) parameters[parameter.nameHash] = a.GetBool(parameter.nameHash);
            }
        }

        public void Restore()
        {
            capsule.enabled = false;
            player.SetFrozen(true, true, false);
            player.transform.SetPositionAndRotation(position, rotation);
            foreach (var pair in parameters)
            {
                if (pair.Value is float number) animator.SetFloat(pair.Key, number);
                else if (pair.Value is int integer) animator.SetInteger(pair.Key, integer);
                else if (pair.Value is bool boolean) animator.SetBool(pair.Key, boolean);
            }
            for (int layer = 0; layer < states.Length; layer++) animator.Play(states[layer].fullPathHash, layer, states[layer].normalizedTime);
            animator.speed = animatorSpeed;
            foreach (var pair in controllerValues) typeof(PlayerController).GetField(pair.Key, Members).SetValue(player, pair.Value);
            JsonUtility.FromJsonOverwrite(inputJson, inputs);
            player.CinemachineCameraTarget.transform.localPosition = targetPosition;
            player.CinemachineCameraTarget.transform.localRotation = targetRotation;
            capsule.enabled = true;
            Physics.SyncTransforms();
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }
    }
}

// Test-only observer: planted feet must be solved against the final body rotation.
[DefaultExecutionOrder(10000)]
public sealed class PlayerCameraTurnFrameProbe : MonoBehaviour
{
    private int ikFrame = -1;
    private float ikYaw;
    public int samples;
    public float maximumPostIkYaw;
    private void OnAnimatorIK(int layer)
    {
        if (layer != 0) return;
        ikFrame = Time.frameCount;
        ikYaw = transform.eulerAngles.y;
    }
    private void LateUpdate()
    {
        if (ikFrame != Time.frameCount) return;
        samples++;
        maximumPostIkYaw = Mathf.Max(maximumPostIkYaw, Mathf.Abs(Mathf.DeltaAngle(ikYaw, transform.eulerAngles.y)));
    }
}

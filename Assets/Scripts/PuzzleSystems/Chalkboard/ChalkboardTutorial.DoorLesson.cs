using System.Collections;
using UnityEngine;

// Ofisten ilk çıkışta oyuncu durur, kapıya döner ve kapatınca serbest kalır.
public sealed partial class ChalkboardTutorial
{
    // Kapı kanadının süpürdüğü alanın dışı; daha yakında durdurulan oyuncunun içinden kapı geçer.
    private const float DoorClearance = 1.5f;
    private EventLogicController closeDoorStep;
    private LookAction doorFocus;
    private Transform doorLookPoint;

    private void BeginDoorLesson() { StartCoroutine(DoorLesson()); }

    private IEnumerator DoorLesson()
    {
        Vector3 hinge = officeDoor.transform.position;
        var player = FindFirstObjectByType<UnityEngine.CharacterController>().transform;
        while (officeDoor.isOpen && Vector3.Distance(player.position, new Vector3(hinge.x, player.position.y, hinge.z)) < DoorClearance)
            yield return null;
        if (!officeDoor.isOpen) yield break;

        doorLookPoint = new GameObject("Kapı Dersi Odak Noktası").transform;
        doorLookPoint.position = officeDoor.GetComponentInChildren<Renderer>().bounds.center;
        doorFocus = gameObject.AddComponent<LookAction>();
        doorFocus.lookTarget = doorLookPoint;
        doorFocus.headTurnDuration = .8f;
        doorFocus.waitUntilInput = false;
        doorFocus.autoReleaseTime = float.MaxValue;
        doorFocus.Execute();
        GlobalSubtitleManager.Instance?.Show("thought_close_door_0", required: true);
        ControlsUIManager.Instance?.ShowMachineUI(ControlsUIManager.MachineType.DoorLesson);
        // Kapı kanadı isabet alanının dışında kalabilir; ders tıklamayı raycast'e bırakmaz.
        while (officeDoor.isOpen)
        {
            if (GameInput.Pressed("Interact")) officeDoor.Interact();
            yield return null;
        }
        EndDoorLesson();
    }

    private void EndDoorLesson()
    {
        if (doorFocus == null) return;
        Destroy(doorFocus);
        Destroy(doorLookPoint.gameObject);
        doorFocus = null;
        ControlsUIManager.Instance?.HideControls();
    }
}

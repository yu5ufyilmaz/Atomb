using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Isolated first-person test harness; no GameManager, saves or live enemy AI.</summary>
public sealed class LeesResistanceDemo : MonoBehaviour
{
    public Transform playerRoot, leesVisual;
    public Camera viewCamera;
    public LeesResistanceController resistance;
    public float lookSensitivity = .11f;
    Vector3 initialPlayer, initialLees, initialCamera;
    Quaternion leesRotation;
    float yaw, pitch, spottedTime;
    bool started, spotted, paused;
    CursorLockMode originalCursor;
    bool originalVisible;

    void Start()
    {
        initialPlayer = playerRoot.position; initialLees = leesVisual.position;
        initialCamera = viewCamera.transform.localPosition; leesRotation = leesVisual.rotation;
        originalCursor = Cursor.lockState; originalVisible = Cursor.visible;
        ResetDemo();
    }
    public void ResetDemo()
    {
        resistance.CancelResistance(); started = spotted = paused = false;
        yaw = pitch = spottedTime = 0; playerRoot.position = initialPlayer;
        viewCamera.transform.localPosition = initialCamera; viewCamera.transform.localRotation = Quaternion.identity;
        leesVisual.position = initialLees; leesVisual.rotation = leesRotation; leesVisual.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        resistance.hud.ShowPrompt("ARKANA BAKMA", "LEES'i gör. Arkanı dön. Kıpırdama.\nBaşını çevirmeye çalıştığında fareyi ters yöne çek.", "BAŞLAMAK İÇİN TIKLA                         R  /  YENİDEN DENE");
    }
    void Update()
    {
        var keyboard = Keyboard.current; var mouse = Mouse.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) { ResetDemo(); return; }
        if (resistance.IsRunning)
        {
            if (!resistance.IsPaused && MovementPressed()) resistance.CatchForMovement();
            if (resistance.Model.State == LeesResistanceModel.Phase.Approaching)
            {
                Vector3 behind = playerRoot.position - viewCamera.transform.forward * 1.15f;
                behind.y = initialLees.y;
                leesVisual.position = Vector3.Lerp(initialLees, behind, Mathf.SmoothStep(0, 1, resistance.Model.Elapsed / Mathf.Max(.01f, resistance.tuning.approachSeconds)));
                Vector3 face = playerRoot.position - leesVisual.position; face.y = 0;
                if (face.sqrMagnitude > .001f) leesVisual.rotation = Quaternion.LookRotation(face);
            }
            return;
        }
        if (resistance.Model.State == LeesResistanceModel.Phase.Survived || resistance.Model.State == LeesResistanceModel.Phase.Caught)
        {
            if (resistance.Model.State == LeesResistanceModel.Phase.Survived) leesVisual.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return;
        }
        if (!started || paused)
        {
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                started = true; paused = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            return;
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { PauseDemo(); return; }
        Vector2 look = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        yaw += look.x * lookSensitivity; pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, -65, 65);
        viewCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 towardLees = (initialLees + Vector3.up * 1.6f - viewCamera.transform.position).normalized;
        float facing = Vector3.Dot(viewCamera.transform.forward, towardLees);
        if (!spotted)
        {
            spottedTime = facing > .8f ? spottedTime + Time.deltaTime : 0;
            if (spottedTime > .3f) spotted = true;
        }
        resistance.hud.ShowPrompt(spotted ? "ARKANI DÖN" : "LEES'İ GÖR", spotted ? "Seni fark etti. Arkana bakma.\nFareyle arkanı dön ve sabit kal." : "Koridorun ucunda seni bekliyor.", "FARE  /  ETRAFA BAK                         ESC  /  DURAKLAT");
        if (spotted && facing < -.8f && !MovementPressed()) resistance.BeginResistance();
    }
    static bool MovementPressed()
    {
        var k = Keyboard.current;
        return k != null && (k.wKey.isPressed || k.aKey.isPressed || k.sKey.isPressed || k.dKey.isPressed || k.upArrowKey.isPressed || k.downArrowKey.isPressed || k.leftArrowKey.isPressed || k.rightArrowKey.isPressed || k.spaceKey.isPressed);
    }
    void PauseDemo()
    {
        paused = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        resistance.hud.ShowPrompt("DURAKLATILDI", "Devam etmek için tıkla.", "");
    }
    void OnApplicationFocus(bool focus) { if (!focus && started && !resistance.IsRunning) PauseDemo(); }
    void OnDisable() { if (resistance != null) resistance.CancelResistance(); Cursor.lockState = originalCursor; Cursor.visible = originalVisible; }
}

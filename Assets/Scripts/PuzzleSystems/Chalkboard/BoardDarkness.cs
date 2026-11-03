using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tahta kartları, tebeşir izleri ve yazılar ışıksız çizildiği için karanlıkta parlıyordu (3 Kasım oyun testi).
// uGUI ışık almaz; tahtanın önündeki siyah perde oda ışıkları yanarken saydam, ışıklar sönükken siyahtır, fener tutulunca açılır.
[RequireComponent(typeof(ChalkboardManager))]
public class BoardDarkness : MonoBehaviour
{
    [Tooltip("Bu mesafedeki, tahtayı doğrudan gören ışıklar odanın ışığı sayılır.")]
    public float roomLightRadius = 7f;

    [Tooltip("Fener tahtaya tutulduğunda perdenin ne kadar açılacağı.")]
    [Range(0f, 1f)] public float flashlightReveal = .9f;

    public float fadeSeconds = .2f;

    private ChalkboardManager board;
    private Image veil;
    private Light flashlight;
    private readonly List<Light> roomLights = new List<Light>();

    private void Start()
    {
        board = GetComponent<ChalkboardManager>();
        Transform surface = board.boardTransform;
        Vector2 min = board.minBounds - Vector2.one * .25f, max = board.maxBounds + Vector2.one * .25f;
        var holder = new GameObject("Karanlık Perdesi", typeof(RectTransform), typeof(Canvas), typeof(Image));
        var rect = (RectTransform)holder.transform;
        rect.SetParent(surface, false);
        // Documents sit at local z .035 on the board's +Z front; the veil goes just in front of everything.
        rect.localPosition = new Vector3((min.x + max.x) * .5f, (min.y + max.y) * .5f, .08f);
        rect.localRotation = Quaternion.Euler(0, 180, 0);
        rect.sizeDelta = max - min;
        var canvas = holder.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        veil = holder.GetComponent<Image>();
        veil.color = Color.clear;
        veil.raycastTarget = false;
        veil.enabled = false;

        var dynamo = FindFirstObjectByType<DynomaFlashLight>();
        flashlight = dynamo != null ? dynamo.GetComponentInChildren<Light>(true) : null;
        Vector3 front = surface.TransformPoint(new Vector3((min.x + max.x) * .5f, (min.y + max.y) * .5f, .3f));
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light == flashlight || light.type == LightType.Directional) continue;
            Vector3 to = light.transform.position - front;
            if (to.magnitude > roomLightRadius) continue;
            // Walls between the board and a light mean another room; the lamp's own housing doesn't count.
            if (Physics.Linecast(front, light.transform.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.distance < to.magnitude - .6f) continue;
            roomLights.Add(light);
        }
    }

    private void Update()
    {
        bool roomLit = false;
        foreach (Light light in roomLights)
            if (light != null && light.isActiveAndEnabled && light.intensity > 0f) { roomLit = true; break; }
        float lit = roomLit ? 1f : FlashlightOnBoard() * flashlightReveal;
        Color c = veil.color;
        c.a = Mathf.MoveTowards(c.a, 1f - lit, Time.deltaTime / Mathf.Max(fadeSeconds, .01f));
        veil.color = c;
        veil.enabled = c.a > .001f;
    }

    // 1 when the flashlight cone's inner half points at the board centre within range.
    private float FlashlightOnBoard()
    {
        if (flashlight == null || !flashlight.isActiveAndEnabled || flashlight.intensity <= 0f) return 0f;
        Vector3 center = veil.rectTransform.position;
        Vector3 to = center - flashlight.transform.position;
        float distance = to.magnitude;
        if (distance > flashlight.range || distance < .01f) return 0f;
        float outer = Mathf.Cos(flashlight.spotAngle * .5f * Mathf.Deg2Rad);
        float cos = Vector3.Dot(flashlight.transform.forward, to / distance);
        return Mathf.Clamp01((cos - outer) / ((1f - outer) * .5f));
    }
}

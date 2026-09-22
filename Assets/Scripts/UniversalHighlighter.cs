using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

public class UniversalHighlighter : MonoBehaviour
{
    public enum HighlightMode
    {
        Static, // Sabit parlama
        Blinking, // Yanıp sönen parlama
    }

    [Header("Highlight Modu")]
    public HighlightMode mode = HighlightMode.Static;
    public HDRPOutlineController outlineController;

    [Header("Emission (Parlama) Ayarları")]
    public Color highlightColor = new Color(1f, 0.8f, 0f);

    [Range(0f, 100f)]
    public float emissionIntensity = 10f;
    public List<Renderer> excludeRenderers = new List<Renderer>();

    [ShowIf("mode", HighlightMode.Blinking)]
    [Header("Blink (Yanıp Sönme) Ayarları")]
    public float blinkSpeed = 3.0f;

    [ShowIf("mode", HighlightMode.Blinking)]
    [Range(0f, 1f)]
    public float minBlinkAlpha = 0.1f; // Tamamen kararmasın, taban ışık kalsın

    [Header("Ekstra Eylemler (Actions / Events)")]
    public UnityEvent onFocusGained;
    public UnityEvent onFocusLost;

    private class RendererHighlightData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] highlightMaterials;
    }

    private List<RendererHighlightData> allRenderersData = new List<RendererHighlightData>();
    private bool isHighlighted = false;
    private Coroutine blinkCoroutine;

    private void Start()
    {
        if (outlineController == null)
            outlineController = GetComponentInChildren<HDRPOutlineController>();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (excludeRenderers.Contains(r))
                continue;

            RendererHighlightData data = new RendererHighlightData();
            data.renderer = r;
            data.originalMaterials = r.materials;
            data.highlightMaterials = new Material[data.originalMaterials.Length];

            for (int i = 0; i < data.originalMaterials.Length; i++)
            {
                Material mat = new Material(data.originalMaterials[i]);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                data.highlightMaterials[i] = mat;
            }

            allRenderersData.Add(data);
        }

        UpdateEmissionColor(emissionIntensity);
    }

    private void UpdateEmissionColor(float intensity)
    {
        Color finalEmission = highlightColor * intensity;
        foreach (var data in allRenderersData)
        {
            if (data.highlightMaterials == null)
                continue;
            for (int i = 0; i < data.highlightMaterials.Length; i++)
            {
                Material mat = data.highlightMaterials[i];
                if (mat == null)
                    continue;

                if (mat.HasProperty("_EmissiveColor"))
                    mat.SetColor("_EmissiveColor", finalEmission);
                else if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", finalEmission);
            }
        }
    }

    public void EnableHighlight()
    {
        if (isHighlighted)
            return;
        isHighlighted = true;

        if (outlineController != null)
        {
            outlineController.ToggleOutline(true);
        }
        else
        {
            foreach (var data in allRenderersData)
            {
                if (data.renderer != null)
                    data.renderer.materials = data.highlightMaterials;
            }
        }

        if (mode == HighlightMode.Blinking)
        {
            StartBlinking();
        }

        onFocusGained?.Invoke();
    }

    public void DisableHighlight()
    {
        if (!isHighlighted)
            return;
        isHighlighted = false;

        StopBlinking();

        if (outlineController != null)
        {
            outlineController.ToggleOutline(false);
        }
        else
        {
            foreach (var data in allRenderersData)
            {
                if (data.renderer != null)
                    data.renderer.materials = data.originalMaterials;
            }
        }

        onFocusLost?.Invoke();
    }

    // --- Action/Event ile Mod Değiştirme Fonksiyonları ---

    public void SetBlink(bool enableBlink)
    {
        mode = enableBlink ? HighlightMode.Blinking : HighlightMode.Static;

        if (isHighlighted)
        {
            if (enableBlink)
                StartBlinking();
            else
            {
                StopBlinking();
                UpdateEmissionColor(emissionIntensity);
            }
        }
    }

    private void StartBlinking()
    {
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void StopBlinking()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (isHighlighted)
        {
            float pingPong = Mathf.PingPong(Time.time * blinkSpeed, 1f);
            float currentAlpha = Mathf.Lerp(minBlinkAlpha, 1f, pingPong);
            UpdateEmissionColor(emissionIntensity * currentAlpha);
            yield return null;
        }
    }
}

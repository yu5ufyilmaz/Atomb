using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Localization/Localized UI Variant")]
public sealed class LocalizedUIVariant : MonoBehaviour
{
    [Serializable]
    public sealed class ObjectState
    {
        public GameObject target;
        public bool active = true;
    }

    [Serializable]
    public sealed class ImageSprite
    {
        public Image target;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class RawImageTexture
    {
        public RawImage target;
        public Texture texture;
    }

    [Serializable]
    public sealed class LocaleVariant
    {
        public string languageCode;
        public ObjectState[] objects = Array.Empty<ObjectState>();
        public ImageSprite[] sprites = Array.Empty<ImageSprite>();
        public RawImageTexture[] textures = Array.Empty<RawImageTexture>();
    }

    public LocaleVariant[] variants = Array.Empty<LocaleVariant>();

    private readonly Dictionary<GameObject, bool> originalObjects = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Image, Sprite> originalSprites = new Dictionary<Image, Sprite>();
    private readonly Dictionary<RawImage, Texture> originalTextures = new Dictionary<RawImage, Texture>();

    private void OnEnable()
    {
        GameLocalization.Initialize();
        GameLocalization.LanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= Apply;
    }

    public void Apply()
    {
        GameLocalization.Initialize();
        CaptureBaselines();
        foreach (var value in originalObjects)
            if (CanToggle(value.Key)) value.Key.SetActive(value.Value);
        foreach (var value in originalSprites)
            if (value.Key != null) value.Key.sprite = value.Value;
        foreach (var value in originalTextures)
            if (value.Key != null) value.Key.texture = value.Value;

        if (variants == null) return;
        foreach (var variant in variants)
        {
            if (variant == null || !string.Equals(variant.languageCode, GameLocalization.CurrentCode, StringComparison.OrdinalIgnoreCase)) continue;
            if (variant.objects != null)
                foreach (var value in variant.objects)
                    if (value != null && CanToggle(value.target)) value.target.SetActive(value.active);
            if (variant.sprites != null)
                foreach (var value in variant.sprites)
                    if (value != null && value.target != null) value.target.sprite = value.sprite;
            if (variant.textures != null)
                foreach (var value in variant.textures)
                    if (value != null && value.target != null) value.target.texture = value.texture;
            break;
        }
    }

    private bool CanToggle(GameObject target)
    {
        // Keeping this component and its ancestors active preserves future language changes.
        return target != null && target != gameObject && !transform.IsChildOf(target.transform);
    }

    private void CaptureBaselines()
    {
        if (variants == null) return;
        foreach (var variant in variants)
        {
            if (variant == null) continue;
            if (variant.objects != null)
                foreach (var value in variant.objects)
                    if (value != null && CanToggle(value.target) && !originalObjects.ContainsKey(value.target))
                        originalObjects.Add(value.target, value.target.activeSelf);
            if (variant.sprites != null)
                foreach (var value in variant.sprites)
                    if (value != null && value.target != null && !originalSprites.ContainsKey(value.target))
                        originalSprites.Add(value.target, value.target.sprite);
            if (variant.textures != null)
                foreach (var value in variant.textures)
                    if (value != null && value.target != null && !originalTextures.ContainsKey(value.target))
                        originalTextures.Add(value.target, value.target.texture);
        }
    }
}

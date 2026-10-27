using System;
using System.Collections.Generic;
using UnityEngine;

// A dose-based game model, independent of signal traces.
public static class ColorMixtureLibrary
{
    public const string Source = "mix-source", Meter = "mix-meter", Splitter = "mix-splitter", Mixer = "mix-mixer";
    public const string Red = "mix-red", Green = "mix-green", Blue = "mix-blue", Solvent = "mix-solvent", Concentrator = "mix-concentrator";
    public static readonly IReadOnlyList<SignalPartLibrary.Part> All = Array.AsReadOnly(new[] {
        Part(Source, "Taşıyıcı Kaynağı", "SIVI", 0, 1, false, "1 doz renksiz taşıyıcı sıvı verir. Pigment içermez."),
        Part(Red, "Kırmızı Pigment", "KIRMIZI", 1, 1, true, "Geçen karışıma 1 doz kırmızı pigment ekler. Miktar da 1 doz artar."),
        Part(Green, "Yeşil Pigment", "YEŞİL", 1, 1, true, "Geçen karışıma 1 doz yeşil pigment ekler. Miktar da 1 doz artar."),
        Part(Blue, "Mavi Pigment", "MAVİ", 1, 1, true, "Geçen karışıma 1 doz mavi pigment ekler. Miktar da 1 doz artar."),
        Part(Solvent, "Çözücü", "SEYRELT", 1, 1, false, "1 doz renksiz taşıyıcı ekler. Pigment oranları korunur; yoğunluk azalır, toplam miktar artar."),
        Part(Concentrator, "Yoğunlaştırıcı", "YOĞUN", 1, 1, false, "Renksiz taşıyıcının yarısını uzaklaştırır. Pigment miktarı korunur; yoğunluk artar, toplam miktar azalır."),
        Part(Splitter, "Sıvı Dağıtıcı", "İKİYE BÖL", 1, 2, false, "Karışımı iki eşit kola böler. Her kolda bütün bileşenlerin yarısı bulunur; toplam miktar korunur."),
        Part(Mixer, "Karıştırma Kabı", "KARIŞTIR", 2, 1, false, "İki girişten gelen sıvıları toplar. Renk pigment miktarlarına göre oluşur; iki giriş de bağlanmalı."),
        Part(Meter, "Karışım Analizörü", "ANALİZ", 1, 0, false, "Son karışımın rengini, pigment yoğunluğunu ve toplam miktarını ölçer. Çıkışı yoktur.")
    });
    private static SignalPartLibrary.Part Part(string id, string title, string symbol, int inputs, int outputs, bool sample, string description)
        => new SignalPartLibrary.Part { id = id, title = title, symbol = symbol, inputs = inputs, outputs = outputs, sample = sample, description = description };
    public static SignalPartLibrary.Part Find(string id)
    {
        foreach (var part in All) if (part.id == id) return part;
        return null;
    }
    public static Color Tint(string id) => id == Red ? new Color(.9f, .25f, .25f) : id == Green ? new Color(.25f, .7f, .4f)
        : id == Blue ? new Color(.25f, .5f, .95f) : new Color(.7f, .83f, .85f);
}

[Serializable]
public sealed class MixtureResult
{
    public float red, green, blue, solvent;
    public float Pigment => red + green + blue;
    public float Volume => Pigment + solvent;
    public float Concentration => Volume > 0 ? Pigment / Volume : 0;
    public Vector3 Ratios => Pigment > 0 ? new Vector3(red, green, blue) / Pigment : Vector3.zero;
    public Color Swatch => Pigment > 0 ? Color.Lerp(Color.white, new Color(Ratios.x, Ratios.y, Ratios.z), Concentration) : Color.white;
    public MixtureResult Copy() => (MixtureResult)MemberwiseClone();
    public void Add(MixtureResult other, float fraction = 1)
    { red += other.red * fraction; green += other.green * fraction; blue += other.blue * fraction; solvent += other.solvent * fraction; }
    public string Describe() => $"Yoğunluk: {Concentration:P0} · Miktar: {Volume:0.##} doz\nPigment K/Y/M: {Ratios.x:P0} / {Ratios.y:P0} / {Ratios.z:P0}";
    public string Encode(bool target) => (target ? "mixture-target/" : "mixture-result/") + JsonUtility.ToJson(this);
    public static MixtureResult Decode(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('/') < 0) return null;
        try { return JsonUtility.FromJson<MixtureResult>(value.Substring(value.IndexOf('/') + 1)); }
        catch (ArgumentException) { return null; }
    }
}

using System;
using System.Collections.Generic;

// One catalog for simulation, editor, physical ports and printed/chalk drawings.
public static class SignalPartLibrary
{
    [Serializable]
    public sealed class Part
    {
        public string id, title, symbol, description;
        public int inputs = 1, outputs = 1;
        public bool sample;
        public float width, heightFactor = 1, delay;
        public int repeats = 1;
        public float repeatInterval;
    }

    public static readonly IReadOnlyList<Part> All = Array.AsReadOnly(new[] {
        new Part { id = "pulse-source", title = "Atım Kaynağı", symbol = "ATIM", inputs = 0,
            description = "Başlangıç: 0,18 anında tek uyarım yollar. Yükseklik 1; giriş yok, bir çıkış." },
        new Part { id = "pulse-n12", title = "Numune N-12", symbol = "N-12", sample = true, width = .025f,
            description = "Kısa tepki: gelen izin genişliğini 0,025 yapar. Zaman ve yükseklik korunur." },
        new Part { id = "pulse-meter", title = "Çift Kanallı Ölçer", symbol = "ÖLÇ", inputs = 2, outputs = 0,
            description = "Sonuç: bir veya iki girişten gelen izleri aynı kayda çizer. Çıkış yok." },
        new Part { id = "pulse-splitter", title = "Dağıtıcı", symbol = "DAĞ", outputs = 2,
            description = "Gelen her izi iki çıkışına da iletir. Her çıkışa ayrı bir hat bağlanır." },
        new Part { id = "pulse-delay", title = "Geciktirici", symbol = "GEC", delay = .32f,
            description = "Her izi 0,32 zaman birimi geciktirir. Yükseklik ve genişlik korunur." },
        new Part { id = "pulse-attenuator", title = "Sönümleyici", symbol = "SÖN", heightFactor = .5f,
            description = "Her izin yüksekliğini yarıya indirir. Zaman ve genişlik korunur." },
        new Part { id = "pulse-n34", title = "Numune N-34", symbol = "N-34", sample = true, width = .075f,
            description = "Geniş tepki: gelen izin genişliğini 0,075 yapar. Zaman ve yükseklik korunur." },
        new Part { id = "pulse-n08", title = "Numune N-08", symbol = "N-08", sample = true, width = .025f, heightFactor = .5f,
            description = "Zayıf tepki: genişliği 0,025 yapar ve yüksekliği yarıya indirir. Zaman korunur." },
        new Part { id = "pulse-n21", title = "Numune N-21", symbol = "N-21", sample = true, width = .025f, delay = .16f,
            description = "Geç tepki: genişliği 0,025 yapar ve 0,16 zaman birimi geciktirir. Yükseklik korunur." },
        new Part { id = "pulse-n46", title = "Numune N-46", symbol = "N-46", sample = true, repeats = 2, repeatInterval = .32f,
            description = "Çift tepki: her giriş izini iki kez yollar; ikinci iz 0,32 sonra gelir. Genişlik ve yükseklik korunur. Tek çıkış hattı yeterlidir." }
    });

    public static Part Find(string id)
    {
        foreach (var part in All) if (part.id == id) return part;
        return null;
    }
}

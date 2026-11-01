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
            description = "0,18 anında tek bir uyarım yollar. Girişi yok, bir çıkışı var." },
        new Part { id = "pulse-n12", title = "Numune N-12", symbol = "N-12", sample = true, width = .025f,
            description = "Kısa tepki: izi inceltir. Zaman ve yükseklik değişmez." },
        new Part { id = "pulse-meter", title = "Çift Kanallı Ölçer", symbol = "ÖLÇ", inputs = 2, outputs = 0,
            description = "Bir veya iki girişten gelen izleri aynı kayda çizer." },
        new Part { id = "pulse-splitter", title = "Dağıtıcı", symbol = "DAĞ", outputs = 2,
            description = "Gelen izi iki çıkışa da iletir. Her çıkışa ayrı hat bağla." },
        new Part { id = "pulse-delay", title = "Geciktirici", symbol = "GEC", delay = .32f,
            description = "İzi 0,32 birim geciktirir. Yükseklik ve genişlik değişmez." },
        new Part { id = "pulse-attenuator", title = "Sönümleyici", symbol = "SÖN", heightFactor = .5f,
            description = "İzin yüksekliğini yarıya indirir. Zaman ve genişlik değişmez." },
        new Part { id = "pulse-n34", title = "Numune N-34", symbol = "N-34", sample = true, width = .075f,
            description = "Geniş tepki: izi genişletir. Zaman ve yükseklik değişmez." },
        new Part { id = "pulse-n08", title = "Numune N-08", symbol = "N-08", sample = true, width = .025f, heightFactor = .5f,
            description = "Zayıf tepki: izi inceltir ve yüksekliği yarıya indirir." },
        new Part { id = "pulse-n21", title = "Numune N-21", symbol = "N-21", sample = true, width = .025f, delay = .16f,
            description = "Geç tepki: izi inceltir ve 0,16 birim geciktirir." },
        new Part { id = "pulse-n46", title = "Numune N-46", symbol = "N-46", sample = true, repeats = 2, repeatInterval = .32f,
            description = "Çift tepki: her izi iki kez yollar; ikincisi 0,32 sonra gelir." }
    });

    public static Part Find(string id)
    {
        foreach (var part in All) if (part.id == id) return part;
        return null;
    }
}

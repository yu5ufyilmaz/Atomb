using System;

[Flags]
public enum PuzzleBookTopic
{
    Foundations = 1, SignalSamples = 2, SignalProcessing = 4,
    ColorMaterials = 8, MixtureProcessing = 16, All = 31
}

[Serializable]
public sealed class PuzzleBookProfile
{
    public string bookID;
    public string title;
    public string location;
    public PuzzleBookTopic topics = PuzzleBookTopic.All;
    public int pageCount = 8;

    public static PuzzleBookProfile Default(int index, string id, bool library = false)
    {
        string[][] titles = {
            new[] { "Deneycinin El Kitabı", "Laboratuvarda İlk Adım", "Kaynaklar ve Ölçerler", "Akışın Temelleri", "Deney Güvenliği", "Ölçümün İzinde", "Aygıtların Dili", "Gözlem ve Kayıt", "Deney Masası", "Araştırmacının Defteri" },
            new[] { "Numunelerin Hafızası", "İnce İzler Atlası", "Tepkinin Biçimi", "Geciken Yanıtlar", "Yankılı Numuneler", "Kristallerin Yanıtı", "Kısa ve Uzun Tepkiler", "Numune Portreleri", "İzlerin Kökeni", "Sinyal Koleksiyonu" },
            new[] { "İletimin Yolları", "Zaman ve Gecikme", "Sönük Tepkiler", "Ayrılan Hatlar", "Birleşen İzler", "Uyarının Yolculuğu", "Devre Günlükleri", "Tepkinin Dönüşümü", "Hatlar Arasında", "Uzak İstasyon Kayıtları" },
            new[] { "Pigment Atlası", "Rengin Bileşenleri", "Kırmızıdan Maviye", "Yeşilin Kökeni", "Boyaların Hafızası", "Renkli Maddeler", "Kaplama Numuneleri", "Pigment Günlükleri", "Rengin İzinde", "Malzeme Koleksiyonu" },
            new[] { "Maddenin Kararsız Halleri", "Karışımın Dengesi", "Çözücü Günlükleri", "Yoğunluğun İzinde", "Taşıyıcının Rolü", "Seyreltme Sanatı", "Birleşen Karışımlar", "Akışkanların Davranışı", "Kapta Kalanlar", "Dönüşüm Notları" }
        };
        int topic = library ? index % titles.Length : 0;
        return new PuzzleBookProfile {
            bookID = id, title = library ? titles[topic][index / titles.Length % titles[topic].Length]
                : new[] { "Deneycinin El Kitabı", "Numune Atlası", "İşlem Rehberi", "Araştırma Günlüğü" }[index % 4],
            topics = library ? (PuzzleBookTopic)(1 << topic) : PuzzleBookTopic.All,
            pageCount = library ? 4 : 8, location = "Araştırma masası"
        };
    }
}

[Serializable]
public sealed class PuzzleResearchReference
{
    public string noteID;
    public string bookID;
    public string bookTitle;
    public string location;
    public int pageIndex;
    public PuzzleResearchReference Clone() => (PuzzleResearchReference)MemberwiseClone();
}

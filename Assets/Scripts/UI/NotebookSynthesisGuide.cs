using System.Collections.Generic;

public static class NotebookSynthesisGuide
{
    public sealed class Entry
    {
        public string title;
        public string text;
        public SynthesisQuestData solution;
    }

    public static List<Entry> Build(ChalkboardManager board, FormulaCatalog catalog, NotebookData notebook)
    {
        var entries = new List<Entry>();
        if (board == null || board.practiceQuest == null) return entries;
        entries.Add(new Entry {
            title = board.practiceQuest.title,
            text = "Hedefi ve araştırma notlarını kitaplardan deftere al. Hedefi tahtaya sabitle; parçaları defterden kopyala. "
                + "Çıkış oklarını giriş halkalarına bağla. DENEYİ ÖLÇ ile çıkan kaydı deftere alıp Turing okuyucusuna taşı. "
                + "Kodu Enter ile sına; sonucu hedefin yüksekliği, genişliği ve zaman aralığıyla karşılaştır."
        });
        return entries;
    }
}

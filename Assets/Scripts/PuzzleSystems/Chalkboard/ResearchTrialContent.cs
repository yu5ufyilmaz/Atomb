using System;
using System.Collections.Generic;
using UnityEngine;

// Yalnız araştırma deneme sahnesinin Play oturumu için üretilen içerik.
public sealed class ResearchTrialContent
{
    public const string FirstQuestID = "research-arrange";
    public const string SecondQuestID = "research-diagnose";

    private const string SourceID = "research-source";
    private const string SeparatorID = "research-separator";
    private const string SampleID = "research-sample";
    private const string ReferenceID = "research-reference";
    private const string AbsorberID = "research-absorber";
    private const string DetectorID = "research-detector";

    public List<FormulaItemSO> Parts = new List<FormulaItemSO>();
    public List<QuestBlueprintSO> Quests = new List<QuestBlueprintSO>();
    public List<BookWorkspacePage>[] Books = new List<BookWorkspacePage>[4];
    public List<SynthesisBondData> FailedBonds = new List<SynthesisBondData>();

    public static ResearchTrialContent Create(FormulaItemSO template)
    {
        if (template == null || template.chalkPrefab == null)
            throw new ArgumentException("Araştırma denemesi dört normal uçlu tebeşir şablonu gerektirir.", nameof(template));

        var content = new ResearchTrialContent();
        content.AddPart(template, SourceID, "Uyarıcı", 1,
            "Ham işaret üretir. Kapasite 1; diğer bileşenlerle ilişkisi Genel İlkeler / Ham uyarım kaydındadır.");
        content.AddPart(template, SeparatorID, "Ayırıcı", 4,
            "Ham kolu ölçüm tarafından ayırır. Kapasite 4; artık kolunun yeri deneyin yöntemine bağlıdır.");
        content.AddPart(template, SampleID, "Numune", 4,
            "Araştırılan örnek. Kapasite 4; duyarlı ölçüm hattının bir ucudur. Ölçüm komşusu deney kaydında belirtilir.");
        content.AddPart(template, ReferenceID, "Referans", 2,
            "Karşılaştırma sağlar. Kapasite 2; doğrudan ve bağımsız karşılaştırma yöntemlerinde aynı komşuları kullanmaz.");
        content.AddPart(template, AbsorberID, "Sönümleyici", 2,
            "Artık veya dönüş kolunda kullanılır. Kapasite 2; boş uçları doldurmak gerekmez. Yeri için deney yöntemine bak.");
        content.AddPart(template, DetectorID, "Algılayıcı", 4,
            "Ölçümü ham koldan ayrı karşılaştırır. Kapasite 4; duyarlı hattı ve dönüş koşulu D-21 / D-22'de araştırılmıştır.");

        var first = content.AddQuest(FirstQuestID, "A / Doğrudan karşılaştırma", 5,
            "Numuneyi ham uyarımdan ayıran doğrudan karşılaştırma düzenini kur. Kaynak: Deney Kayıtları / D-11 ve D-12.");
        first.nodes[0].position = new Vector2(-1.32f, .58f);
        first.nodes[1].position = new Vector2(-.72f, .58f);
        first.nodes[2].position = new Vector2(-.10f, .58f);
        first.nodes[3].position = new Vector2(-.10f, -.30f);
        first.nodes[4].position = new Vector2(-.72f, -.52f);
        first.bonds.AddRange(new[] { Bond(0, 1), Bond(1, 2, BondType.Double), Bond(2, 3), Bond(1, 4) });

        var second = content.AddQuest(SecondQuestID, "B / Bağımsız karşılaştırma", 6,
            "D-20'deki başarısız önerinin iki hatasını düzelt. Yeni koşullar: Deney Kayıtları / D-21 ve D-22.");
        second.nodes[0].position = new Vector2(-1.33f, .62f);
        second.nodes[1].position = new Vector2(-.78f, .62f);
        second.nodes[2].position = new Vector2(.14f, -.20f);
        second.nodes[3].position = new Vector2(-1.22f, -.12f);
        second.nodes[4].position = new Vector2(-.60f, -.63f);
        second.nodes[5].position = new Vector2(-.12f, .38f);
        second.bonds.AddRange(new[]
        {
            Bond(0, 1), Bond(1, 5), Bond(5, 2, BondType.Double),
            Bond(5, 3), Bond(2, 4), Bond(4, 3)
        });
        content.FailedBonds.AddRange(new[]
        {
            Bond(0, 1), Bond(1, 5), Bond(5, 2), Bond(5, 3), Bond(2, 4), Bond(4, 1)
        });
        content.BuildBooks();
        return content;
    }

    public static bool OwnsQuest(string questID) => questID == FirstQuestID || questID == SecondQuestID || questID == SecondTraceExperiment.QuestID || questID == SignalTrial.QuestID;

    // Görünüm kopyası doğrulama şemasını veya kaydedilmiş hedefi değiştirmez.
    public SynthesisQuestData DisplayQuest(SynthesisQuestData quest)
    {
        if (quest == null) return null;
        var copy = JsonUtility.FromJson<SynthesisQuestData>(JsonUtility.ToJson(quest));
        if (copy.questID == FirstQuestID)
            copy.bonds.Clear();
        else if (copy.questID == SecondQuestID)
        {
            copy.title = "B / BAŞARISIZ KAYIT D-20";
            copy.bonds = CopyBonds(FailedBonds);
        }
        return copy;
    }

    public bool Evaluate(string questID, SynthesisGraphData graph, out string message)
    {
        var blueprint = Quests.Find(quest => quest != null && quest.questID == questID);
        if (blueprint == null)
            return Fail("Bu araştırma hedefi deneme arşivinde bulunamadı.", out message);

        bool valid = SynthesisGraphValidator.Validate(graph, blueprint.CreateQuest(), out string reason);
        if (valid)
        {
            message = questID == FirstQuestID
                ? "Şema D-11 / D-12 ile uyumlu: ham kol ve doğrudan karşılaştırma ayrıldı. Parçanın görevi, komşularına da bağlıdır."
                : "Şema D-21 / D-22 ile uyumlu: duyarlı hat çift, dönüş yolu ham koldan bağımsız. Eski yerleşimi değil, yalıtım ilkesini aktardın.";
            return true;
        }
        if (graph == null || graph.parts == null || graph.parts.Count == 0 || graph.bonds == null)
            return Fail("Henüz okunabilir bir düzen yok. Hedefteki bileşenlerle tahtada bir hipotez kur.", out message);

        // Kimlikleri açık olan hedefte eksik/fazla bileşen, bir bilgi toplama eşiği değildir.
        var indices = new Dictionary<string, int>();
        for (int i = 0; i < graph.parts.Count; i++)
        {
            var part = graph.parts[i];
            if (part == null || string.IsNullOrEmpty(part.itemID) || indices.ContainsKey(part.itemID))
                return Fail("Bileşen kaydı eksik veya yinelenmiş. Hedefte gösterilen farklı bileşenleri kullan.", out message);
            indices.Add(part.itemID, i);
        }
        if (indices.Count != blueprint.nodes.Count || blueprint.nodes.Exists(node => !indices.ContainsKey(node.itemID)))
            return Fail("Düzendeki bileşenler hedefte adı verilenlerle uyuşmuyor. Parça Atlası çizimlerini karşılaştır.", out message);

        var links = new int[graph.parts.Count, graph.parts.Count];
        foreach (var bond in graph.bonds)
        {
            if (bond == null || bond.fromIndex < 0 || bond.fromIndex >= graph.parts.Count
                || bond.toIndex < 0 || bond.toIndex >= graph.parts.Count || bond.fromIndex == bond.toIndex
                || (bond.bondType != BondType.Single && bond.bondType != BondType.Double)
                || links[bond.fromIndex, bond.toIndex] != 0)
                return Fail("Bir bağ kaydı okunamıyor. Uçları ve tek / çift çizgi durumunu denetle.", out message);
            links[bond.fromIndex, bond.toIndex] = links[bond.toIndex, bond.fromIndex] = (int)bond.bondType;
        }
        for (int i = 0; i < graph.parts.Count; i++)
        {
            int used = 0;
            for (int j = 0; j < graph.parts.Count; j++) used += links[i, j];
            var part = graph.parts[i];
            if (part.capacity < used || part.capacity < 0 || part.openEnds < 0 || part.blockedEnds < 0)
                return Fail("Bir bileşenin kapasite / uç koşulu sağlanmıyor. Çift çizgi iki kapasite kullanır; Atlas'a bak.", out message);
        }

        int source = indices[SourceID], separator = indices[SeparatorID], sample = indices[SampleID];
        int reference = indices[ReferenceID], absorber = indices[AbsorberID];
        if (links[source, separator] != 1 || Neighbors(links, source) != 1)
            return Fail("Ham uyarımın yalıtım koşulu sağlanmıyor. Genel İlkeler / Ham uyarım kaydını yeniden yorumla.", out message);

        if (questID == FirstQuestID)
        {
            if (links[separator, sample] != 2)
                return Fail("Duyarlı ölçüm hattı D-11'in koşulunu sağlamıyor. Komşuluk ile çizgi türünü birlikte denetle.", out message);
            if (links[sample, reference] != 1 || Neighbors(links, reference) != 1)
                return Fail("Doğrudan karşılaştırma kolu D-11'in tarifine uymuyor. Referansın hangi ilişkiyi karşılaştırdığını düşün.", out message);
            if (links[separator, absorber] != 1 || Neighbors(links, absorber) != 1)
                return Fail("Artık kolu D-12'de korunan koşulu sağlamıyor. İki denemenin değişen tek komşuluğunu karşılaştır.", out message);
        }
        else
        {
            int detector = indices[DetectorID];
            if (links[detector, sample] != 2)
                return Fail("Duyarlı ölçüm hattının bağlantı koşulu D-22 ile uyuşmuyor. Aynı uçlardaki tek ve çift çizgiyi karşılaştır.", out message);
            if (links[separator, detector] != 1 || Neighbors(links, separator) != 2)
                return Fail("Ham kolun ayrılığı D-21'in koşulunu sağlamıyor. Eski yalıtım ilkesini yeni dönüş yoluna uygula.", out message);
            if (links[detector, reference] != 1)
                return Fail("Bağımsız karşılaştırmanın referans kolu D-21'deki korunmuş ilişkiyle uyuşmuyor.", out message);
            if (links[sample, absorber] != 1 || links[absorber, reference] != 1 || Neighbors(links, absorber) != 2)
                return Fail("Dönüş yolu D-21'in bağımsızlık koşulunu sağlamıyor. Korunan numune kolu ile dönüş ucunu birlikte incele.", out message);
        }
        // Yukarıdaki yerel koşulları geçse de ek çizgi veya başka bir yapısal hata kabul edilmez.
        return Fail(reason == "Bütün parçalar aynı bağlı yapıda olmalı."
            ? "Düzen kopuk: bütün bileşenler aynı bağlı yapıya katılmalı."
            : "Kayıtlarda tanımlanmayan ek bir hat veya bağ türü var. Genel İlkeler / Deney sınırı notuyla karşılaştır.", out message);
    }

    public void Dispose()
    {
        foreach (var quest in Quests) DestroyRuntime(quest);
        foreach (var part in Parts) DestroyRuntime(part);
        Quests.Clear();
        Parts.Clear();
        FailedBonds.Clear();
        foreach (var pages in Books) pages?.Clear();
    }

    internal void AddPart(FormulaItemSO template, string id, string title, int capacity, string description)
    {
        var part = UnityEngine.Object.Instantiate(template);
        part.name = id;
        part.hideFlags = HideFlags.DontSave;
        part.itemID = id;
        part.itemName = title;
        part.description = description;
        part.connectionCapacity = capacity;
        part.isCore = false;
        part.isDecoy = false;
        part.useUniversalNodeRules = true;
        part.connectionAliasID = "";
        part.doubleBondNodes = new List<int>();
        part.initiallyBlockedNodes = new List<int>();
        part.initiallyUnstableNodes = new List<int>();
        Parts.Add(part);
    }

    internal QuestBlueprintSO AddQuest(string id, string title, int partCount, string summary)
    {
        var quest = ScriptableObject.CreateInstance<QuestBlueprintSO>();
        quest.name = quest.questID = id;
        quest.hideFlags = HideFlags.DontSave;
        quest.title = title;
        quest.clueSummary = summary;
        quest.output = null;
        for (int i = 0; i < partCount; i++)
        {
            quest.nodes.Add(new QuestNodeData { itemID = Parts[i].itemID, isUnknown = false });
            quest.clueParts.Add(Parts[i]);
        }
        // Genel notlar bağımsız arşivlenir; hedef için bulunacak ipucu yuvaları üretilmez.
        quest.clueNotes = new List<string>();
        Quests.Add(quest);
        return quest;
    }

    private void BuildBooks()
    {
        for (int i = 0; i < Books.Length; i++) Books[i] = new List<BookWorkspacePage>();
        Books[0].Add(Page(0, "Araştırma Soruları / A", "Doğrudan karşılaştırma; bitmiş şema verilmemiştir.",
            Target(Quests[0]),
            Note("A / Araştırma sorusu", "Ham uyarım numuneyi yanıltmadan karşılaştırma nasıl kurulur? Deney Kayıtları / D-11 ve D-12'yi Genel İlkeler ile birlikte oku.")));
        Books[0].Add(Page(1, "Araştırma Soruları / B", "Gösterilen şema BAŞARISIZ KAYIT D-20'dir; hedef çözüm değildir.",
            Target(Quests[1]),
            Note("B / Araştırma sorusu", "Aynı parçalar neden yeni yöntemde aynı yerde çalışmıyor? D-20'yi kur, iki varsayımı D-21 / D-22 ile sorgula. A'yı önce çözmek zorunlu değil.")));

        Books[1].Add(Page(0, "Parça Atlası / Ham ve duyarlı kol", "Çizimler tekrar kullanılabilir; deney başına tüketilmez.",
            Part(Parts[0]), Part(Parts[1]), Part(Parts[2])));
        Books[1].Add(Page(1, "Parça Atlası / Karşılaştırma", "Kapasite bir üst sınırdır; komşuluk deney yönteminden çıkarılır.",
            Part(Parts[3]), Part(Parts[4]), Part(Parts[5])));

        Books[2].Add(Page(0, "Genel İlkeler / Yalıtım", "A ve B aynı yalıtım ilkesini farklı düzenlerde kullanır.",
            Note("Ham uyarım", "Uyarıcının tek komşusu ayırıcıdır; aralarında tek hat bulunur. Ham kola, ölçümün dönüşünü bağlamak yalıtımı bozar."),
            Note("Duyarlı hat", "Numunenin ölçüm komşusuna bağlantısı çift çizgidir (=). Diğer bütün hatlar tektir. Ölçüm komşusu kullanılan yöntemin kaydında belirtilir.")));
        Books[2].Add(Page(1, "Genel İlkeler / Deney sınırı", "Sınama, çizilen ilişkileri kayıtların koşullarıyla karşılaştırır.",
            Note("Deney sınırı", "Kayıtta korunmuş ilişkiler dışında ek hat kurma. Bütün bileşenler tek bağlı düzen oluşturur; açık uçlar kalabilir. Kapasiteyi doldurmak amaç değildir."),
            Note("Tek ve çift çizgi", "Tek çizgi 1, çift çizgi 2 kapasite kullanır. Aynı iki uç arasını yeniden çizmek bağı çift yapar; sağ tık bir basamak azaltır. Bağların yönü yoktur.")));

        Books[3].Add(Page(0, "Deney Kayıtları / Doğrudan yöntem", "D-11 ve D-12 tek bir düzenin farklı ilişkilerini inceler.",
            Note("D-11 / Karşılaştırma", "Doğrudan yöntemde numunenin ölçüm komşusu ayırıcıdır. Referans yalnız numuneye bağlanır. Çizgi türleri için Genel İlkeler / Duyarlı hat kaydına bak."),
            Note("D-12 / Artık kolu", "Sönümleyici numuneye eklenince doğrudan karşılaştırma bozuldu. Yalnız ayırıcıya bağlandığında D-11 koşulu korundu; bu yöntemde başka komşusu yoktu.")));
        Books[3].Add(Page(1, "Deney Kayıtları / Başarısız öneri", "D-20 bir sonuç değil, sorgulanacak öneridir.",
            Note("D-20 / Başarısız kayıt", "Bu öneri bağımsız karşılaştırma sağlamadı. Hangi çizgilerin doğru olduğu burada kanıtlanmıyor. D-21'de korunan ilişkilerle D-22'de değişen koşulu karşılaştır."),
            Note("D-21 / Korunan ilişkiler", "Bağımsız yöntemde ayırıcı algılayıcıya; algılayıcı referansa; numune sönümleyiciye tek hatla bağlı kalır. Dönüş ucunu yalıtım ilkesinden çıkar.")));
        Books[3].Add(Page(2, "Deney Kayıtları / Ayırt edici kanıt", "Yeni yöntem eski yerleşimi değil, eski ilkeyi korur.",
            Note("D-21 / Dönüş karşılaştırması", "Sönümleyicinin ikinci ucu iki adayla sınandı: ayırıcı veya referans. Bağımsız dönüş, ham kola değmeden numune ile referansı birleştirmelidir."),
            Note("D-22 / Duyarlılık", "Bu yöntemde numunenin ölçüm komşusu algılayıcıdır. D-20'deki tek hat yeterli değildir; Genel İlkeler / Duyarlı hat koşulu burada da geçerlidir.")));
    }

    private static NotebookBlockData Target(QuestBlueprintSO quest) => new NotebookBlockData
    {
        blockType = NotebookBlockType.Quest, questID = quest.questID, title = quest.title,
        description = quest.questID == FirstQuestID ? "Bağları D-11 / D-12'den çıkar. Bitmiş şema yok."
            : "BAŞARISIZ KAYIT D-20. İki hatayı D-21 / D-22 ile düzelt."
    };

    private static NotebookBlockData Part(FormulaItemSO part) => new NotebookBlockData
    {
        blockType = NotebookBlockType.PartClue, itemID = part.itemID,
        title = part.itemName.Substring(0, Mathf.Min(3, part.itemName.Length)).ToUpperInvariant() + " / " + part.itemName, description = part.description
    };

    private static NotebookBlockData Note(string title, string description) => new NotebookBlockData
    {
        blockType = NotebookBlockType.RuleClue, questID = "", ruleIndex = -1,
        title = title, description = description
    };

    private static BookWorkspacePage Page(int index, string title, string explanation, params NotebookBlockData[] blocks) =>
        new BookWorkspacePage { pageIndex = index, title = title, explanation = explanation, blocks = new List<NotebookBlockData>(blocks) };

    private static SynthesisBondData Bond(int from, int to, BondType type = BondType.Single) =>
        new SynthesisBondData { fromIndex = from, toIndex = to, bondType = type };

    private static List<SynthesisBondData> CopyBonds(List<SynthesisBondData> bonds) =>
        bonds.ConvertAll(bond => Bond(bond.fromIndex, bond.toIndex, bond.bondType));

    private static int Neighbors(int[,] links, int index)
    {
        int count = 0;
        for (int i = 0; i < links.GetLength(0); i++) if (links[index, i] != 0) count++;
        return count;
    }

    private static bool Fail(string text, out string message) { message = text; return false; }

    private static void DestroyRuntime(UnityEngine.Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(item);
        else UnityEngine.Object.DestroyImmediate(item);
    }
}

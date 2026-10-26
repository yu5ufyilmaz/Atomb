using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Yalnız okur: rehber açmak görev üretmez, parça vermez veya kilit açmaz.
public static class NotebookSynthesisGuide
{
    public sealed class Entry
    {
        public string title;
        public string text;
        public SynthesisQuestData solution;
    }

    public static List<Entry> Build(ChalkboardManager board, ProceduralSynthesisGenerator generator, NotebookData notebook)
    {
        var entries = new List<Entry>();
        var questIDs = new HashSet<string>();
        if (board != null && board.practiceQuest != null)
        {
            var quest = board.practiceQuest.CreateQuest();
            entries.Add(QuestEntry(quest, generator, board, notebook, "AKTİF ALIŞTIRMA", true));
            questIDs.Add(quest.questID);
        }
        if (board != null && board.practicePuzzles != null)
            foreach (var practice in board.practicePuzzles)
                if (practice != null && questIDs.Add(practice.CreateQuest().questID))
                    entries.Add(QuestEntry(practice.CreateQuest(), generator, board, notebook, "DİĞER ALIŞTIRMA / HEDEFİNİ TAHTAYA SABİTLE", true));
        if (generator != null && generator.Run != null && generator.Run.quests != null)
            foreach (var quest in generator.Run.quests)
                if (quest != null && questIDs.Add(quest.questID))
                    entries.Add(QuestEntry(quest, generator, board, notebook,
                        board != null && board.practiceQuest == null && board.useProceduralQuests
                            ? "BU OYUNUN SENTEZ ZİNCİRİ" : "KOŞU ARŞİVİ / BU TAHTADA KAPALI", false));
        var recipes = new List<RecipeSO>();
        if (board != null && board.allRecipes != null) recipes.AddRange(board.allRecipes);
        if (notebook != null && notebook.synthesisRecipes != null) recipes.AddRange(notebook.synthesisRecipes);
        var seen = new HashSet<RecipeSO>();
        foreach (var recipe in recipes)
        {
            if (recipe == null || recipe.recipeType != RecipeType.Synthesis || !seen.Add(recipe)) continue;
            bool available = board != null && board.practiceQuest == null
                && !(board.useProceduralQuests && generator != null && generator.Run != null)
                && board.allRecipes != null && board.allRecipes.Contains(recipe);
            var body = new StringBuilder(available ? "BU TAHTANIN TARİFİ\n\n" : "TARİF ARŞİVİ / BU TAHTADA KAPALI\n\n");
            body.Append("Girdiler: ").Append(Ingredients(recipe.inputs)).Append("\n\nÇıktı: ").Append(Ingredients(recipe.outputs)).Append("\n\n");
            Entry entry;
            if (recipe.blueprint != null)
            {
                entry = QuestEntry(recipe.blueprint.CreateQuest(), generator, board, notebook, "BAĞ ŞEMASI", false);
                body.Append(entry.text);
            }
            else
            {
                entry = new Entry();
                body.Append("Özel bir bağ şeması tanımlı değil. Girdilerin tamamını uyumlu uçlardan tek bir bağlı yapıda birleştir; parça kapasitelerini aşma.\n\n");
            }
            AppendRules(body, recipe.rules);
            body.Append("\nYapının tamamını sentez alanına koy ve SENTEZLE / TEST ET düğmesine bas.");
            entry.title = (string.IsNullOrEmpty(recipe.recipeName) ? recipe.name : recipe.recipeName) + " → " + Ingredients(recipe.outputs);
            entry.text = body.ToString();
            entries.Add(entry);
        }
        return entries;
    }

    private static Entry QuestEntry(SynthesisQuestData original, ProceduralSynthesisGenerator generator, ChalkboardManager board,
        NotebookData notebook, string mode, bool practice)
    {
        var quest = JsonUtility.FromJson<SynthesisQuestData>(JsonUtility.ToJson(original));
        var body = new StringBuilder(mode + "\n\n");
        var names = new List<string>();
        foreach (var node in quest.nodes)
        {
            node.isUnknown = false;
            var item = Resolve(node.itemID, generator, board, notebook);
            names.Add(item != null ? Name(item) : node.itemID);
        }
        body.Append("Parçalar: ").Append(string.Join(" + ", names)).Append("\n\nBağlar:\n");
        foreach (var bond in quest.bonds)
        {
            if (bond.fromIndex < 0 || bond.toIndex < 0 || bond.fromIndex >= names.Count || bond.toIndex >= names.Count) continue;
            body.Append(names[bond.fromIndex]).Append(bond.bondType == BondType.Double ? " ═ " : " — ")
                .Append(names[bond.toIndex]).Append(bond.bondType == BondType.Double ? " (çift bağ)" : " (tek bağ)").Append('\n');
        }
        body.Append("\nHedef notunu tahtaya sürükle. Parçaları da defterden tahtaya kopyala.\n");
        body.Append("Yuvalar doğrudan kullanılabilir; uç açma/silme adımı yok.\n");
        body.Append("ÇİFT BAĞ (=) işaretli yuvada 0/2 → 1/2 → 2/2 ilerlemesini takip et. SOL TUŞU basılı tutarak uçları birleştir. Aynı iki uç arasında ikinci çizim çift bağ yapar; mevcut bağ yerinde kalır. Yanlış bağı SAĞ TIK ile düzelt.\n\n");
        AppendRules(body, quest.rules);
        if (practice) body.Append("\nSENTEZLE / TEST ET → Doğru bildin. Bu alıştırma parça tüketmez ve yeni ürün vermez.");
        else
        {
            var output = Resolve(quest.outputItemID, generator, board, notebook);
            body.Append("\nSENTEZLE / TEST ET → ").Append(output != null ? Name(output) : quest.outputItemID);
        }
        return new Entry { title = quest.title, text = body.ToString(), solution = quest };
    }

    private static void AppendRules(StringBuilder body, List<SynthesisRuleData> rules)
    {
        if (rules == null || rules.Count == 0) return;
        body.Append("Kurallar:\n");
        foreach (var rule in rules)
            if (rule != null && rule.ruleType != SynthesisRuleType.RequireNoBlockedEnds)
                body.Append("• ").Append(rule.GetDescription()).Append('\n');
    }

    private static FormulaItemSO Resolve(string id, ProceduralSynthesisGenerator generator, ChalkboardManager board, NotebookData notebook)
    {
        var item = generator != null ? generator.ResolveFormula(id) : null;
        if (item != null) return item;
        if (board != null && board.practiceQuest != null)
        {
            item = board.practiceQuest.clueParts.Find(p => p != null && p.itemID == id);
            if (item != null) return item;
        }
        if (board != null && board.formulaCatalog != null) item = board.formulaCatalog.Find(p => p != null && p.itemID == id);
        if (item == null && notebook != null && notebook.synthesisCatalog != null) item = notebook.synthesisCatalog.Find(p => p != null && p.itemID == id);
        return item;
    }

    private static string Name(FormulaItemSO item) => string.IsNullOrEmpty(item.itemName) ? item.itemID : item.itemName;

    private static string Ingredients(List<FormulaItemSO> items)
    {
        if (items == null || items.Count == 0) return "Tanımlanmamış";
        var counts = new Dictionary<string, int>();
        var labels = new Dictionary<string, string>();
        foreach (var item in items)
        {
            if (item == null) continue;
            counts.TryGetValue(item.itemID, out int count);
            counts[item.itemID] = count + 1;
            labels[item.itemID] = Name(item);
        }
        var result = new List<string>();
        foreach (var pair in counts) result.Add(pair.Value + "× " + labels[pair.Key]);
        return string.Join(" + ", result);
    }
}

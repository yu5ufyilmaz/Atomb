using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class PuzzleBookDistribution
{
    public static List<BookWorkspacePage>[] Build(List<PuzzleDefinition> puzzles, int bookCount, int seed)
        => Build(puzzles, DefaultBooks(bookCount), seed);

    public static List<BookWorkspacePage>[] Build(PuzzleDefinition puzzle, int bookCount, int seed)
        => Build(new List<PuzzleDefinition> { puzzle }, DefaultBooks(bookCount), seed);

    private static List<PuzzleBookProfile> DefaultBooks(int count)
    {
        var books = new List<PuzzleBookProfile>();
        for (int i = 0; i < count; i++) books.Add(PuzzleBookProfile.Default(i, "book/" + i, count > 4));
        return books;
    }

    public static List<NotebookBlockData> Notes(PuzzleDefinition puzzle)
    {
        var result = new List<NotebookBlockData> {
            new NotebookBlockData { instanceID = "target/" + puzzle.id, blockType = NotebookBlockType.Quest,
                questID = puzzle.id, title = puzzle.title, description = puzzle.description,
                itemID = puzzle.TargetVisual },
            Note("target", "İSTENEN SONUÇ", puzzle.kind == PuzzleKind.ColorMixture ? "Rengi, pigment yoğunluğunu ve toplam miktarı hedefle karşılaştır." : "Zamanı, genişliği ve yüksekliği hedefle karşılaştır.", puzzle.TargetVisual),
            Note("constraints", "DENEYİN KOŞULLARI", DescribeRules(puzzle.rules, puzzle.kind)),
            Note("ports", "HATLARIN YÖNÜ", "Halka giriş, ok çıkıştır. Bir uçtan tek hat çıkar. Tek kaynak ve tek ölçer kullan. Bütün parçalar kaynaktan beslenip ölçere ulaşmalı."),
            Note("coincident", puzzle.kind == PuzzleKind.ColorMixture ? "RENK VE YOĞUNLUK" : "EŞZAMANLI VARIŞ", puzzle.kind == PuzzleKind.ColorMixture ? "Renk pigment oranlarından oluşur. Yoğunluk, pigment / toplam miktardır. Aynı görünen rengin miktarı veya yoğunluğu farklı olabilir." : "Aynı anda, aynı genişlikte gelen izlerin yükseklikleri toplanır. Kayıtta tek görünür iz oluşur."),
            Note("beforeafter", "İŞLEMLERİN SIRASI", puzzle.kind == PuzzleKind.ColorMixture ? "Dağıtıcı miktarları yarıya böler; karıştırıcı iki kolu toplar. Pigment ekleme, seyreltme ve yoğunlaştırmanın sırası sonucu değiştirebilir." : "Her numunenin ve işlem parçasının etkisi, içinden geçen her ize uygulanır. Sıra ve dallanma sonucu değiştirebilir."),
            Note("transfer", "KAYDI TAŞIMAK", "Tahtada DENEYİ ÖLÇ. Ölçüm kağıdını deftere al; Turing'in sol klavyesindeki okuyucuya bırak. Kod hazır olunca Enter ile sına.")
        };
        foreach (var part in PuzzleParts.ForKind(puzzle.kind))
        {
            var rule = puzzle.rules.Find(part.id);
            if (puzzle.rules.Allows(part.id))
                result.Add(new NotebookBlockData { instanceID = "part/" + part.id, blockType = NotebookBlockType.PartClue,
                    itemID = part.id, title = part.title, description = (string.IsNullOrWhiteSpace(rule.explanation) ? part.description : rule.explanation)
                        + (part.inputs == 0 || part.outputs == 0 ? " Bu deneyde 1 adet." : $" Bu deneyde {rule.minimum}–{rule.maximum} adet.") });
            else result.Add(Note("ban/" + part.id, "KULLANIM YASAĞI", "Bu deneyde " + part.title + " kullanma."));
        }
        if (puzzle.notes != null) for (int i = 0; i < puzzle.notes.Count; i++)
        {
            var page = puzzle.notes[i]; if (page == null) continue;
            if (!string.IsNullOrWhiteSpace(page.explanation)) result.Add(Note("custom/" + i, page.title, page.explanation));
        }
        return result;
    }

    public static string DescribeRules(PuzzleRules rules, PuzzleKind kind = PuzzleKind.Signal)
    {
        string result = $"Toplam parça: {rules.minParts}–{rules.maxParts}. Numune: {rules.minSamples}–{rules.maxSamples}. "
            + $"En fazla hat derinliği: {rules.maxDepth}. " + (kind == PuzzleKind.Signal ? $"Hedef: {rules.traceCount} görünür iz. " : "Hedef: renk, yoğunluk ve miktar. ") + "Tek kaynak, tek ölçer; tüm uçlar işler durumda olmalı.";
        return result;
    }

    private static NotebookBlockData Note(string id, string title, string text, string visual = null) => new NotebookBlockData {
        instanceID = "rule/" + id, blockType = NotebookBlockType.RuleClue, itemID = visual ?? "puzzle-rule/" + id,
        title = title, description = text };
}

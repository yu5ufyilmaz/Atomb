using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Menüden veya -executeMethod SynthesisPuzzleValidation.RunAll ile çalıştırılır.
public static class SynthesisPuzzleValidation
{
    [MenuItem("Senzora/Doğrulama/Prosedürel Sentez")]
    public static void RunAll()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Sentez doğrulamasını Play Mode kapalıyken çalıştırın.");
        ValidateGraphRules();
        ValidateGeneratedRuns();
        Debug.Log("Sentez doğrulaması geçti: grafik kuralları, 64 tohum, 320 aşama ve JSON geri yükleme.");
    }

    private static void ValidateGraphRules()
    {
        // Aynı üç parçanın farklı topolojileri eşdeğer sayılmamalı.
        var quest = new SynthesisQuestData
        {
            nodes = new List<QuestNodeData>
            {
                new QuestNodeData { itemID = "A" },
                new QuestNodeData { itemID = "M", isUnknown = true },
                new QuestNodeData { itemID = "B" }
            },
            bonds = new List<SynthesisBondData> { Bond(0, 1), Bond(1, 2) },
            rules = new List<SynthesisRuleData>
            {
                new SynthesisRuleData { ruleType = SynthesisRuleType.RequireBridge, itemID = "A", otherItemID = "B", bridgeItemID = "M" },
                new SynthesisRuleData { ruleType = SynthesisRuleType.ExactPartCount, itemID = "M", amount = 1 }
            }
        };
        var graph = new SynthesisGraphData
        {
            parts = new List<SynthesisPartData>
            {
                new SynthesisPartData { itemID = "A", capacity = 2 },
                new SynthesisPartData { itemID = "M", capacity = 3 },
                new SynthesisPartData { itemID = "B", capacity = 2 }
            },
            bonds = new List<SynthesisBondData> { Bond(0, 1), Bond(1, 2) }
        };
        Check(SynthesisGraphValidator.Validate(graph, quest, out _), "Geçerli köprü reddedildi.");
        graph.bonds[1] = Bond(0, 2);
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Yanlış topoloji kabul edildi.");
        graph.bonds[1] = Bond(1, 2);
        graph.parts[1].itemID = "YANLIŞ";
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Bilinmeyen yuva ipuçları göz ardı edildi.");
        graph.parts[1].itemID = "M";
        graph.bonds[0].bondType = BondType.Double;
        quest.bonds[0].bondType = BondType.Double;
        quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.MinDoubleBonds, amount = 1 });
        // Çift bağ, merkezde 2 + 1 = 3 kapasite kullanır.
        Check(SynthesisGraphValidator.Validate(graph, quest, out _), "Yeterli çift bağ kapasitesi reddedildi.");
        graph.parts[1].capacity = 2;
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Çift bağ kapasite taşması kabul edildi.");
        graph.parts[1].capacity = 3;
        graph.bonds[0].bondType = BondType.Single;
        quest.bonds[0].bondType = BondType.Single;
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Asgari çift bağ kuralı göz ardı edildi.");
        quest.rules.RemoveAt(quest.rules.Count - 1);
        graph.bonds.Add(Bond(1, 0));
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Yinelenen/asimetrik bağ kabul edildi.");
        graph.bonds.RemoveAt(2);
        graph.bonds.RemoveAt(1);
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Bağlantısız grafik kabul edildi.");
        graph.bonds.Add(Bond(1, 2));
        quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.RequireNoBlockedEnds });
        graph.parts[2].blockedEnds = 1;
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Silinmemiş kararsız uç kabul edildi.");
        graph.parts[2].blockedEnds = 0;
        var negativeRules = new List<SynthesisRuleData>
        {
            new SynthesisRuleData { ruleType = SynthesisRuleType.MaxSingleBonds, amount = 1 },
            new SynthesisRuleData { ruleType = SynthesisRuleType.ForbidDirectBond, itemID = "A", otherItemID = "M" },
            new SynthesisRuleData { ruleType = SynthesisRuleType.MinPartCount, itemID = "M", amount = 2 },
            new SynthesisRuleData { ruleType = SynthesisRuleType.MaxPartCount, itemID = "M", amount = 0 },
            new SynthesisRuleData { ruleType = SynthesisRuleType.ForbiddenPart, itemID = "M" }
        };
        foreach (var rule in negativeRules)
        {
            quest.rules.Add(rule);
            Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Kural göz ardı edildi: " + rule.ruleType);
            quest.rules.RemoveAt(quest.rules.Count - 1);
        }
        graph.parts[2].openEnds = 1;
        quest.rules.Add(new SynthesisRuleData { ruleType = SynthesisRuleType.MaxOpenEnds, amount = 0 });
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Açık uç sınırı göz ardı edildi.");
        graph.parts[2].openEnds = 0;
        graph.bonds[0].fromIndex = 9;
        Check(!SynthesisGraphValidator.Validate(graph, quest, out _), "Geçersiz bağ indeksi kabul edildi.");
        graph.bonds[0].fromIndex = 0;

        // Bilinmeyen yuva gizli çözüm kimliğiyle değil oyuncuya verilen kurallarla çözülür.
        graph.parts[1].itemID = "N";
        quest.rules[0].bridgeItemID = "N";
        quest.rules[1].itemID = "N";
        Check(SynthesisGraphValidator.Validate(graph, quest, out _), "Bilinmeyen yuva ipucu yerine gizli kimliğe kilitlendi.");
    }

    private static void ValidateGeneratedRuns()
    {
        var host = new GameObject("Synthesis validation (temporary)");
        try
        {
            var generator = host.AddComponent<ProceduralSynthesisGenerator>();
            foreach (string guid in AssetDatabase.FindAssets("t:FormulaItemSO"))
            {
                var item = AssetDatabase.LoadAssetAtPath<FormulaItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                generator.RegisterFormula(item);
                if (item == null || item.chalkPrefab == null) continue;
                var root = item.chalkPrefab.GetComponent<DraggableFormula>();
                if (root == null) continue;
                int expected = 0;
                foreach (var node in item.chalkPrefab.GetComponentsInChildren<FormulaNode>(true))
                    if (node.GetComponentInParent<DraggableFormula>(true) == root) expected++;
                var inspect = typeof(ProceduralSynthesisGenerator).GetMethod("OwnNodes",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var actual = (FormulaNode[])inspect.Invoke(null, new object[] { item });
                Check(actual.Length == expected, "Prefab assetinin pasif hiyerarşisindeki uçlar atlandı: " + item.itemID);
            }

            var signatures = new HashSet<string>();
            for (int seed = 0; seed < 64; seed++)
            {
                generator.GenerateRun(seed);
                Check(generator.Run != null && generator.Run.quests.Count == 5, "Beş aşama üretilemedi: " + seed);
                string json = JsonUtility.ToJson(generator.Run);
                // Kimlik/tohum yazısını değil gerçek parça/topoloji çeşitliliğini say.
                string signature = "";
                foreach (var node in generator.Run.quests[0].nodes) signature += node.itemID + ";";
                foreach (var bond in generator.Run.quests[0].bonds) signature += bond.fromIndex + "-" + bond.toIndex + ":" + (int)bond.bondType + ";";
                signatures.Add(signature);
                string previous = null;
                foreach (var quest in generator.Run.quests)
                {
                    var graph = new SynthesisGraphData();
                    foreach (var node in quest.nodes)
                    {
                        FormulaItemSO item = generator.ResolveFormula(node.itemID);
                        Check(item != null && item.chalkPrefab != null, "Eksik çizim prefabı: " + node.itemID);
                        Check(!node.itemID.StartsWith("sx_run_", StringComparison.Ordinal) || node.itemID == previous,
                            "Henüz üretilmemiş ara ürün başlangıç ipucuna karıştı.");
                        graph.parts.Add(new SynthesisPartData { itemID = item.itemID, capacity = item.connectionCapacity });
                    }
                    foreach (var bond in quest.bonds)
                        graph.bonds.Add(Bond(bond.fromIndex, bond.toIndex, bond.bondType));
                    Check(SynthesisGraphValidator.Validate(graph, quest, out string reason), "Çözümsüz aşama " + quest.questID + ": " + reason);
                    Check(generator.CanRealizeQuest(quest), "Prefab uçlarıyla kurulamayan aşama: " + quest.questID);
                    if (previous != null)
                        Check(quest.nodes.Exists(node => node.itemID == previous), "Önceki çıktı sonraki girdiye aktarılmadı.");
                    previous = quest.outputItemID;
                }
                generator.GenerateRun(seed);
                Check(JsonUtility.ToJson(generator.Run) == json, "Aynı tohum farklı görev üretti.");
                var save = new GameData();
                generator.CompleteActiveQuest();
                generator.SaveData(ref save);
                var restored = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(save));
                generator.GenerateRun(seed + 1000);
                generator.LoadData(restored);
                Check(generator.Run.seed == seed && generator.Run.currentStage == 1, "Kayıt görev ilerlemesini geri yüklemedi.");
                Check(generator.ResolveFormula(previous) != null, "Kayıttan sonra ara ürün kataloğu kurulmadı.");
                var restoredOutput = generator.ResolveFormula(previous);
                generator.LoadData(restored);
                Check(ReferenceEquals(restoredOutput, generator.ResolveFormula(previous)), "İkinci kayıt yüklemesi tahta SO referansını geçersiz kıldı.");
            }
            Check(signatures.Count >= 20, "Yirmiden az farklı prosedürel koşu üretildi.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static SynthesisBondData Bond(int a, int b, BondType type = BondType.Single)
    {
        return new SynthesisBondData { fromIndex = a, toIndex = b, bondType = type };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

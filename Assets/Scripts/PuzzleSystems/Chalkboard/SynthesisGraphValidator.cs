using System;
using System.Collections.Generic;

public static class SynthesisGraphValidator
{
    public static bool Validate(SynthesisGraphData graph, SynthesisQuestData quest, out string reason)
    {
        if (!ValidateGraph(graph, out int[,] adjacency, out reason))
            return false;
        if (quest == null || quest.nodes == null || quest.bonds == null || quest.nodes.Count != graph.parts.Count)
            return Fail("Hedef şemadaki parça sayısı uyuşmuyor.", out reason);
        if (!TryAdjacency(quest.nodes.Count, quest.bonds, out int[,] target, out reason))
            return false;

        int[] mapping = new int[quest.nodes.Count];
        for (int i = 0; i < mapping.Length; i++) mapping[i] = -1;
        // ponytail: küçük bulmaca grafikleri için geri izleme; büyük şemalarda kanonik grafik eşleştirme kullanın.
        if (!MatchTopology(0, quest.nodes, graph.parts, target, adjacency, mapping, new bool[mapping.Length]))
            return Fail("Bağ türleri veya şemanın komşuluk düzeni hedefle uyuşmuyor.", out reason);
        return ValidateRules(graph, adjacency, quest.rules, out reason);
    }

    public static bool ValidateRecipe(SynthesisGraphData graph, RecipeSO recipe, out string reason)
    {
        if (recipe == null || recipe.recipeType != RecipeType.Synthesis || recipe.inputs == null)
            return Fail("Geçerli bir sentez tarifi yok.", out reason);
        if (!ValidateGraph(graph, out int[,] adjacency, out reason))
            return false;
        var required = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in recipe.inputs)
        {
            if (item == null || string.IsNullOrEmpty(item.itemID))
                return Fail("Tarifte eksik parça tanımı var.", out reason);
            required.TryGetValue(item.itemID, out int count);
            required[item.itemID] = count + 1;
        }
        if (recipe.inputs.Count != graph.parts.Count)
            return Fail("Tarifteki parça sayısı uyuşmuyor.", out reason);
        foreach (var part in graph.parts)
        {
            if (!required.TryGetValue(part.itemID, out int count) || count == 0)
                return Fail("Tarifteki parça bileşimi uyuşmuyor.", out reason);
            required[part.itemID] = count - 1;
        }
        if (recipe.blueprint != null && !Validate(graph, recipe.blueprint.CreateQuest(), out reason))
            return false;
        return ValidateRules(graph, adjacency, recipe.rules, out reason);
    }

    private static bool ValidateGraph(SynthesisGraphData graph, out int[,] adjacency, out string reason)
    {
        adjacency = null;
        if (graph == null || graph.parts == null || graph.parts.Count == 0 || graph.bonds == null)
            return Fail("Sentez grafiği boş.", out reason);
        if (!TryAdjacency(graph.parts.Count, graph.bonds, out adjacency, out reason))
            return false;
        for (int i = 0; i < graph.parts.Count; i++)
        {
            var part = graph.parts[i];
            if (part == null || string.IsNullOrEmpty(part.itemID) || part.capacity < 0 || part.openEnds < 0 || part.blockedEnds < 0)
                return Fail("Geçersiz parça veya uç verisi.", out reason);
            int usedCapacity = 0;
            for (int j = 0; j < graph.parts.Count; j++) usedCapacity += adjacency[i, j];
            if (usedCapacity > part.capacity)
                return Fail("[" + part.itemID + "] bağlantı kapasitesini aşıyor.", out reason);
        }
        var visited = new bool[graph.parts.Count];
        var pending = new Queue<int>();
        pending.Enqueue(0);
        visited[0] = true;
        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            for (int i = 0; i < visited.Length; i++)
                if (adjacency[current, i] > 0 && !visited[i])
                {
                    visited[i] = true;
                    pending.Enqueue(i);
                }
        }
        foreach (bool reachable in visited)
            if (!reachable)
                return Fail("Bütün parçalar aynı bağlı yapıda olmalı.", out reason);
        reason = "";
        return true;
    }

    private static bool TryAdjacency(int count, List<SynthesisBondData> bonds, out int[,] adjacency, out string reason)
    {
        adjacency = new int[count, count];
        foreach (var bond in bonds)
        {
            if (bond == null || bond.fromIndex < 0 || bond.fromIndex >= count || bond.toIndex < 0 || bond.toIndex >= count || bond.fromIndex == bond.toIndex)
                return Fail("Bağın uç indeksleri geçersiz.", out reason);
            int order = (int)bond.bondType;
            if (order != 1 && order != 2)
                return Fail("Bilinmeyen bağ türü.", out reason);
            if (adjacency[bond.fromIndex, bond.toIndex] != 0)
                return Fail("Yinelenen veya asimetrik bağ kaydı.", out reason);
            adjacency[bond.fromIndex, bond.toIndex] = order;
            adjacency[bond.toIndex, bond.fromIndex] = order;
        }
        reason = "";
        return true;
    }

    private static bool MatchTopology(int index, List<QuestNodeData> targetParts, List<SynthesisPartData> parts,
        int[,] target, int[,] actual, int[] mapping, bool[] used)
    {
        if (index == mapping.Length) return true;
        QuestNodeData expected = targetParts[index];
        if (expected == null) return false;
        for (int candidate = 0; candidate < parts.Count; candidate++)
        {
            if (used[candidate] || (!expected.isUnknown && expected.itemID != parts[candidate].itemID)) continue;
            int expectedDegree = 0, actualDegree = 0;
            for (int i = 0; i < mapping.Length; i++)
            {
                expectedDegree += target[index, i];
                actualDegree += actual[candidate, i];
            }
            if (expectedDegree != actualDegree) continue;
            bool compatible = true;
            for (int i = 0; i < index; i++)
                if (target[index, i] != actual[candidate, mapping[i]]) { compatible = false; break; }
            if (!compatible) continue;
            mapping[index] = candidate;
            used[candidate] = true;
            if (MatchTopology(index + 1, targetParts, parts, target, actual, mapping, used)) return true;
            used[candidate] = false;
            mapping[index] = -1;
        }
        return false;
    }

    private static bool ValidateRules(SynthesisGraphData graph, int[,] adjacency, List<SynthesisRuleData> rules, out string reason)
    {
        if (rules == null) { reason = ""; return true; }
        foreach (var rule in rules)
        {
            if (rule == null || rule.amount < 0)
                return Fail("Geçersiz sentez kuralı.", out reason);
            int count = 0;
            bool valid;
            switch (rule.ruleType)
            {
                case SynthesisRuleType.MinDoubleBonds:
                    foreach (var bond in graph.bonds) if (bond.bondType == BondType.Double) count++;
                    valid = count >= rule.amount;
                    break;
                case SynthesisRuleType.MaxSingleBonds:
                    foreach (var bond in graph.bonds) if (bond.bondType == BondType.Single) count++;
                    valid = count <= rule.amount;
                    break;
                case SynthesisRuleType.MinPartCount:
                case SynthesisRuleType.MaxPartCount:
                case SynthesisRuleType.ExactPartCount:
                case SynthesisRuleType.ForbiddenPart:
                    if (string.IsNullOrEmpty(rule.itemID)) return Fail("Kuralın parça kimliği eksik.", out reason);
                    foreach (var part in graph.parts) if (part.itemID == rule.itemID) count++;
                    valid = rule.ruleType == SynthesisRuleType.MinPartCount ? count >= rule.amount
                        : rule.ruleType == SynthesisRuleType.MaxPartCount ? count <= rule.amount
                        : rule.ruleType == SynthesisRuleType.ExactPartCount ? count == rule.amount : count == 0;
                    break;
                case SynthesisRuleType.ForbidDirectBond:
                case SynthesisRuleType.RequireBridge:
                    valid = CheckBridge(graph, adjacency, rule);
                    break;
                case SynthesisRuleType.RequireNoBlockedEnds:
                    foreach (var part in graph.parts) count += part.blockedEnds;
                    valid = count == 0;
                    break;
                case SynthesisRuleType.MaxOpenEnds:
                    foreach (var part in graph.parts) count += part.openEnds;
                    valid = count <= rule.amount;
                    break;
                default: return Fail("Bilinmeyen sentez kuralı.", out reason);
            }
            if (!valid) return Fail(rule.GetDescription(), out reason);
        }
        reason = "";
        return true;
    }

    private static bool CheckBridge(SynthesisGraphData graph, int[,] adjacency, SynthesisRuleData rule)
    {
        if (string.IsNullOrEmpty(rule.itemID) || string.IsNullOrEmpty(rule.otherItemID)) return false;
        bool foundPair = false;
        for (int a = 0; a < graph.parts.Count; a++)
        {
            if (graph.parts[a].itemID != rule.itemID) continue;
            for (int b = 0; b < graph.parts.Count; b++)
            {
                if (a == b || graph.parts[b].itemID != rule.otherItemID) continue;
                foundPair = true;
                if (adjacency[a, b] > 0) return false;
                if (rule.ruleType == SynthesisRuleType.ForbidDirectBond) continue;
                bool bridge = false;
                for (int mid = 0; mid < graph.parts.Count; mid++)
                    if (mid != a && mid != b && graph.parts[mid].itemID == rule.bridgeItemID && adjacency[a, mid] > 0 && adjacency[mid, b] > 0)
                    { bridge = true; break; }
                if (!bridge) return false;
            }
        }
        return rule.ruleType == SynthesisRuleType.ForbidDirectBond || foundPair;
    }

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }
}

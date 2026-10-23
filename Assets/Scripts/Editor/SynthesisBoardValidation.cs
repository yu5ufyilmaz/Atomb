using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

// Menüden veya -executeMethod SynthesisBoardValidation.RunAll ile çalıştırılır.
public static class SynthesisBoardValidation
{
    [MenuItem("Senzora/Doğrulama/Tahta Bağları ve Kayıt")]
    public static void RunAll()
    {
        var host = new GameObject("Synthesis board validation (temporary)");
        var definitions = new List<FormulaItemSO>();
        ChalkboardManager previous = ChalkboardManager.Instance;
        try
        {
            var manager = host.AddComponent<ChalkboardManager>();
            SetManager(manager);
            var board = new GameObject("Board");
            board.transform.SetParent(host.transform);
            board.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 25f, 5f));
            manager.boardTransform = board.transform;
            manager.useProceduralQuests = false;

            DraggableFormula a = CreatePart(host.transform, board.transform, "A", new Vector3(-0.4f, 0f, 0.02f), definitions);
            DraggableFormula b = CreatePart(host.transform, board.transform, "B", new Vector3(0.4f, 0f, 0.02f), definitions);
            DraggableFormula c = CreatePart(host.transform, board.transform, "C", new Vector3(0f, 0.4f, 0.02f), definitions);
            manager.formulaCatalog.AddRange(definitions);
            var zoneObject = new GameObject("ReactionZone", typeof(BoxCollider), typeof(ReactionZone));
            zoneObject.transform.SetParent(board.transform, false);
            zoneObject.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            zoneObject.GetComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.1f);
            manager.synthesisZone = zoneObject.GetComponent<ReactionZone>();
            Physics.SyncTransforms();
            Check(manager.synthesisZone.ContainsFormula(a), "Tahtanın arkasındaki eski çember collider'ı düzlemsel parçayı kabul etmedi.");
            FormulaNode[] an = a.GetOwnNodes(), bn = b.GetOwnNodes(), cn = c.GetOwnNodes();
            Vector3 aPosition = a.transform.position, bPosition = b.transform.position, cPosition = c.transform.position;
            an[0].ConnectTo(bn[0]);
            bn[1].ConnectTo(cn[0]);
            cn[1].ConnectTo(an[1]);
            Check(a.GetConnectedParts().Count == 3, "Üçlü döngü kurulamadı.");
            Check(a.GetRootFormula() == b.GetRootFormula() && b.GetRootFormula() == c.GetRootFormula(), "Bağlı grafiğin taşıma grubu farklı.");
            Check(Vector3.Distance(a.transform.position, aPosition) < 0.0001f
                && Vector3.Distance(b.transform.position, bPosition) < 0.0001f
                && Vector3.Distance(c.transform.position, cPosition) < 0.0001f, "Gruplama parçaları ışınladı.");

            an[0].ConnectTo(bn[0]);
            Check(an[0].bondType == BondType.Double && bn[0].bondType == BondType.Double && a.UsedCapacity == 3, "Çift bağ yükseltme/kapasite hatalı.");
            SynthesisGraphData graph = manager.BuildGraph(a);
            Check(graph.parts.Count == 3 && graph.bonds.Count == 3 && graph.bonds.Exists(edge => edge.bondType == BondType.Double), "Grafik döngü/çift bağı kaybetti.");
            an[0].Erase();
            Check(an[0].isOccupied && an[0].bondType == BondType.Single && a.UsedCapacity == 2, "Çift bağ tek bağa inmedi.");
            a.formulaData.connectionCapacity = 2;
            an[0].ConnectTo(bn[0]);
            Check(an[0].bondType == BondType.Single, "Çift bağ kapasite sınırını aştı.");
            a.formulaData.connectionCapacity = 4;
            an[0].Erase();
            Check(!an[0].isOccupied && a.GetConnectedParts().Count == 3 && a.GetRootFormula() == b.GetRootFormula(), "Döngüde tek bağ silinince grup gereksiz bölündü.");
            bn[1].Disconnect();
            Check(a.GetConnectedParts().Count == 2 && b.GetConnectedParts().Count == 1 && a.GetRootFormula() != b.GetRootFormula(), "Gerçek ayrılma Transform grubuna yansımadı.");
            cn[1].Disconnect();
            Check(a.transform.parent == board.transform && b.transform.parent == board.transform && c.transform.parent == board.transform,
                "Bağımsız parçalarda eski üst formül sahipliği kaldı.");

            an[2].nodeState = FormulaNodeState.Blocked;
            an[2].ConnectTo(bn[2]);
            Check(!an[2].isOccupied, "Kapalı uç bağlantı kabul etti.");
            Invoke(an[2], "UpdateBlockedVisual");
            TMP_Text blocked = an[2].GetComponentInChildren<TMP_Text>();
            Check(blocked != null && blocked.gameObject.activeSelf && blocked.text == "x", "Kapalı uç işareti oluşturulamadı.");
            an[2].Erase();
            Invoke(an[2], "UpdateBlockedVisual");
            Check(an[2].nodeState == FormulaNodeState.Open && !blocked.gameObject.activeSelf, "Kapalı uç silgisi görseli temizlemedi.");
            an[2].nodeState = FormulaNodeState.Unstable;
            an[2].Erase();
            Check(an[2].nodeState == FormulaNodeState.Open, "Kararsız uç silinemedi.");
            Check(a.formulaLabel != null && a.formulaLabel.text == "Parça A", "Paylaşılan prefab parça adını göstermiyor.");

            an[0].ConnectTo(bn[0]);
            an[0].ConnectTo(bn[0]);
            c.GetOwnNodes()[2].nodeState = FormulaNodeState.Blocked;
            a.IsSynthesized = true;
            GameData save = new GameData();
            manager.SaveData(ref save);
            var restoredSave = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(save));
            Check(restoredSave.savedChalkboard.parts.Count == 3 && restoredSave.savedChalkboard.bonds.Count == 1, "Tahta JSON kaydı eksik.");
            FormulaNode templateNode = a.formulaData.chalkPrefab.GetComponent<DraggableFormula>().GetOwnNodes()[0];
            templateNode.connectionType = ConnectionType.Special;
            manager.LoadData(restoredSave);
            Check(board.GetComponentsInChildren<DraggableFormula>().Length == 3 && a != null,
                "Uyumsuz prefab uçları mevcut tahtayı değiştirdi veya artık parça bıraktı.");
            templateNode.connectionType = ConnectionType.Normal;
            a.formulaData.connectionCapacity = 1;
            manager.LoadData(restoredSave);
            Check(board.GetComponentsInChildren<DraggableFormula>().Length == 3 && a != null,
                "Kapasiteyi aşan kayıt mevcut tahtayı değiştirdi.");
            a.formulaData.connectionCapacity = 4;

            var missingOutput = ScriptableObject.CreateInstance<FormulaItemSO>();
            definitions.Add(missingOutput);
            object[] attemptArgs = { a, new List<FormulaItemSO> { missingOutput }, null };
            bool replaced = (bool)typeof(ChalkboardManager).GetMethod("TryReplaceMolecule", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, attemptArgs);
            Check(!replaced && a.gameObject.activeSelf && board.GetComponentsInChildren<DraggableFormula>().Length == 3,
                "Eksik çıktı prefabı girdileri tüketti.");

            manager.LoadData(restoredSave);
            DraggableFormula[] restored = board.GetComponentsInChildren<DraggableFormula>();
            DraggableFormula restoredA = Array.Find(restored, part => part.formulaData.itemID == "A");
            DraggableFormula restoredC = Array.Find(restored, part => part.formulaData.itemID == "C");
            Check(restored.Length == 3 && restoredA != null && restoredA.IsSynthesized, "Sentezlenmiş ürün yüklemede kayboldu.");
            Check(restoredA.GetOwnNodes()[0].bondType == BondType.Double && restoredA.GetConnectedParts().Count == 2,
                "Kayıt çift bağı veya bağlantı grubunu kaybetti.");
            Check(restoredC != null && restoredC.GetOwnNodes()[2].nodeState == FormulaNodeState.Blocked, "Kayıt kapalı uç durumunu kaybetti.");
            Check(Vector3.Distance(restoredA.transform.position, aPosition) < 0.0001f, "Döndürülmüş tahtada kayıt konumu değişti.");
            manager.ResetBoard();
            Check(board.GetComponentsInChildren<DraggableFormula>(true).Length == 0, "Yeni oyun tahtayı temizlemedi.");
            Debug.Log("Tahta doğrulaması geçti: döngü gruplaması, çift bağ kapasitesi, uç silgisi, etiketler, JSON kayıt/yükleme.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            foreach (FormulaItemSO definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
            SetManager(previous);
        }
    }

    private static DraggableFormula CreatePart(Transform host, Transform board, string id, Vector3 position, List<FormulaItemSO> definitions)
    {
        var data = ScriptableObject.CreateInstance<FormulaItemSO>();
        data.itemID = id;
        data.itemName = "Parça " + id;
        data.connectionCapacity = 4;
        definitions.Add(data);
        var template = new GameObject("Template " + id, typeof(BoxCollider), typeof(DraggableFormula));
        template.transform.SetParent(host);
        template.GetComponent<DraggableFormula>().formulaData = data;
        for (int i = 0; i < 3; i++)
        {
            var node = new GameObject("Node " + i, typeof(SphereCollider), typeof(LineRenderer));
            node.transform.SetParent(template.transform, false);
            node.AddComponent<FormulaNode>();
        }
        data.chalkPrefab = template;
        var formula = UnityEngine.Object.Instantiate(template, board).GetComponent<DraggableFormula>();
        formula.transform.localPosition = position;
        foreach (FormulaNode node in formula.GetOwnNodes()) Invoke(node, "Awake");
        formula.Initialize(data);
        return formula;
    }

    private static void SetManager(ChalkboardManager manager)
    {
        typeof(ChalkboardManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
            .GetSetMethod(true).Invoke(null, new object[] { manager });
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

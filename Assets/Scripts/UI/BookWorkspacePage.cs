using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BookWorkspacePage
{
    [Header("Basılı Kitap Sayfası")]
    [Min(0)] public int pageIndex;
    public string title;
    public string bookID;
    public string sourceLocation;
    [TextArea(3, 10)] public string explanation;
    [Tooltip("Metin kopyalanmaz; bu özel bloklar deftere aktarılabilir.")]
    public List<NotebookBlockData> blocks = new List<NotebookBlockData>();
}

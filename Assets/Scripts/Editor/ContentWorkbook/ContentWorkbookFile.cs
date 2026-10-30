using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

// Editor-only, also executable by tools/check-content-workbook.ps1 without Unity.
public sealed class ContentSheet
{
    public string Name;
    public readonly List<string[]> Rows = new List<string[]>();
    public ContentSheet(string name) { Name = name; }
    public void AddRow(string[] cells) { Rows.Add(cells); }
    public static string Cell(string[] row, int column) { return column < row.Length ? row[column] ?? "" : ""; }
}

public sealed class ContentWorkbookFile
{
    public static readonly string[] Names = { "Metinler", "Diyaloglar", "Altyazilar", "Baglantilar", "Kontroller", "Kilavuz" };
    public static readonly string[][] Headers = {
        new[] { "Kod", "Kaynak", "Türkçe", "İngilizce", "Bağlam" },
        new[] { "Diyalog", "Not", "Ses dosyası" },
        new[] { "Diyalog", "Sıra", "Süre (sn)", "Türkçe", "İngilizce", "Almanca" },
        new[] { "Bileşen kimliği", "Dosya", "Nesne", "Tür", "Giriş", "Hatırlatma 1", "Hatırlatma 2", "Bekleme (sn)", "Koşullar", "Ses override" },
        new[] { "Eylem", "Bağlama kimliği", "Bağlam", "Parça", "Varsayılan", "Değiştirilebilir", "Türkçe", "İngilizce" },
        new[] { "Konu", "Açıklama" }
    };
    public readonly List<ContentSheet> Sheets = new List<ContentSheet>();
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Relations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly Regex Arguments = new Regex(@"\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}");
    static readonly Regex Inputs = new Regex(@"\{input:([A-Za-z][A-Za-z0-9_]*)\}");
    sealed class FormatArgument : IFormattable { public string ToString(string format, IFormatProvider provider) { return "0"; } }
    static bool ValidFormat(string text)
    {
        foreach (Match match in Arguments.Matches(text))
        {
            int index;
            if (!int.TryParse(match.Groups[1].Value, out index) || index > 32) return false;
        }
        foreach (Match match in Regex.Matches(text, @"\{\d+,\s*(-?\d+)"))
        {
            int alignment;
            if (!int.TryParse(match.Groups[1].Value, out alignment) || alignment < -1000 || alignment > 1000) return false;
        }
        try
        {
            string escaped = Inputs.Replace(text, m => "{" + m.Value + "}");
            string.Format(CultureInfo.InvariantCulture, escaped, Enumerable.Repeat<object>(new FormatArgument(), 33).ToArray()); return true;
        }
        catch (FormatException) { return false; }
    }

    public ContentSheet Sheet(string name) { return Sheets.Find(s => s.Name == name); }
    public static ContentWorkbookFile CreateTemplate()
    {
        var result = new ContentWorkbookFile();
        for (int i = 0; i < Names.Length; i++) { var sheet = new ContentSheet(Names[i]); sheet.AddRow(Headers[i]); result.Sheets.Add(sheet); }
        return result;
    }

    public void Validate()
    {
        Errors.Clear(); Warnings.Clear();
        for (int i = 0; i < Names.Length; i++)
        {
            var sheet = Sheet(Names[i]);
            if (sheet == null || sheet.Rows.Count == 0) { Errors.Add(Names[i] + ": sekme/başlık yok."); continue; }
            if (!sheet.Rows[0].SequenceEqual(Headers[i])) Errors.Add(Names[i] + ": sütun başlıkları değişmiş. Kılavuzdaki şemayı kullanın.");
        }
        if (Errors.Count > 0) return;
        var dialogues = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var bindings = new HashSet<string>(StringComparer.Ordinal);
        var actions = new HashSet<string>(Data("Kontroller").Select(r => ContentSheet.Cell(r, 0)), StringComparer.Ordinal);
        Each("Diyaloglar", (r, line) => {
            string id = ContentSheet.Cell(r, 0);
            if (!Regex.IsMatch(id, @"^[A-Za-z_][A-Za-z0-9_]*$") || !dialogues.Add(id)) Error("Diyaloglar", line, "boş/geçersiz veya yinelenen diyalog kodu: " + id);
        });
        Each("Metinler", (r, line) => {
            string key = ContentSheet.Cell(r, 0);
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key) || key.StartsWith("subtitle.", StringComparison.Ordinal)) Error("Metinler", line, "boş/yinelenen veya altyazı için ayrılmış kod: " + key);
            CheckText("Metinler", line, ContentSheet.Cell(r, 1), ContentSheet.Cell(r, 2), ContentSheet.Cell(r, 3), actions);
        });
        var orders = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        Each("Altyazilar", (r, line) => {
            string id = ContentSheet.Cell(r, 0); int order;
            if (!dialogues.Contains(id)) Error("Altyazilar", line, "diyalog bulunamadı: " + id);
            if (!orders.ContainsKey(id)) orders[id] = new HashSet<int>();
            if (!int.TryParse(ContentSheet.Cell(r, 1), NumberStyles.None, CultureInfo.InvariantCulture, out order) || order < 1 || !orders[id].Add(order)) Error("Altyazilar", line, "sıra pozitif, tam sayı ve konuşma içinde benzersiz olmalı.");
            if (!Positive(ContentSheet.Cell(r, 2))) Error("Altyazilar", line, "süre sıfırdan büyük, sonlu bir sayı olmalı.");
            CheckText("Altyazilar", line, ContentSheet.Cell(r, 4), ContentSheet.Cell(r, 3), ContentSheet.Cell(r, 4), actions);
            string de = ContentSheet.Cell(r, 5);
            if (!string.IsNullOrEmpty(de)) CheckText("Altyazilar", line, ContentSheet.Cell(r, 4), de, de, actions, true);
        });
        foreach (string id in dialogues)
        {
            HashSet<int> sequence;
            if (!orders.TryGetValue(id, out sequence) || sequence.Count == 0) Errors.Add("Altyazilar: konuşmanın cümlesi yok: " + id);
            else if (!Enumerable.Range(1, sequence.Count).All(sequence.Contains)) Errors.Add("Altyazilar: sıra 1'den başlamalı ve kesintisiz olmalı: " + id);
        }
        Each("Kontroller", (r, line) => {
            if (!Regex.IsMatch(ContentSheet.Cell(r, 0), @"^[A-Za-z][A-Za-z0-9_]*$")) Error("Kontroller", line, "geçersiz eylem kodu.");
            string binding = ContentSheet.Cell(r, 1);
            if (string.IsNullOrWhiteSpace(binding) || !bindings.Add(binding)) Error("Kontroller", line, "boş/yinelenen bağlama kimliği.");
            if (string.IsNullOrWhiteSpace(ContentSheet.Cell(r, 4))) Error("Kontroller", line, "varsayılan kontrol yolu boş.");
            if (ContentSheet.Cell(r, 5) != "Evet" && ContentSheet.Cell(r, 5) != "Hayır") Error("Kontroller", line, "Değiştirilebilir: Evet veya Hayır olmalı.");
        });
        var targets = new HashSet<string>(StringComparer.Ordinal);
        Each("Baglantilar", (r, line) => {
            if (!targets.Add(ContentSheet.Cell(r, 0))) Error("Baglantilar", line, "yinelenen bileşen kimliği.");
            for (int c = 4; c <= 6; c++)
                if (!string.IsNullOrWhiteSpace(ContentSheet.Cell(r, c)) && !dialogues.Contains(ContentSheet.Cell(r, c)))
                {
                    if (ContentSheet.Cell(r, 3) == "DialogAction") Error("Baglantilar", line, "bilinmeyen konuşma: " + ContentSheet.Cell(r, c));
                    else Warnings.Add("Baglantilar satır " + line + ": mevcut salt okunur tüketicinin konuşması veritabanında yok: " + ContentSheet.Cell(r, c));
                }
            if (ContentSheet.Cell(r, 3) == "DialogAction" && !string.IsNullOrEmpty(ContentSheet.Cell(r, 7)) && !Positive(ContentSheet.Cell(r, 7))) Error("Baglantilar", line, "bekleme süresi pozitif olmalı.");
        });
    }

    public IEnumerable<string[]> Data(string name) { return Sheet(name).Rows.Skip(1).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))); }
    void Each(string name, Action<string[], int> action)
    {
        var rows = Sheet(name).Rows;
        for (int i = 1; i < rows.Count; i++) if (rows[i].Any(c => !string.IsNullOrWhiteSpace(c))) action(rows[i], i + 1);
    }
    void Error(string sheet, int row, string message) { Errors.Add(sheet + " satır " + row + ": " + message); }
    static bool Positive(string value) { double number; return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && number > 0 && !double.IsInfinity(number) && !double.IsNaN(number); }
    void CheckText(string sheet, int line, string source, string tr, string en, HashSet<string> actions, bool optionalTranslation = false)
    {
        var expected = new HashSet<string>(Arguments.Matches(source).Cast<Match>().Select(m => m.Groups[1].Value));
        // Existing canonical sources intentionally retain their original [F]/[Tab] aliases.
        // English owns token identity; a new source that already uses tokens owns it directly.
        var expectedInputs = new HashSet<string>(Inputs.Matches(source.Contains("{input:") ? source : en).Cast<Match>().Select(m => m.Groups[1].Value));
        foreach (string text in new[] { tr, en })
        {
            if (string.IsNullOrWhiteSpace(text)) { Warnings.Add(sheet + " satır " + line + ": eksik çeviri; mevcut dil geri dönüşü kullanılır."); continue; }
            if (!ValidFormat(text)) Error(sheet, line, "bozuk süslü parantez veya biçim değişkeni. {0} ve {input:Cancel} biçimlerini koruyun.");
            if (!expected.SetEquals(Arguments.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value))) Error(sheet, line, "{0} gibi biçim değişkenleri uyuşmuyor.");
            if (!expectedInputs.SetEquals(Inputs.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value)))
            {
                if (optionalTranslation) Warnings.Add(sheet + " satır " + line + ": isteğe bağlı Almanca çevirisindeki tuş değişkenleri güncellenmeli.");
                else Error(sheet, line, "{input:...} eylem değişkenleri uyuşmuyor.");
            }
            foreach (Match match in Inputs.Matches(text)) if (!actions.Contains(match.Groups[1].Value)) Error(sheet, line, "bilinmeyen tuş eylemi: " + match.Groups[1].Value);
            if (Inputs.Replace(text, "").Contains("{input:")) Error(sheet, line, "bozuk tuş değişkeni.");
        }
    }

    public static ContentWorkbookFile Read(string path)
    {
        var book = new ContentWorkbookFile();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
            var shared = sharedEntry == null ? new List<string>() : Load(sharedEntry).Root.Elements(Main + "si").Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))).ToList();
            foreach (var pair in SheetPaths(zip))
            {
                var sheet = new ContentSheet(pair.Key);
                var xml = Load(Required(zip, pair.Value));
                foreach (var row in xml.Descendants(Main + "sheetData").Elements(Main + "row"))
                {
                    int number = (int?)row.Attribute("r") ?? sheet.Rows.Count + 1;
                    if (number > 100000) throw new InvalidDataException(pair.Key + ": 100000 satır sınırı aşıldı.");
                    while (sheet.Rows.Count < number) sheet.Rows.Add(new string[0]);
                    var cells = new SortedDictionary<int, string>();
                    foreach (var cell in row.Elements(Main + "c"))
                    {
                        string address = (string)cell.Attribute("r") ?? "A";
                        int column = ColumnIndex(address);
                        if (column > 100) throw new InvalidDataException(pair.Key + ": sütun sınırı aşıldı.");
                        if (cell.Element(Main + "f") != null) throw new InvalidDataException(pair.Key + " " + address + ": formül hücresi içe alınmaz; değere dönüştürün.");
                        string type = (string)cell.Attribute("t"), value = (string)cell.Element(Main + "v") ?? "";
                        if (type == "s") { int index; if (!int.TryParse(value, out index) || index < 0 || index >= shared.Count) throw new InvalidDataException("Bozuk paylaşılan metin: " + address); value = shared[index]; }
                        else if (type == "inlineStr") value = string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
                        else if (type == "e") throw new InvalidDataException(pair.Key + " " + address + ": Excel hata hücresi.");
                        cells[column] = value;
                    }
                    var values = new string[cells.Count == 0 ? 0 : cells.Keys.Max() + 1];
                    foreach (var cell in cells) values[cell.Key] = cell.Value;
                    sheet.Rows[number - 1] = values;
                }
                // Excel omits empty trailing cells; normalize only the documented width.
                int schema = Array.IndexOf(Names, sheet.Name);
                if (schema >= 0)
                    for (int r = 0; r < sheet.Rows.Count; r++)
                    {
                        var normalized = new string[Math.Max(Headers[schema].Length, sheet.Rows[r].Length)];
                        for (int c = 0; c < normalized.Length; c++) normalized[c] = ContentSheet.Cell(sheet.Rows[r], c);
                        sheet.Rows[r] = normalized;
                    }
                book.Sheets.Add(sheet);
            }
        }
        return book;
    }

    static Dictionary<string, string> SheetPaths(ZipArchive zip)
    {
        var rels = Load(Required(zip, "xl/_rels/workbook.xml.rels")).Root.Elements().ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"));
        var result = new Dictionary<string, string>();
        foreach (var sheet in Load(Required(zip, "xl/workbook.xml")).Descendants(Main + "sheet"))
        {
            string target = rels[(string)sheet.Attribute(Relations + "id")];
            var uri = new Uri(new Uri("http://xlsx/xl/workbook.xml"), target);
            result.Add((string)sheet.Attribute("name"), Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')));
        }
        return result;
    }
    static ZipArchiveEntry Required(ZipArchive zip, string name) { var entry = zip.GetEntry(name); if (entry == null) throw new InvalidDataException("XLSX parçası eksik: " + name); return entry; }
    static XDocument Load(ZipArchiveEntry entry)
    {
        if (entry.Length > 32 * 1024 * 1024) throw new InvalidDataException("XLSX parçası çok büyük.");
        using (var stream = entry.Open())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) return XDocument.Load(reader);
    }
    static int ColumnIndex(string address) { int value = 0; foreach (char c in address) { if (c < 'A' || c > 'Z') break; value = value * 26 + c - 'A' + 1; } return value - 1; }
    static string Column(int index) { string name = ""; for (int i = index + 1; i > 0; i = (i - 1) / 26) name = (char)('A' + (i - 1) % 26) + name; return name; }
    static XElement DataElement(ContentSheet sheet, Dictionary<int, string> styles = null)
    {
        return new XElement(Main + "sheetData", sheet.Rows.Select((row, r) => new XElement(Main + "row", new XAttribute("r", r + 1), row.Select((value, c) => {
            var cell = new XElement(Main + "c", new XAttribute("r", Column(c) + (r + 1)), new XAttribute("t", "inlineStr"), new XElement(Main + "is", new XElement(Main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? "")));
            bool numeric = r > 0 && (sheet.Name == "Altyazilar" && (c == 1 || c == 2) || sheet.Name == "Baglantilar" && c == 7);
            double number;
            if (numeric && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                cell.Attribute("t").Remove(); cell.RemoveNodes(); cell.Add(new XElement(Main + "v", value));
            }
            if (r > 0 && styles != null && styles.ContainsKey(c)) cell.SetAttributeValue("s", styles[c]);
            return cell;
        }))));
    }
    static void Save(ZipArchive zip, string name, XDocument document)
    {
        var old = zip.Mode == ZipArchiveMode.Create ? null : zip.GetEntry(name); if (old != null) old.Delete();
        using (var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open()) document.Save(stream);
    }
    public static void ReplaceSheet(string path, ContentSheet sheet)
    {
        string temporary = path + ".updating";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            var previousSheet = Read(path).Sheet(sheet.Name);
            // Exclusive lock before preparing a replacement: Excel may have the book open.
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var target = new FileStream(temporary, FileMode.CreateNew)) source.CopyTo(target);
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Update))
            {
                string part = SheetPaths(zip)[sheet.Name]; var xml = Load(Required(zip, part));
                var styles = new Dictionary<int, string>();
                var sample = xml.Descendants(Main + "row").Skip(1).FirstOrDefault();
                if (sample != null) foreach (var c in sample.Elements(Main + "c")) if (c.Attribute("s") != null) styles[ColumnIndex((string)c.Attribute("r"))] = (string)c.Attribute("s");
                var oldHeader = xml.Descendants(Main + "row").FirstOrDefault();
                var previousRows = xml.Descendants(Main + "sheetData").Elements(Main + "row").ToDictionary(r => (int)r.Attribute("r"));
                var keyedRows = new Dictionary<string, XElement>(StringComparer.Ordinal);
                var typedRows = new Dictionary<string, XElement>(StringComparer.Ordinal);
                for (int r = 1; r < previousSheet.Rows.Count; r++)
                    if (previousRows.TryGetValue(r + 1, out var oldRow))
                    {
                        keyedRows[ContentSheet.Cell(previousSheet.Rows[r], 0)] = oldRow;
                        typedRows[ContentSheet.Cell(previousSheet.Rows[r], 3)] = oldRow;
                    }
                var data = DataElement(sheet, styles);
                if (oldHeader != null) data.Elements().First().ReplaceWith(new XElement(oldHeader));
                foreach (var row in data.Elements().Skip(1))
                {
                    int index = (int)row.Attribute("r") - 1;
                    XElement template;
                    if (!keyedRows.TryGetValue(ContentSheet.Cell(sheet.Rows[index], 0), out template)) typedRows.TryGetValue(ContentSheet.Cell(sheet.Rows[index], 3), out template);
                    if (template != null)
                    {
                        foreach (var attribute in template.Attributes().Where(a => a.Name.LocalName != "r")) row.SetAttributeValue(attribute.Name, attribute.Value);
                        var cellStyles = template.Elements(Main + "c").Where(c => c.Attribute("s") != null).ToDictionary(c => ColumnIndex((string)c.Attribute("r")), c => (string)c.Attribute("s"));
                        foreach (var cell in row.Elements(Main + "c")) if (cellStyles.TryGetValue(ColumnIndex((string)cell.Attribute("r")), out var style)) cell.SetAttributeValue("s", style);
                    }
                }
                xml.Root.Element(Main + "sheetData").ReplaceWith(data);
                string range = "A1:" + Column(sheet.Rows[0].Length - 1) + Math.Max(1, sheet.Rows.Count);
                var dimension = xml.Root.Element(Main + "dimension"); if (dimension != null) dimension.SetAttributeValue("ref", range);
                var filter = xml.Root.Element(Main + "autoFilter"); if (filter != null) filter.SetAttributeValue("ref", range);
                Save(zip, part, xml);
                // Keep Excel table extents in sync when a connection is added or removed.
                string relPath = part.Substring(0, part.LastIndexOf('/') + 1) + "_rels/" + Path.GetFileName(part) + ".rels";
                var relEntry = zip.GetEntry(relPath);
                if (relEntry != null)
                    foreach (var rel in Load(relEntry).Root.Elements().Where(e => ((string)e.Attribute("Type") ?? "").EndsWith("/table", StringComparison.Ordinal)))
                    {
                        string tablePath = new Uri(new Uri("http://xlsx/" + part), (string)rel.Attribute("Target")).AbsolutePath.TrimStart('/');
                        var table = Load(Required(zip, tablePath)); table.Root.SetAttributeValue("ref", range);
                        var tableFilter = table.Root.Element(Main + "autoFilter"); if (tableFilter != null) tableFilter.SetAttributeValue("ref", range);
                        Save(zip, tablePath, table);
                    }
            }
            File.Replace(temporary, path, null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    // Small standard OOXML writer for regression fixtures and Editor re-export.
    // The delivered, formatted master is authored with artifact-tool.
    public static void WriteNew(string path, ContentWorkbookFile book)
    {
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            XNamespace package = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
            var contentTypes = new XElement(types + "Types",
                new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
            contentTypes.Add(book.Sheets.Select((s, i) => new XElement(types + "Override", new XAttribute("PartName", "/xl/worksheets/sheet" + (i + 1) + ".xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
            Save(zip, "[Content_Types].xml", new XDocument(contentTypes));
            Save(zip, "_rels/.rels", new XDocument(new XElement(package + "Relationships", new XElement(package + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", Relations.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
            Save(zip, "xl/workbook.xml", new XDocument(new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", Relations), new XElement(Main + "sheets", book.Sheets.Select((s, i) => new XElement(Main + "sheet", new XAttribute("name", s.Name), new XAttribute("sheetId", i + 1), new XAttribute(Relations + "id", "rId" + (i + 1))))))));
            Save(zip, "xl/_rels/workbook.xml.rels", new XDocument(new XElement(package + "Relationships", book.Sheets.Select((s, i) => new XElement(package + "Relationship", new XAttribute("Id", "rId" + (i + 1)), new XAttribute("Type", Relations.NamespaceName + "/worksheet"), new XAttribute("Target", "worksheets/sheet" + (i + 1) + ".xml"))))));
            for (int i = 0; i < book.Sheets.Count; i++) Save(zip, "xl/worksheets/sheet" + (i + 1) + ".xml", new XDocument(new XElement(Main + "worksheet", DataElement(book.Sheets[i]))));
        }
    }
}

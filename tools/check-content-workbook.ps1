$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$source = Join-Path $project 'Assets/Scripts/Editor/ContentWorkbook/ContentWorkbookFile.cs'
if (!(Test-Path $source)) { throw 'Content workbook reader/validator has not been implemented.' }
Add-Type -Path $source
function Check($value, $message) { if (!$value) { throw $message } }
$book = [ContentWorkbookFile]::CreateTemplate()
$book.Sheet('Diyaloglar').AddRow([string[]]@('test_dialogue', 'İki cümle', ''))
$book.Sheet('Altyazilar').AddRow([string[]]@('test_dialogue', '2', '4.5', 'İkinci cümle', 'Second line', ''))
$book.Sheet('Altyazilar').AddRow([string[]]@('test_dialogue', '1', '2', "İlk satır`nİkinci satır", "First line`nSecond line", ''))
$book.Sheet('Metinler').AddRow([string[]]@('test.key', 'Close {0}', 'Kapat {0}', 'Close {0}', 'Test'))
$book.Validate()
Check ($book.Errors.Count -eq 0) ($book.Errors -join "`n")
$tmp = Join-Path $project 'Temp/content-workbook-check.xlsx'
if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp }
[ContentWorkbookFile]::WriteNew($tmp, $book)
$copy = [ContentWorkbookFile]::Read($tmp)
$copy.Validate()
Check ($copy.Errors.Count -eq 0) 'Valid workbook failed round trip.'
Check ($copy.Sheet('Altyazilar').Rows[2][3] -ceq "İlk satır`nİkinci satır") 'Multiline Turkish text changed.'
$copy.Sheet('Altyazilar').Rows[1][1] = '1'
$copy.Validate()
Check ($copy.Errors.Count -gt 0) 'Duplicate sequence accepted.'
$copy.Sheet('Altyazilar').Rows[1][1] = '2'
$copy.Sheet('Altyazilar').Rows[1][2] = 'NaN'
$copy.Validate()
Check ($copy.Errors.Count -gt 0) 'NaN duration accepted.'
$copy.Sheet('Altyazilar').Rows[1][2] = '0'
$copy.Validate()
Check ($copy.Errors.Count -gt 0) 'Zero duration accepted.'
$copy.Sheet('Altyazilar').Rows[1][2] = '4.5'
$copy.Sheet('Metinler').Rows[1][2] = 'Kapat'
$copy.Validate()
Check ($copy.Errors.Count -gt 0) 'Missing format argument accepted.'
$copy.Sheet('Metinler').Rows[1][2] = 'Kapat {0}'
$copy.Sheet('Metinler').Rows[1][2] = 'Kapat {0} {oops}'
$copy.Validate()
Check ($copy.Errors.Count -gt 0) 'Malformed composite format variable accepted.'
$copy.Sheet('Metinler').Rows[1][2] = 'Kapat {0}'
$copy.Sheet('Altyazilar').Rows[1][0] = 'unknown'
$copy.Validate()
Check ($copy.Errors.Count -gt 0) 'Unknown dialogue accepted.'
$copy.Sheet('Altyazilar').Rows[1][0] = 'test_dialogue'
$before = [IO.File]::ReadAllBytes($tmp)
$copy.Sheet('Baglantilar').AddRow([string[]]@('fake-id','Assets/test.unity','Object','DialogAction','test_dialogue','','','10','', ''))
[ContentWorkbookFile]::ReplaceSheet($tmp, $copy.Sheet('Baglantilar'))
$refreshed = [ContentWorkbookFile]::Read($tmp)
Check ($refreshed.Sheet('Altyazilar').Rows[2][3] -ceq "İlk satır`nİkinci satır") 'Connection refresh changed existing content.'
Check ($refreshed.Sheet('Baglantilar').Rows.Count -eq 2) 'Connection refresh lost rows.'
# Insert a formula into an actual XLSX cell. The parser must reject cached formula values.
$zip = [IO.Compression.ZipFile]::Open($tmp, [IO.Compression.ZipArchiveMode]::Update)
try {
    $entry = $zip.GetEntry('xl/worksheets/sheet1.xml')
    $reader = [IO.StreamReader]::new($entry.Open()); $xml = $reader.ReadToEnd(); $reader.Dispose()
    $entry.Delete()
    $xml = $xml.Replace('</sheetData>', '<row r="100"><c r="A100"><f>1+1</f><v>2</v></c></row></sheetData>')
    $entry = $zip.CreateEntry('xl/worksheets/sheet1.xml'); $writer = [IO.StreamWriter]::new($entry.Open()); $writer.Write($xml); $writer.Dispose()
} finally { $zip.Dispose() }
$rejected = $false
try { [ContentWorkbookFile]::Read($tmp) | Out-Null } catch { $rejected = $_.Exception.Message -match 'formül|formula' }
Check $rejected 'Formula cell was accepted.'
Write-Output 'CONTENT WORKBOOK PASS: XLSX round trip, sorted sequences, multiline Unicode, invalid references/durations/placeholders, preserving refresh and formula rejection.'

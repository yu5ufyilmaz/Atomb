$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$catalogSource = Join-Path $project 'Assets/Scripts/Localization/LocalizationCatalog.cs'
if (!(Test-Path -LiteralPath $catalogSource)) { throw 'Missing shared localization catalog.' }
Add-Type -Path $catalogSource
Add-Type -Path (Join-Path $project 'Assets/Scripts/PuzzleSystems/Workshop/SignalPartLibrary.cs')
function Check($condition, $message) { if (!$condition) { throw $message } }
$entries = [LocalizationEntry[]]@(
    [LocalizationEntry]@{key='part.source'; source='Atım Kaynağı'; text='Pulse Source'},
    [LocalizationEntry]@{key='counter'; source='TOPLANAN HEDEFLER ({0})'; text='COLLECTED TARGETS ({0})'},
    [LocalizationEntry]@{key='target'; source='Hedef: {0}'; text='Target: {0}'},
    [LocalizationEntry]@{key='repeat'; source='{0} parça / {0} ölçüm'; text='{0} parts / {0} readings'},
    [LocalizationEntry]@{key='empty'; source='Özgün'; text=''}
)
$catalog = [LocalizationCatalog]::new($entries, [Globalization.CultureInfo]::GetCultureInfo('en'))
Check ($catalog.Translate('Atım Kaynağı') -ceq 'Pulse Source') 'Canonical part text was not translated.'
Check ($catalog.Get('part.source', 'fallback') -ceq 'Pulse Source') 'Stable key was not resolved.'
Check ($catalog.Get('missing', 'Atım Kaynağı') -ceq 'Pulse Source') 'Missing key did not use its readable source fallback.'
Check ($catalog.Translate('TOPLANAN HEDEFLER (3)') -ceq 'COLLECTED TARGETS (3)') 'Legacy formatted value was not localized.'
Check ($catalog.Translate('Hedef: Atım Kaynağı') -ceq 'Target: Pulse Source') 'Nested canonical content was not translated.'
Check ($catalog.Translate('<b>Atım Kaynağı</b>') -ceq '<b>Pulse Source</b>') 'Rich-text markup was changed or text was not localized.'
Check ($catalog.Translate("Atım Kaynağı`nECHOECHO_=_001") -ceq "Pulse Source`nECHOECHO_=_001") 'Codes or multiline content changed incorrectly.'
Check ($catalog.Translate('2 parça / 2 ölçüm') -ceq '2 parts / 2 readings') 'Repeated format argument was not preserved.'
Check ($catalog.Translate('2 parça / 3 ölçüm') -ceq '2 parça / 3 ölçüm') 'Unequal repeated arguments matched an unrelated template.'
Check ($catalog.Translate('pulse-source') -ceq 'pulse-source') 'An identifier was translated.'
Check ($catalog.Translate('Özgün') -ceq 'Özgün') 'Empty translation erased visible content.'
Check ($catalog.Format('TOPLANAN HEDEFLER ({0})', [object[]]@(4)) -ceq 'COLLECTED TARGETS (4)') 'Explicit dynamic format did not work.'

$enTablePath = Join-Path $project 'Assets/Resources/Localization/Tables/en.json'
$trTablePath = Join-Path $project 'Assets/Resources/Localization/Tables/tr.json'
Check (Test-Path -LiteralPath $enTablePath) 'Missing en.json table.'
Check (Test-Path -LiteralPath $trTablePath) 'Missing tr.json table.'
$enJson = [System.IO.File]::ReadAllText($enTablePath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
$trJson = [System.IO.File]::ReadAllText($trTablePath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
Check ($enJson.entries.Count -ge 600) 'en.json entry count is unexpectedly low.'
Check ($enJson.entries.Count -eq $trJson.entries.Count) 'en.json and tr.json entry counts do not match.'
$realEnEntries = [System.Collections.Generic.List[LocalizationEntry]]::new()
foreach ($item in $enJson.entries) {
    $realEnEntries.Add([LocalizationEntry]@{ key = $item.key; source = $item.source; text = $item.text })
}
$realEnCatalog = [LocalizationCatalog]::new($realEnEntries, [Globalization.CultureInfo]::GetCultureInfo('en'))
Check ($realEnCatalog.Get('notebook.ws.title', '') -ceq 'RESEARCH NOTEBOOK') 'Real en.json table failed to resolve notebook header key.'
Check ($realEnCatalog.Translate('NEW GAME') -ceq 'NEW GAME') 'Real en.json table failed to translate menu string.'
Check ($realEnCatalog.Format('Page {0}/{1}', [object[]]@(1, 2)) -ceq 'Page 1/2') 'Real en.json table failed to format page counter.'
Check ($realEnCatalog.Get('subtitle.start_game.0', '') -ceq 'Leaving that bright chair at the university was a mistake, Arthur.') 'Subtitle key lookup failed in real en.json.'

$missingParts = @()
foreach ($part in [SignalPartLibrary]::All) {
    if ($realEnCatalog.Translate($part.title) -ceq $part.title) { $missingParts += "$($part.id) title" }
    if ($realEnCatalog.Translate($part.description) -ceq $part.description) { $missingParts += "$($part.id) description" }
}
Check ($missingParts.Count -eq 0) "Current signal catalog is untranslated: $($missingParts -join ', ')"
Check ($realEnCatalog.Translate('Numune N-12') -ceq 'Sample N-12') 'The current sample name or its code was lost.'
Check ($realEnCatalog.Translate('Gelen akışa uygulanır. Kısa tepki: gelen izin genişliğini 0,025 yapar. Zaman ve yükseklik korunur.') -ceq 'Applies to the incoming flow. Short response: sets the incoming trace width to 0.025. Time and height are preserved.') 'A distributed part description was only partly translated.'

# Read the current mixture catalog's data literals, then exercise the shared resolver.
$mixtureSource = [IO.File]::ReadAllText((Join-Path $project 'Assets/Scripts/PuzzleSystems/Workshop/ColorMixtureLibrary.cs'))
$mixtureParts = [regex]::Matches($mixtureSource, 'Part\(\w+, "([^"]+)", "([^"]+)", \d+, \d+, (?:true|false), "([^"]+)"\)')
Check ($mixtureParts.Count -eq 9) 'The mixture catalog coverage needs updating.'
foreach ($part in $mixtureParts) {
    foreach ($index in 1, 2, 3) {
        $source = $part.Groups[$index].Value
        Check ($realEnCatalog.Translate($source) -cne $source) "Current mixture text is untranslated: $source"
    }
}

$realTrEntries = [System.Collections.Generic.List[LocalizationEntry]]::new()
foreach ($item in $trJson.entries) {
    $realTrEntries.Add([LocalizationEntry]@{ key = $item.key; source = $item.source; text = $item.text })
}
$realTrCatalog = [LocalizationCatalog]::new($realTrEntries, [Globalization.CultureInfo]::GetCultureInfo('tr'))
foreach ($part in [SignalPartLibrary]::All) {
    Check ($realTrCatalog.Translate($part.title) -ceq $part.title) "Turkish canonical part title changed: $($part.id)"
    Check ($realTrCatalog.Translate($part.description) -ceq $part.description) "Turkish canonical part description changed: $($part.id)"
    Check ($realEnCatalog.Translate($part.id) -ceq $part.id) "A part identity was translated: $($part.id)"
}

$untranslatedGoals = @()
$goalCount = 0
foreach ($asset in Get-ChildItem (Join-Path $project 'Assets/Scripts/Scriptables/Puzzles') -Filter '*.asset' -Recurse) {
    $assetText = [IO.File]::ReadAllText($asset.FullName)
    $fields = [regex]::Matches($assetText, '(?m)^ {4,}(title|description|explanation): ("(?:\\.|[^"\\])*"|[^\r\n]*)')
    if ($fields.Count -eq 0) { continue }
    $goalCount++
    foreach ($field in $fields) {
        $value = [regex]::Replace($field.Groups[2].Value, '[ \t]*\r?\n[ \t]*', ' ')
        if ($value.StartsWith('"')) { $value = [regex]::Unescape($value.Substring(1, $value.Length - 2)) }
        if ([string]::IsNullOrWhiteSpace($value)) { continue }
        $translated = $realEnCatalog.Translate($value)
        if ($translated -ceq $value -or $translated -cmatch '[çğıöşüÇĞİÖŞÜ]') {
            $untranslatedGoals += "$($asset.BaseName) $($field.Groups[1].Value)"
        }
    }
}
Check ($goalCount -ge 10) 'Puzzle goal coverage did not find the authored puzzle definitions and active archive pool.'
Check ($untranslatedGoals.Count -eq 0) "Authored puzzle goals are untranslated: $($untranslatedGoals -join ', ')"

$lessonSource = [IO.File]::ReadAllText((Join-Path $project 'Assets/Scripts/PuzzleSystems/Chalkboard/ChalkboardTutorial.cs'))
$lessonFields = [regex]::Matches($lessonSource, '(?:title|description|explanation) = "((?:\\.|[^"\\])*)"')
foreach ($field in $lessonFields) {
    $value = [regex]::Unescape($field.Groups[1].Value)
    Check ($realEnCatalog.Translate($value) -cne $value) "Untranslated generated tutorial text: $value"
}

Write-Output "LOCALIZATION CATALOG PASS: source/key, missing/empty fallback, legacy formats, nested content, markup, stable codes, all 19 current parts, $goalCount authored goals, TR source preservation, and $($enJson.entries.Count) real table entries."

$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $project 'Assets/Scripts/Localization/InputHintText.cs')
Add-Type -Path (Join-Path $project 'Assets/Scripts/Localization/LocalizationCatalog.cs')
function Check($value, $message) { if (!$value) { throw $message } }
$entry = [LocalizationEntry]@{key='hint'; source='[{input:Cancel}] Kapat {0}'; text='[{input:Cancel}] Close {0}'}
$catalog = [LocalizationCatalog]::new([LocalizationEntry[]]@($entry), [Globalization.CultureInfo]::InvariantCulture)
$formatted = $catalog.Format('[{input:Cancel}] Kapat {0}', [object[]]@(3))
Check ($formatted -ceq '[{input:Cancel}] Close 3') 'Input tokens broke localization format arguments.'
$resolver = [Func[string,string]]{ param($action) if ($action -eq 'Cancel') { 'G' } else { 'Unbound' } }
Check ([InputHintText]::Resolve($formatted, $resolver) -ceq '[G] Close 3') 'The current binding was not expanded after translation.'
Check ($catalog.Translate('[{input:Cancel}] Kapat 7') -ceq '[{input:Cancel}] Close 7') 'Legacy formatted source lost its input token.'
Write-Output 'INPUT HINTS PASS: translation, numeric formatting and live action tokens coexist.'

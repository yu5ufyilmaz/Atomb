$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$clockSource = Join-Path $project 'Assets/Scripts/Managers/TutorialThoughtClock.cs'
if (!(Test-Path -LiteralPath $clockSource)) { throw 'Missing tutorial thought clock: contextual reminders are not implemented.' }
Add-Type -Path $clockSource
$clock = [TutorialThoughtClock]::new()
function Assert-Voice($condition, $message) { if (!$condition) { throw $message } }
Assert-Voice ($clock.Advance('book', 0, .4, 10, $false) -eq 0) 'Opening thought is missing.'
Assert-Voice ($clock.Advance('book', 0, 60, 10, $true) -eq -1) 'Blocked time emitted a reminder.'
Assert-Voice ($clock.Advance('book', 0, 9, 10, $false) -eq -1) 'Reminder fired before ten seconds.'
Assert-Voice ($clock.Advance('book', 0, 1, 10, $false) -eq 1) 'First reminder did not follow the opening.'
Assert-Voice ($clock.Advance('book', 1, 0, 10, $false) -eq -1) 'Progress emitted a stale thought.'
Assert-Voice ($clock.Advance('book', 1, 9, 10, $false) -eq -1) 'Progress did not postpone the reminder.'
Assert-Voice ($clock.Advance('book', 1, 1, 10, $false) -eq 2) 'Progress replayed the introduction or lost the second variant.'
Assert-Voice ($clock.Advance('book', 1, 29, 10, $false) -eq -1) 'Repeated reminders did not become less frequent.'
Assert-Voice ($clock.Advance('book', 1, 1, 10, $false) -eq 1) 'Long wait did not resume a different reminder.'
Assert-Voice ($clock.Advance('board', 0, .4, 30, $false) -eq 0) 'A new step kept the previous reminder.'
Assert-Voice ($clock.Advance('board', 0, 29, 30, $false) -eq -1) 'Reading/building did not allow extra time.'
Assert-Voice ($clock.Advance('board', 0, 1, 30, $false) -eq 1) 'Reading/building reminder did not fire.'
Assert-Voice ($clock.Advance($null, 0, 200, 10, $false) -eq -1) 'Disabled tutorial emitted a thought.'
Assert-Voice ($clock.Advance('book', 0, .4, 10, $false) -eq 0) 'New tutorial retained the old timer.'
Assert-Voice ($clock.Advance('hide', 0, 200, 10, $true) -eq -1) 'A blocked transition spoke over existing dialogue.'
Assert-Voice ($clock.Advance('hide', 0, .4, 10, $false) -eq 0) 'Blocked transition lost its opening.'
Assert-Voice ($clock.Advance('hide', 0, 10000, 10, $false) -eq 1) 'A long frame skipped reminder variants.'
Assert-Voice ($clock.Advance('hide', 0, 0, 10, $false) -eq -1) 'A long frame caused a burst of reminders.'
Write-Output 'TUTORIAL THOUGHT CLOCK PASS: timing, progress, step changes, blocked time, reset, backoff and frame spikes.'

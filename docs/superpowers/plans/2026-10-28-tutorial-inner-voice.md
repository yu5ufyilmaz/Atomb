# Tutorial Inner Voice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the persistent tutorial objective panel with a second character's contextual, timed thoughts throughout the laboratory tutorial.

**Architecture:** Keep ChalkboardTutorial's real completion conditions. TutorialManager owns a small thought clock and uses the existing subtitle manager/database; scene event DialogActions defer to this controller only inside the upgraded tutorial. Optional clips share subtitle ownership and cancel on step changes.

**Tech Stack:** Unity 6000.3.10f1, C#, TextMeshPro, existing editor verification menus.

**Spec:** docs/superpowers/specs/2026-10-28-tutorial-inner-voice-design.md

## Global Constraints

- Work in the existing checkout with its uncommitted tutorial; no commit/push.
- No persistent objective panel or recall key; distinct contextual reminders.
- Basic reminder delay: 10 seconds after speech; reading/building: 30 seconds.
- Preserve tutorial completion, interaction restrictions and external dialogue.
- TR/EN/DE subtitles; optional recordings only, no generated voice assets.

## Review Focus

- Progress within one step must postpone a reminder without replaying the introduction.
- A changed pressure reading must not continuously reset the reminder clock.
- A stale thought must stop on step change without cancelling an unrelated speaker.
- Pause, disabled tutorial, completion and new sessions must not leak reminders.
- Existing delayed tutorial dialogue must not overwrite the contextual sequence.

### Task 1: Contextual thought sequence

**Files:** Create Assets/Scripts/Managers/TutorialManager.InnerVoice.cs and TutorialThoughtClock.cs; modify TutorialManager.cs, ChalkboardTutorial.cs, GlobalSubtitleManager.cs, DialogAction.cs, MegaphoneSystem.cs, ObjectiveAction.cs, SubtitleData.cs and its editor; create Assets/Scripts/Editor/TutorialVoiceChecks.cs; modify MainSubtitles.asset.

**Interfaces:** TutorialThoughtClock.Advance(string step, int progress, float deltaTime, float reminderDelay, bool blocked) returns a line index or -1. ChalkboardTutorial exposes CurrentThought and GetThoughtProgress(string step). TutorialManager owns the clock and subtitle/audio playback.

- [x] Add editor and standalone clock checks for timing, cancellation, progress, blocked time, reset, localization and scene coverage. The standalone check failed first because the thought clock was absent.
- [x] Replace objective text with stable thought states; add clock and localized speech; remove the overlay; suppress only upgraded tutorial DialogActions.
- [x] Run Unity core checks; TUTORIAL VOICE CORE PASS and TUTORIAL CORE PASS confirmed in the existing editor.

### Task 2: Integration verification

**Files:** Extend TutorialVoiceChecks.cs with a live Play check; extend TutorialPlayChecks.cs with camera-blend waiting and rejected-submission coverage; update this plan with results.

- [x] Exercise subtitle replacement, ownership, pause and disabled tutorial in Play, then execute the existing full TutorialPlayChecks sequence.
- [x] Review changed files, run git diff --check and record exact results/limitations. Preserve pre-existing edits.

## Execution ledger

User explicitly requested integration after the design conversation; continue inline without another design/plan approval loop. The existing dirty tutorial is required input, so a fresh worktree would omit necessary work.

- Ruling: Unity batch cannot open the project because it is already open. Use the project's existing local Unity MCP connection on port 6400; no second editor, project reload or scene overwrite.
- Clock check: RED (missing clock), then GREEN. Handles ten-second delay, thirty-second reading/building delay, progress, interrupted steps, blocked time, disable/reset, reminder backoff and frame spikes.
- Core checks: five existing tutorial regressions passed; 25 stages and 75 distinct localized thoughts passed. A pre-existing edit-mode Destroy call in PhysicalBoardFixture logged errors; make temporary fixture cleanup use DestroyImmediate outside Play.
- First live voice check passed. Existing full-flow verification checked the board handle before its camera blend settled; observed screenshot showed the handle visible after transition. The test now waits for both visibility and completion of the actual Cinemachine blend.
- Review: no-recording subtitle fallback, external audio tail/ownership and rejected Turing submissions were verified as real gaps and addressed. Both DialogAction and ObjectiveAction also register playable clips without subtitle IDs. Targeted review confirmed no remaining findings.
- Live verification additionally waits for the actual hiding exit animation and camera control before looking at Lees; the old check aimed while the camera was still attached to the exit animation. No enemy gameplay code was changed.
- Final Unity Play: TUTORIAL VOICE PLAY PASS, TUTORIAL REAL FLOW PASS and TUTORIAL INNER VOICE FULL PASS. Actual book/archive, physical board/ports/measurement, door/hiding, spotted Lees/survival, rejected Turing submission and valid reader acceptance, pressure threshold/valve exit, single scripted breaker reset and normal-play restoration passed. Unity console: zero errors in the final run.
- Final clock script and scoped git diff --check passed. Changes are left in the existing checkout; no commit/push. New companion speech has localized subtitle content and optional clip bindings; recorded voice assets were not created.

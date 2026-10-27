# Puzzle Workshop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Build the approved Unity puzzle workshop, verified random generator, expanded sample catalog, pool selection and randomized persistent book distribution.

**Architecture:** Serializable puzzle snapshots and one shared signal catalog/simulator drive editor authoring and runtime validation. The existing research scene, books, notebook, board and Turing reader consume those snapshots. A native Unity Editor window edits assets and previews them in a separate workshop scene copied from the existing blockout.

**Tech Stack:** Unity 6000.3.10f1, C#, ScriptableObject, IMGUI, existing uGUI and local Unity bridge. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-27-puzzle-workshop-design.md`

## Global Constraints

- Keep current player/camera/interaction transforms and prior uncommitted work.
- Keep book → notebook → board → measurement → Turing controls.
- Every generated puzzle has a simulated witness; accept equivalent solutions by output and visible rules.
- At most two visible target traces; use existing plot range and proportions.
- Stable puzzle/book identities; continue restores snapshots before board/notebook.
- Workshop/blockout never write the user's normal save file; verify persistence with an isolated file.
- Authoring happens outside Play; native Undo/Redo and asset saves persist.

## Review Focus

- Contradictory/unsatisfiable rules must return bounded failure without replacing the draft.
- Edited/removed assets must not silently change a saved game's target or old measurements.
- Duplicate IDs and unreachable/missing books must fail visibly without silently rerolling.
- Disconnected, unused or cyclic parts cannot satisfy quantity requirements.
- Reopening editor, leaving Play, and starting a second game must not lose drafts or reuse stale readings.

### Task 1: Puzzle model, shared catalog and simulation

**Files:** Create `Assets/Scripts/PuzzleSystems/Workshop/SignalPartLibrary.cs`, `PuzzleDefinitionSO.cs`, `PuzzleSimulation.cs`; modify `Chalkboard/SignalTrial.cs`; tests `Assets/Scripts/Editor/PuzzleWorkshopChecks.cs`.

**Interfaces:** `PuzzleDefinition` serializable snapshot with rules/graph/positions/target/notes; `PuzzleDefinitionSO.definition`; `SignalPartLibrary.Find(string)` and `All`; `PuzzleSimulation.Measure(SynthesisGraphData, PuzzleDefinition)` returns existing `SignalExperiment.Reading`; `Validate(PuzzleDefinition)` returns error text; `PuzzleDefinition.Legacy()` preserves Double Record.

- [x] Add executable developer check using reflection to report missing model/simulation before implementation; run it RED through Unity.
- [x] Implement the model/catalog (existing seven parts plus N-08/N-21/N-46) and bounded DAG simulation, normalization, visible-rule validation, legacy wrapper.
- [x] Run shared checks: legacy witness, new sample transforms, cyclic/unused parts, alternate node order, target tolerances, invalid rule ranges and target overlaps.

### Task 2: Bounded deterministic generation and book content

**Files:** Create `Workshop/PuzzleGenerator.cs`, `PuzzlePoolSO.cs`, `PuzzleBookDistribution.cs`; modify `Chalkboard/SignalTrialContent.cs`.

**Interfaces:** `PuzzleGenerator.TryGenerate(PuzzleDefinition settings, int seed, out PuzzleDefinition result, out string error)`; `PuzzlePoolSO.puzzles`; `PuzzleBookDistribution.Build(PuzzleDefinition,int bookCount,int seed)` returns page lists; `SignalTrialContent.Create(template,puzzle,pages)` uses existing runtime parts/quests.

- [x] Add/run RED checks for generator and complete deterministic book distribution.
- [x] Implement local Random with capped attempts, serial and split paths, rule validation and simulated witness; keep failed result null and input intact.
- [x] Build all required rule/part/observation notes; two blocks per page, optional fixed target book, stable IDs, complete shuffle.
- [x] Run 100+ seeds across three rule sets, contradictory settings, same-seed determinism, coverage and pagination.

### Task 3: Runtime session, measurement identity and save ordering

**Files:** Create `Workshop/PuzzleSessionData.cs`, `PuzzleBookIdentity.cs`; modify `ResearchPuzzleTrial.cs`, `SignalExperiment.cs`, `FormulaCatalog.cs`, `SaveManager.cs`, `GameData.cs`, `ChalkboardManager.cs` and reader reset hook if needed.

**Interfaces:** trial exposes `CurrentPuzzle`, pool/preview fields, `StartNewSession()`, `RestoreSession(GameData)`, `CaptureSession(GameData)`; snapshot contains definition, exact book IDs/pages, completed and measurement records. SaveManager restores session first and exposes an isolated path for checks.

- [x] Add/run RED session snapshot and cross-puzzle measurement checks.
- [x] Install per-session content, clear stale UI/readings, bind pool selection, preserve legacy fallback, save/load snapshots before existing saveables.
- [x] Make board and Turing follow active puzzle ID; persist measurement identity and rejection reasons.
- [x] Check exact JSON roundtrip, asset removal independence, missing book refusal, old saves and repeated New Game.

### Task 4: Shared visuals

**Files:** Modify `UI/QuestDiagramGraphic.cs`, `UI/SignalTrialGraphic.cs`, `Chalkboard/ChalkFormulaSketch.cs`, `Chalkboard/SignalPort.cs` and `SignalTrialContent.OwnsQuest`.

**Interfaces:** visual part data comes from `SignalPartLibrary`; target chart reads immutable puzzle trace data in quest snapshot; book target notes use encoded traces.

- [x] Add/run checks proving novel target data reaches the quest chart and every catalog part's physical port count agrees.
- [x] Render all five samples distinctly and preserve target/measurement axis proportions.
- [x] Check physical book, notebook, board and output visuals using real scene/captures.

### Task 5: Workshop editor, assets and playable scene

**Files:** Create `Assets/Scripts/Editor/PuzzleWorkshopWindow.cs`, `PuzzleWorkshopSetup.cs`; new generated `Assets/Scenes/scn_PuzzleWorkshop.unity`, `Assets/Scripts/Scriptables/Puzzles/*` and metas.

**Interfaces:** menu `Senzora/Puzzle Atölyesi/Aç`; editor asset selection, graph windows with clickable ports, constraints and notes, target/assignment previews; native Undo and Save/Save As; preview asset serialized on trial.

- [x] Add/run asset save/reopen/identity, Undo graph and setup idempotence checks.
- [x] Implement editor window and setup with safe scene saving/copy; create pool with legacy plus three different verified targets.
- [x] Bind pool to the working laboratory; offer explicit packaged-entry setup with validation; workshop uses sessionOnly.
- [x] Generate and inspect the actual saved scene; verify authoring persists after Play.

### Task 6: Integration verification, review and guide

**Files:** Extend `PuzzleWorkshopChecks.cs`; create `docs/PuzzleWorkshop.md`; update relevant current docs and plan/ledger.

- [x] Run existing legacy model/port checks and new full suite through Unity; console compile errors must be zero.
- [x] Exercise real books → notebook → board → measurement → Turing for a new puzzle and legacy; verify isolated persistence and normal save hash.
- [x] Capture workshop and representative game views; inspect readability/layout and unchanged scene transforms.
- [x] Request one independent read-only code review; fix material findings and rerun covering checks.
- [x] Document exact menu/scene, authoring steps, seed/limits, playing random pool, saved-game handling and build-entry setup. Mark task evidence in ledger.

## Execution rulings

### Follow-up execution: color mixing and usability

- [x] Preserve prior verified implementation; finish live integration and final fixes.
- [x] Add `PuzzleKind`, type catalogs and real `ColorMixtureSimulation` with dose conservation, pigment ratios, concentration and volume. Shared generator, rules, physical ports, notebook and Turing follow kind. Add native color targets/measurements and working pool examples.
- [x] Replace dense simultaneous panes with Start / Graph / Rules / Books / Save tabs, full-name part palette, presets, insert-on-edge, auto layout and ordinary project saving.
- [x] Use working copies and persistent recoverable drafts in UserSettings. Test new/open/save-as without silent loss or duplicate assets.
- [x] Run mixture fixtures, equivalent branch solution, 100+ color seeds, full legacy suite, real color book-to-Turing flow and isolated save reload. Complete guide and independent review.

- User explicitly authorized scene and code implementation after approving design; proceed natively without another plan approval round.
- Actual Git repository is nested `Atomb/`; outer `.git` has no valid HEAD. Nested checkout is on `SNZ-003` with substantial user changes, including current required signal scripts. Reuse it to preserve that work and the open Unity session; do not create an unrelated checkout or commit unrelated files.
- Keep a per-task ledger under ignored `Temp/PuzzleWorkshop/progress.md`, because the full initial dirty checkout cannot be represented by a clean branch diff. Record baseline files before editing.

## Final verification

27 October 2026: Unity All Edit Mode suite passed (360 seeded generation cases across both families); real color and signal book-to-notebook-to-board-to-Turing flows passed, including isolated disk save/load, failed Continue protection and missing-scene-book refusal. Authoring working copies, Undo, draft recovery and Save As preflight passed. Final console had no errors. Independent review findings were fixed. Full evidence: UserSettings/PuzzleWorkshop/progress.md (local ignored ledger). Work remains in the current checkout with prior changes preserved.

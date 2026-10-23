# Chalk Sketch Placeholder Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this small change inline.

**Goal:** Board parts appear as flat chalk sketches rather than the existing solid formula models.

**Architecture:** New independent chalk prefabs retain each original prefab's ordered connection rules, while a small `ChalkFormulaSketch` component draws rough planar strokes and an item label. Existing spawning, synthesis and saves continue to use `FormulaItemSO.chalkPrefab`.

**Tech Stack:** Unity 6000.3, LineRenderer, TextMeshPro, existing HDRP Unlit material.

**Spec:** User requested a placeholder chosen by the implementer, no existing 3D prefab appearance; no external model or artwork is required.

## Global Constraints

- Preserve book changes and the user's scene edits.
- No new dependencies, no 3D mesh renderers on the new prefabs.
- Preserve node order, connection types and allowed formula references for saved graphs.
- Keep drawing in the board plane, including while moving and rotating.

## Review Focus

- Prefab node rules and saved node indices remain equivalent.
- Reinitializing a part does not duplicate strokes or labels.
- Sketch generation does not consume the procedural generator's random state.
- Runtime strokes use an HDRP compatible material and cast no shadows.
- Actual board rendering must be checked in Play Mode.

## Task 1: Flat chalk assets and rendering

- [x] Add/run an asset regression check against current data; expect failure for solid prefabs.
- [x] Add `Assets/Scripts/PuzzleSystems/Chalkboard/ChalkFormulaSketch.cs` and initialize it from `DraggableFormula.Initialize`.
- [x] Create `P_ChalkFormulaA/B/C.prefab` and a chalk material; point formula assets at these new prefabs.
- [x] Add `Assets/Scripts/Editor/ChalkSketchValidation.cs` to check flat geometry, labels, node rules, idempotence and random state.
- [x] Run asset checks, compile runtime/editor, then run Unity checks and inspect actual board samples.
- [x] Review the final diff and report artwork requirements for a future polished version.

## Verification results

- Asset regression failed on the old solid prefabs, then passed for all 11 catalog assets.
- Runtime and editor compilation passed; Unity editor validation passed all 11 parts, including two-sided HDRP material, node rules, labels, geometry, idempotence and random state.
- Actual Play Mode: three chalk sketches visibly rendered on the test board; body click and removal worked. Fixed HDRP backface culling discovered during this check.
- Automated dragging could not be confirmed: input tracing showed the automation delivered mouse-down at the drag destination. No dragging success is claimed.
- Independent static review found no actionable issues in the new prefab spawn path.
- Future polish can use transparent chalk PNG artwork; no external model or sketch is needed for this placeholder.

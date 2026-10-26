# Notebook popup and fixed chalk implementation plan

> Execute inline using executing-plans. Preserve the current checkout and all previous puzzle work.

**Goal:** A notebook that folds to an edge button, and bonds/formulas that feel drawn on the board.

**Design:** Use the existing Unity UI and chalk material. The notebook collapses toward a bottom-right button and reopens at its last dragged position; data, tabs and book remain available. Chalk formulas cannot be picked up, rotated or returned to the notebook; erase and redraw them. Original physical formula prefabs retain movement. Chalk bonds lie in the board plane, with stable rough width and dry gaps, including preview/double bonds. No external packages or new image assets.

**Ruling:** Existing dirty checkout is the working artifact. Do not commit unrelated changes or stop a user's Play session. Use isolated additive test scenes; live preview only from Edit mode.

## Tasks
- [x] Add failing checks for collapse/reopen position and retained data; fixed chalk movement; planar, broken, stable bonds and erasure.
- [x] Add NotebookWorkspaceController popup partial; keep edge button reachable while window is hidden and disable hidden hit targets.
- [x] Reuse board-plane chalk rendering for bonds and previews. Guard chalk movement and connected groups; retain graph/save behavior.
- [x] Run editor regression suites and a disposable live preview, inspect screenshots, review, and remove temporary runner.

## Review focus
- Close/reopen during animation, after dragging the notebook and during pause.
- Hidden notebook must not block board input; book source blocks still work.
- Formula node pulse must not make finished bonds wobble or float.
- Double/single bonds, save/load, erase and reopening board preserve fixed placement.
- Previous grouped clue board remains functional; no missing clue solutions displayed.

## Evidence
- RED: Notebook failed for missing edge button; Chalk failed for movable drawings. Other existing suites passed (`Temp/notebook-chalk-red.result`).
- GREEN: Notebook popup and fixed chalk checks; LinkedClueValidation, BoardDocumentsValidation, StarterPuzzleValidation, SecondPracticeValidation, ChalkSketchValidation, and SenzoraWorkspaceValidation.RunAll all passed in Unity Editor. Final result: `Temp/notebook-chalk-edit.result`.
- Live Play preview started only from Edit mode, then stopped its own session. Board interaction, two clue groups and missing slots, wrong-group rejection, fixed double chalk bond, unobscured edge button, collapse and reopening at a dragged position all passed (`Temp/notebook-chalk-live.result`). No disk save written. Open/closed screenshots inspected at 1920×1080.
- Review: independent read-only reviewer found no critical/important issue. Same-frame double toggle could leave raycasts disabled; added regression, observed RED (`Temp/notebook-rapid-toggle-red.result`), fixed, then full editor suite GREEN.
- Additional checks cover preview cancellation preserving the existing bond, geometry remaining on a rotated board plane, actual Update refusing movement/rotation, node pulse stability, single/double erasure and JSON reload.
- Graph transform grouping retained after regression tests showed synthesis still uses the group root; movement is prevented at the shared input/update guard instead. Original physical prefab movement remains available.
- Current checkout retained for the user's Unity preview; no branch merge, push or unrelated commit.

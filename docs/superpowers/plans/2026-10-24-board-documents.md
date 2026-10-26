# Physical board documents and movable notebook

> **For agentic workers:** Use superpowers:executing-plans inline. Keep the existing open Unity project and its uncommitted work.

**Goal:** Attach the active target to the actual left side of the chalkboard, provide notebook/board spaces for collected information, and let both book and notebook move.

**Architecture:** A world-space Canvas belongs to `ChalkboardManager.boardTransform`, with a target sheet above a scrollable information area. Notebook information is an indexed view of existing `RuleClue` blocks across saved pages, preserving their original notes and save format. Pinned board information is independently copied into the board save. Reuse `DraggableWorkspaceItem` for the notebook header.

**Tech Stack:** Existing Unity 6, uGUI, TextMeshPro and C#; no added dependencies.

**Scope:** User's three requested changes in the current scene. The active target remains the selected practice/procedural quest. Book movement is preserved. Generic synthesis feedback still hides answers.

## Tasks

- [x] Add/run a failing editor check for notebook drag handle, information archive, physical canvas ownership and board-information save/copy/reset.
- [x] Enable the notebook header drag and add a `Bilgiler` view to `NotebookWorkspaceController.Sections.cs`/a small information partial. Rule notes remain stored in existing notebook pages; deleting from the information view removes the matching stored note.
- [x] Add `ChalkboardDocuments` in `PuzzleSystems/Chalkboard`, replace the screen target panel, route notebook target/information drops to the physical board areas, and save independent pinned information in `SynthesisBoardSaveData`.
- [x] Run new checks plus starter, second practice and existing workspace/synthesis suites. Play-check real document drops, board attachment, notebook/book movement and persistence; inspect unsolved screenshots.
- [x] Request a fresh review, address important findings, remove temporary driver and update user-facing controls.

## Review focus

- The document area must be on the viewer's left for rotated boards and move with the board, without an extra screen target panel.
- Overlaying the movable notebook must take precedence over a board drop; panels must scroll and not obscure the synthesis work area.
- Information clones must not delete notebook originals, duplicate board pins, or disappear on save/load.
- Long rule text, multiple notebook pages and old saved notes remain usable.
- Pausing and exiting interactions must disable the physical document input; moving the notebook must not move or erase its contents.

## Ledger

- Ruling: continue in the same Unity checkout because the user's current project and previous feature are uncommitted and already open there. A fresh worktree would omit that content.
- Ruling: use an aggregate information view over existing page data instead of migrating notebook saves. Cost: information is available both on its original page and in the dedicated view.
- Initial RED: notebook header lacked a drag component. GREEN: new document checks plus starter, second practice and complete existing suites passed.
- Fresh reviewer found camera-dependent placement after inactive save loading. Regression reproduced the failure. Papers now use the same fixed +Z front as formula objects and the viewer-left +X board edge, independent of roaming camera yaw.
- Reserved the paper strip from formula movement and spawning; preserved saved graph positions during inactive loading and fit whole connected groups when entering the board. The existing rotated-board save regression caught and verified the inactive-load requirement.
- Final GREEN: BoardDocumentsValidation, StarterPuzzleValidation, SecondPracticeValidation and SenzoraWorkspaceValidation passed together. Live pointer checks passed for book/notebook movement without note changes, physical-page collection, physical target and two information drops, independent board save, existing second-puzzle success/wrong-bond behavior, pause and persistent noninteractive documents after exit.
- Visually inspected unsolved `Temp/physical-board-documents.png` and `Temp/movable-book-notebook.png`; captured the former before solving. Permanent usage notes: `docs/BoardDocuments.md`. Removed the temporary driver; retained the permanent validation menu. No deferred review findings.

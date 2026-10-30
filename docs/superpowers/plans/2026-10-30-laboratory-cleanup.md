# Laboratory Cleanup Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. The user approved the audit and requested staged implementation in the current checkout.

**Goal:** Clean the verified laboratory leftovers, correct lifecycle/reference faults, and organize the scene without breaking the current 47-book research, tutorial, settings or Lees gameplay.

**Architecture:** Keep existing components and serialized identities. Use Unity's own scene/asset APIs for cleanup; keep small Editor checks for the scene invariants and real runtime regressions. No new runtime framework or dependencies.

**Tech Stack:** Unity 6000.3.10f1, C#, existing Unity MCP local bridge, Python snapshot utilities.

**Spec:** `docs/LaboratorySynthesisAudit-2026-10-30.md`, approved by the user on 30 October 2026.

## Global Constraints

- Work in the existing SNZ-003 checkout, preserving all pre-existing uncommitted changes. No worktree, commits, resets or branch changes needed for this staged edit.
- Keep 47 research book IDs, three target definitions, authored interaction/player/camera poses, current tutorial choice, Lees resistance, and sessionOnly save protection.
- Back up touched files before editing. Do not overwrite a dirty Editor scene with its disk copy.
- Reversible scene/file cleanup is authorized by the user. Shared assets with other scene users stay.
- First complete a verified cleanup stage; do not claim FPS or memory gains without measurements.

## Review Focus

- Persistent manager duplicates must not refresh state from a soon-to-be-destroyed duplicate or retain stale scene references.
- New settings prefab must remain functional after old children and obsolete serialized fields are removed.
- Reparenting must retain authored world poses, component fileIDs and ContentWorkbook GlobalObjectIds.
- Old inactive board/book instances may still appear in lists; clean references before deletion.
- Opening Play must not modify the normal save file, user settings or the current scene's saved runtime state.

## Task 1: Establish the real Editor baseline and reusable scene checks

**Files:** create `Assets/Scripts/Editor/LaboratoryCleanupChecks.cs`; create backup/evidence in `outputs/laboratory-cleanup-2026-10-30/`.

**Interfaces:** scene checks consume the existing ResearchPuzzleTrial, SettingsUIManager, FormulaCatalog and save protection. Menu commands write JSON results outside Assets.

- [x] Inspect active Editor scene, playing/dirty state and compile errors through the existing local bridge.
- [x] Capture baseline object GlobalObjectIds, world matrices, important bindings, missing scripts and scene dependency data; back up touched sources/assets.
- [x] Add checks for one catalog and settings prefab without obsolete UI; observe their expected failures before cleanup. Low-impact dead-file deletion needs dependency validation, not artificial tests.

## Task 2: Remove verified dead assets and scene leftovers

**Files:** target scene; the five unused scripts and four PasswordData assets from the audit; existing settings adapters remain.

- [x] Recheck asset and code users; remove five unused scripts and four unused SOs with their metas after backup.
- [x] Use Unity scene APIs to retain one FormulaCatalog, remove the five extra roots, and remove old settings children while preserving the two adapters and new prefab instances.
- [x] Remove the two missing-script remnants only after identifying them in the loaded scene. Clear stale custom reflection reference; investigate occlusion data through the Editor rather than inventing a bake result.
- [x] Re-run scene invariants, compile, real settings checks and current 47-book/model checks. Record exact results.

## Task 3: Correct manager lifecycle and material ownership

**Files:** existing manager and book/highlighter scripts only where needed; `LaboratoryCleanupChecks.cs` for real regression scenarios.

- [x] Reproduce manager root/duplicate failures and material/mesh lifetime behavior with real runtime checks before fixing.
- [x] Decide scene-local versus persistent behavior from actual callers. Make the smallest change; preserve scene reference refresh and stop duplicate Awake execution.
- [x] Give owned runtime materials/meshes explicit cleanup; avoid modifying shared asset materials or compromising book presentation. Verify repeat spawn/destroy, open/close and scene-reload behavior.
- [x] Run relevant Play/regression checks; compare resource counts when available. Defer unmeasured Animator/GPU changes.

## Task 4: Organize hierarchy while preserving references

**Files:** target scene; existing Editor tools with exact path assumptions; audit implementation progress.

- [x] Group scene systems, cameras and audio incrementally; retain player/rig and machine/book child structures. Keep genuinely persistent managers at roots.
- [x] Update obsolete four-book validation to current session validation; use stable identities/relative structure instead of original absolute root paths when checking preserved poses.
- [ ] Verify GlobalObjectIds, world poses, bindings, 47 books, 3 goals, tutorial yes/no, settings, board/Turing and Lees integration. Preserve conditional legacy books/formulas until their references are safely resolved.
- [x] Record stage results and remaining occlusion/Profiler work, review the full scoped diff, and report the concrete stage completed.

## Execution ledger

- Initial ruling: user approval of the audit includes staged execution; do not ask for plan approval again.
- Initial ruling: reuse this dirty Unity checkout and current feature branch; a fresh worktree would omit the actual scene and untracked gameplay work being cleaned.
- Initial ruling: use existing Editor integration checks and a few behavioral regressions; do not add a test suite for simple asset deletion or mirror source text.

- Task 1: complete. Live dirty Editor scene backed up before edits; baseline 3193 objects/47 books. Scene check RED with six catalogs, 169 obsolete settings descendants, two missing scripts.
- Task 2: complete. Eighteen verified dead files removed; scene GREEN with one catalog, zero obsolete settings descendants/missing scripts. Book projection 2450 points, settings data/UI, 48 book distributions and LEES integration 112 assertions PASS.
- Task 3: complete. Manager and book lifetime regressions RED→GREEN. UniversalHighlighter RED→GREEN; legacy book focus PASS. Extended DisableEmission twice/destroy fixture: 11 resources destroyed, all source materials survive.
- Task 4: hierarchy and current validation implemented. 3022 surviving/new objects, 47 IDs/order/pool/poses/tutorial/sessionOnly verified; no missing GUID/local refs. Tutorial YES24/NO25 assertions and actual laboratory main/pause settings PASS.
- Task 4: outstanding verification: full English 47-book/three-target Play flow stops after 20+ books at a TMP heading overflow (Record C-43 / EXPERIMENT CONDITIONS). Final board/Turing completion is NOT certified by that run. Track in docs/LaboratorySynthesisCleanup-2026-10-30.md; do not hide it or mark the full flow green.
- Final review: fresh read-only gpt-6-astra reviewer; no confirmed Critical/Important/Minor production findings. Reviewer identified fixture coverage gap; repeated DisableEmission and borrowed renderer material protection now exercised and PASS.
- Final: Ruling: FPS/GPU/memory gains remain unmeasured — native occlusion data was missing, clear its stale pointer and defer a new bake/Profiler comparison — cost if wrong: rendering cost may still dominate.
- Final: Ruling: unchanged legacy behavior outside cleanup was not reviewed wholesale — preserve it and report the English heading overflow as open follow-up — cost if wrong: other existing flows may contain defects beyond this scoped review.

- Final verification: real settings Play PASS (2026-10-30 15:44 local); returned to saved scn_LaboratorySynthesis in Edit Mode, 13 roots, clean scene, not compiling. User settings SHA-256 unchanged; normal gameplay save remains absent. Keep files in place; no commit/merge/push. Full English content-flow check remains open as recorded above.

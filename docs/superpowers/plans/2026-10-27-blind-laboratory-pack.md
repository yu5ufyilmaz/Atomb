# Blind laboratory pack implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement inline. User explicitly requested preparing this playtest; preserve the dirty shared checkout.

**Goal:** Three unknown puzzles playable in any order in scn_LaboratorySynthesis, with one target in the first book, the other targets in different books and clues scattered across all books.

**Architecture:** Reuse the existing puzzle pool asset as an explicit simultaneous set, leaving random single-puzzle pools unchanged. One immutable session owns the set, combined catalog and exact book pages. Pinning a target chooses its simulator and fingerprint; measurements and completion remain scoped to their own puzzle. Existing single-puzzle snapshots remain supported.

**Tech stack:** Unity6000.3, existing C# ScriptableObjects, IMGUI authoring helpers, local Unity MCP bridge.

**Spec:** User request and clarification in this chat; docs/PuzzlePlayerExperience.md provides the accepted qualitative design direction.

## Constraints and review focus

- Do not reveal solution graphs, part locations or other targets to the player outside play.
- Preserve existing scenes, unrelated assets, draft files and normal persistent save.
- Three distinct valid targets, complete clue coverage, distinct target books and <=2 notes per page.
- Switching targets must preserve other measurements/completion and reject cross-target submissions.
- Save and restore the whole set and exact pages; old single-target snapshots continue to work.

### Task1: Session and distribution

- [x] Write/run a failing combined-book model check in Assets/Scripts/Editor/LaboratoryBlindPackChecks.cs.
- [x] Add list overloads to PuzzleBookDistribution.Build and SignalTrialContent.Create; preserve single-puzzle overload behavior.
- [x] Extend ResearchPuzzleTrial and PuzzleSessionData with simultaneous-set ownership and snapshot support; select current definition on QuestPinned without resetting the session.
- [x] Scope Turing acceptance by pinned target and retain measurements across target changes.

### Task2: Three authored puzzles and scene binding

- [x] Add an editor setup command to create three validated, neutral-named definitions in a separate asset folder and a dedicated set; configure the existing laboratory without replacing other scene content.
- [x] Use short observation notes and catalog choices; target graphics carry the goal, no solution links in player notes.
- [x] Validate complete all-book coverage and target placement across seeds.

### Task3: Verification and handoff

- [x] Run model and existing Edit Mode checks.
- [x] In Play verify actual books, mixed catalog, target pinning, three real simulations/submissions, wrong-target refusal and full snapshot restore. Check normal save untouched.
- [x] Stop Play, leave laboratory scene open with no solved/collected runtime state. Give spoiler-free play instructions.

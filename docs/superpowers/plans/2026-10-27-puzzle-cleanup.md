# Puzzle cleanup implementation plan

> **For agentic workers:** Execute inline using superpowers:executing-plans. The user has authorized cleanup; preserve the current playable experiment.

**Goal:** Remove retired book/blackboard puzzle content without breaking the current four-book SignalTrial flow.

**Architecture:** Keep shared book, notebook, chalk graph and Turing interaction infrastructure. Remove retired symbol-overlay puzzles, old research content and setup tools only after detaching serialized references through Unity.

**Tech Stack:** Unity 6000.3.10f1, C#, serialized scenes/prefabs/ScriptableObjects.

**Spec:** User request in this chat; current flow documented in `docs/LaboratorySynthesis.md`.

## Global constraints

- Preserve authored player, camera, machine and book transforms and animations.
- Preserve the current session-only save guard and existing build/menu routing.
- Delete asset `.meta` files together with retired assets; preserve GUIDs of retained assets.

## Review focus

- A retained book must survive deletion of its retired puzzle prefab.
- No scene/prefab may acquire a missing script or removed-asset reference.
- Collecting notes, directed ports, measuring and submitting in Turing must still work.
- Old puzzle selection/setup commands must not restore deleted content.
- Existing unrelated scene defects must be distinguished from cleanup regressions.

### Task 1: Record dependencies and detach retired content

- [x] Record existing missing references and run SignalTrial validation.
- [x] Unpack only retired prefab instances, remove their obsolete components and clear retired SO references using Unity.
- [x] Verify retained laboratory books, player and machines remain bound.

### Task 2: Remove retired content and code

- [x] Delete symbol puzzle assets/tools and old starter/practice/A-B/second-trace content.
- [x] Make the experiment use SignalTrial exclusively while retaining serialized component GUIDs.
- [x] Update current validators and documentation; remove obsolete setup/test commands.

### Task 3: Verify

- [x] Compile in Unity and inspect errors.
- [x] Compare missing-reference inventory against baseline.
- [x] Run SignalTrial model checks and laboratory scene/play checks.
- [x] Review diff and report any validation limits.

Ruling: old SymbolDataSO research clues, notebook symbol tab and UnlockSymbolResearch event actions are part of the retired symbol puzzle. Remove those actions through Unity before deleting their data/type; preserve all other event actions and conditions.

Ruling: current Turing camera poses differ from the historical source scene before cleanup. Compare against the actual pre-cleanup transform inventory; do not overwrite authored poses to satisfy the old source-copy test.

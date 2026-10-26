# Laboratory synthesis migration

Goal: move the current four-book experiment into a copy of scn_labratory, keeping the real player and authored machine/board camera, sitting and standing transforms.

Design: reuse the research notebook, book content and experiment code. Enable that content explicitly in the copied scene; retain the original scene and test scene. Walking notebook uses screen UI, rises from the lower left, keeps time running and the pointer locked, and supports Tab, Q/E and wheel. Workstations retain their interactive cursor and drag/drop UI. Disable hand-model notebook presentation only in the migration copy. Reuse existing laboratory book locations and existing Turing machine rather than importing the blockout player or room.

The user's requested scene copy is the isolation boundary. No extra worktree or prefab edits are required.

- [x] Inspect original Lab UI, books, board and machine references; run notebook behavior check before changing it.
- [x] Add explicit gameplay-scene initialization and screen notebook configuration with original defaults retained elsewhere.
- [x] Generate scn_LaboratorySynthesis from the original laboratory; migrate content and visuals through Unity serialization.
- [x] Compile in Unity; validate original scene unchanged, machine transforms/references preserved and live notebook input; exercise book/board/terminal flow.

Review focus: notebook open during machine approach; cursor restoration after pause; no physical notebook animation event reactivation; original book camera placement; copied scene save/reopen and missing scene references.

Verified in Unity: source scene comparison; all four real books and physical page cards; archive and board experiments; measurement output collection; actual Turing paper feed and code; exit cursor restoration; live walking notebook and animation event guard. Focused independent review fixes: title-screen notebook guard, Turing exit cursor restoration, and session-only saving for the intermediate scene, whose measurement state is not yet persisted.

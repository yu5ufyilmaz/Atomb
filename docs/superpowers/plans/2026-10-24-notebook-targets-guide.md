# Notebook target collection and synthesis cheat sheet

Goal: make target collection an explicit drop destination and add a reversible, read-only synthesis guide inside the notebook.

Approach: a labelled Toplanan Hedefler drop area above the notes, with Notes / Targets / Guide views below it. Targets are a global notebook collection saved separately; migrate old page targets without losing content. Only target notes enter this area; ordinary notes retain their drop position. The guide reads actual practice, current procedural-run and configured legacy recipe data, reveals copied diagrams and lists parts, bond types, erase operations, constraints and output. Clearly mark recipes unavailable in the current board mode. No synthesis, unlocking, random generation or inventory changes from viewing the guide.

- [x] Add and run a failing Unity check for the explicit target destination.
- [x] Add target save/migration and separate collection UI/drop handling; preserve board transfer and deletion.
- [x] Build recipe guide data and a toggleable notebook guide view; include existing recipe assets.
- [x] Run migration, drops, guide content/toggle, existing suites and a Play loop; inspect rendered views.
- [x] Fresh final source review, remove temporary runner and document controls.

Review focus: legacy save migration is idempotent and clones data; wrong-zone target drops reject with a readable explanation; guides never reveal unknowns on the original quest; hiding/reopening preserves page and drawings; future procedural stages and repeated ingredients are represented accurately.

Verification: the explicit target-area check failed on the previous UI, then the full editor suite passed with the new collection. Migration/JSON independence, duplicate recipe handling, repeated quantities, revealed copies, unchanged quest/random state and all five future procedural stages passed. Fresh source review found one bookmark view-routing issue; its regression check failed and then passed after page navigation was made to select Notes. Initial Play guide capture/read-only toggle passed, but target-to-board transfer exposed an old cached-pointer check; drops now raycast their actual destination instead. Final Play returned PLAY PASS at 03:29 on 2026-10-24: wrong-zone/type drops, duplicate target collection, guide content and preserved drawing, target-to-board pinning, notes-to-board copies and complete non-consuming puzzle success passed. Rendered collection, guide and solved board inspected. Temporary integration runner/meta removed and generated fallback font atlas restored. No new scene changes; existing diff remains the single starter reference.

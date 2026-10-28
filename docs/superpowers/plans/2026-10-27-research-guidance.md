# Research playtest improvements

User approved implementing the discussed fixes on 2026-10-27. Preserve the three unknown laboratory goals and existing authored assets.

1. Reproduce the serialized B-29 target rendering error; carry an explicit puzzle family through the quest renderer.
2. Give books persistent meaningful titles, subjects and shelf locations. Distribute authored/generated goals through the same constrained random slot allocator. Deduplicate common observations; do not manufacture duplicate clues to fill shelves.
3. Derive goal research directions from the completed placement (book identity, title, page), and preserve these exact references in session saves. Directions reveal sources, never the solution order.
4. Fix physical page projection/slot bounds and readable goal directions; improve notebook/document controls, part roles and floating paper appearance in isolated UI work.
5. Run model regressions, actual serialized target rendering, physical page and real drag checks, then the three-goal end-to-end playtest. Save the laboratory ready for a fresh unsolved playtest.

Verification covers repeatable seeds, different seeds, topic eligibility, capacity failure without partial installation, unique clues, accurate post-placement source links, generated and authored puzzles, save/restore, and both experiment renderers. Root alone controls the shared Unity editor; independent UI/page fixes are delegated under the dispatching-parallel-agents skill.

Completed: 360 existing generator/model seeds;60 authored distributions and40 generated targets;24 overlapping-topic capacity regressions; serialized B29 rendering; exposed/curved geometry, assigned/legacy slots and page turns; actual UI drags/controls/roles and three goal source texts. Full real-menu playtest opened47books, solved3goals out of order through the real board/Turing, rejected other-target results, restored the exact snapshot and preserved the normal save. Live opening/turning/closing screenshots inspected. Play stopped; laboratory saved for a fresh unsolved trial.

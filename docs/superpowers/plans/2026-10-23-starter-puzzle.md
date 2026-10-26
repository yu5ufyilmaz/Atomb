# Başlatıcı-X Practice Puzzle Implementation Plan

**Goal:** One complete collect → notebook → board → erase/connect → test loop, ending with “Doğru bildin”.

**Architecture:** Reuse the existing notebook blocks, physical book cards, quest blueprint, graph validator and formula nodes. An authored practice quest on the blockout scene supplies five candidate assets and two readable rule clues. Practice validation never consumes pieces or generates an output.

**Constraints:** Preserve book UV settings and scene transforms. No new dependencies. Do not change the procedural campaign. The final example is authoritative: BE-9 — U-235 ═ REF; the earlier MOD bridge rule is not part of this puzzle.

**Review focus:** Unknown slots must not accept wrong candidates; blocked ends must really start blocked; the central capacity must allow one single plus one double; notebook controls must not block board input; repeated success must leave the board usable.

- [x] Add a runnable editor check that fails because the authored starter content is missing.
- [x] Author five candidate assets and one quest, with capacity/blocked-end/rule data. Wire the current open blockout scene without rebuilding it.
- [x] Add practice-only validation through the existing manager; require pinning the collected quest and report success without consuming anything.
- [x] Populate the physical book with distinct quest, part and rule cards. Reuse existing copying and quest stacking. Keep the original UV data asset intact.
- [x] Make the quest diagram use authored positions and show numbered unknowns; add board tools and a visible test button.
- [x] Run positive/negative graph checks, real node erase/double-bond checks, UI transfer checks, compilation, and actual Play Mode visual checks.
- [x] Independent final review; fix material findings and document verification.

**Verification:** Unity menu `Senzora/Doğrulama/Başlatıcı-X Döngüsü`; existing synthesis/workspace validation; runtime/editor C# compilation. Play the current scene, inspect both note types and board controls, exercise the same drop/erase/connect/test paths.

2026-10-24 resumed verification: Unity editor suite passed (including 64 procedural seeds / 320 stages), and Play integration passed note collection, target pinning, port erasure, single/double connections and non-consuming success. Rendered book and board frames inspected. Corrected imported YAML references, inactive TMP initialization, practice camera bloom and connection ribbon alignment. Scene diff is only the starter quest reference. Final reviewer findings about scene wiring and practice-only captions were fixed. The follow-up direct-mouse/page changes are tracked in the 2026-10-24 plan.

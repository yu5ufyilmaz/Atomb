# Bond clarity and removal of socket unlocking

Goal: identify double-bond sockets before connecting, preserve existing bond rendering while drawing an upgrade, and remove the blocked/unstable-end erasure step.

Approach: a separate temporary preview LineRenderer, with permanent single/double lines always rendered. Formula data explicitly marks double-bond sockets; REF's card, part label and socket indicate its requirement and 0/2 → 1/2 → 2/2 progress. Nodes initialize and restore open, including legacy data; no X overlay, new blocked-end generation or erase-to-open instructions. Right-click still corrects actual bonds and freehand strokes.

- [x] Reproduce the overwritten permanent-line preview in an engine regression test.
- [x] Separate preview/permanent lines and add visible double-bond socket hints.
- [x] Remove runtime socket unlocking, update starter/generated content and legacy-save normalization.
- [x] Update guide, notes and validation; run full suites and Play/render checks.
- [x] Fresh source review; cleanup temporary runner and document verification.

Review focus: reverse-direction upgrades, invalid/cancelled drag preservation, no third bond, shared capacity limits, legacy closed nodes do not block a loaded puzzle, hints agree with actual bond state.

Verification: old renderer reuse failed the real Unity regression check. Full EDIT PASS after separating preview, covering permanent single/double visibility during preview and cancellation, reverse upgrade ownership, third-bond rejection, capacity limits, 0/2→1/2→2/2 hints and legacy blocked-state load normalization. Existing starter, 64-seed procedural and notebook suites passed. Fresh source review found no important correctness issues; obsolete documentation was updated. PLAY PASS at 03:49 on 2026-10-24: collected updated book notes, transferred target and pieces, connected all sockets without erasure, completed marked double bond and displayed non-consuming success. Inspected actual book/board renders: REF card and part label say ÇİFT BAĞ; sockets show gold =2/2, and both permanent lines are visible. Temporary runner/meta removed; generated font atlas restored. Scene still only differs by starter quest reference. User's existing solution-file edit was preserved.

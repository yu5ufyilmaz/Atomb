# Direct drawing and automatic notebook pages

Goal: left mouse draws, right mouse erases without tool buttons. Notebook notes stay where collected; dragging copies to the board still works. Next-page navigation creates blank pages as needed, and a scrollable side strip bookmarks only occupied pages. Board supports free chalk strokes alongside formula connections. Books support page-specific annotations.

Approach: reuse NotebookFreehandDrawer geometry and erase logic for all three surfaces; preserve authored book UV mapping and puzzle validation. Store board strokes with the existing board snapshot. Book annotations persist for the book's runtime lifetime, by page; cross-session book annotation saving is outside this pass.

- [x] Extend real Unity editor checks for mouse buttons, page 20 and occupied-page bookmarks.
- [x] Implement direct mouse input; remove notebook/board tool buttons and notebook repositioning.
- [x] Implement automatic page navigation and scrollable occupied-page tabs.
- [x] Reuse the drawer on board and physical book pages; preserve formula input priority.
- [x] Run editor checks and Play Mode loop, inspect screenshots, remove temporary integration runner.

Review focus: right-click never starts a note drag; empty pages are not bookmarked; erasing the last stroke removes its tab; notebook-to-board drag does not draw on the board; formula ports retain connect/erase; pause and page-turn end active strokes.

Verification ledger: right-mouse erasure check first failed on the old behavior, then passed after shared mouse input was added. Full editor suite passed automatic page 20, occupied/empty tabs and board stroke JSON save/load/reset. Book UV checks (curvature, spine, nonuniform scales) and all 16 chalk prefab checks passed. Final Play integration returned PLAY LOOP PASS at 03:03 on 2026-10-24, including real EventSystem hits for blank book/notebook drawing areas, book mouse UV draw/erase, notebook collection/copying, board free strokes and unchanged non-consuming puzzle success. Actual rendered frames show page 20 with tabs 1/2/20 and both free drawings. A fresh source reviewer found no important correctness findings. Temporary integration runner and its meta were removed; generated TMP fallback atlas changes were restored. Scene remains a one-line starter quest reference change.

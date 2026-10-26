# Second blind practice puzzle

Goal: preserve the first puzzle and add a second authored puzzle of comparable complexity without revealing its answer in chat.

Approach: two practice blueprints in the same book, each with three diagram nodes, two unknowns, one single and one double bond, five candidates and two short clues. A collected target selects the matching practice blueprint when pinned. Share the existing candidate catalog and validation; preserve non-consuming success. The guide remains opt-in. Quest descriptions come from authored summaries instead of starter-specific hardcoded text.

- [x] Add a failing asset/content check for the second puzzle.
- [x] Author new fictional candidate/quest assets and wire additional book spreads.
- [x] Support selecting either collected target, correct pinned hints and guide entries.
- [x] Verify unique solution, wrong candidates/bond, switching, save/load, and existing suites.
- [x] Play-check clue collection and synthesis; do not expose solved screenshots or solution in final output. Cleanup temporary runner.

Review focus: original content stays available, page spreads do not overlap, wrong old/new parts fail, hidden identities stay hidden outside the opt-in guide, and loading a second-puzzle save resolves its target.

## Completion ledger (2026-10-24)

- Resumed the existing implementation; no new puzzle design was substituted.
- RED: `SecondPracticeValidation.Run` failed because the second quest replaced the starter in the scene and the additional list was empty.
- Repaired the two scene references using Undo in the loaded scene. Saved a pre-change copy to `Temp/SecondPractice-before.unity`; no scene reload or scene rebuild was needed.
- GREEN: second practice, starter practice, synthesis puzzle, synthesis board and notebook workspace checks passed in the open Unity editor.
- Fresh reviewer found a hidden-answer leak through detailed rule failures with mixed-puzzle candidates. Added a regression; observed the expected RED, then changed practice failure feedback to a generic clue reminder. The regression and complete existing suites passed.
- Live check passed: real physical-page raycast and drag handlers, note/target collection, notebook-to-board transfer, second-target selection, single-versus-double validation, successful non-consuming synthesis.
- Moved only the test book's opening offset so both pages are visible beside the notebook. Re-ran the full editor suite and physical book/board Play check with this placement; both passed. The test driver waits for the newly selected notebook page to render before injecting drag input, matching human interaction timing.
- Removed the temporary editor driver. Retained the permanent content/regression menu and live board validation command. Unsolved preview: `Temp/second-practice-book.png`; test records: `Temp/second-practice-edit.result` and `Temp/second-practice-play.result`.
- Ruling: practice validation uses generic mismatch feedback so hidden candidate identities remain discoverable from book clues; detailed topology/rule errors remain available to code-level validators. Cost: less specific feedback on a wrong practice graph.

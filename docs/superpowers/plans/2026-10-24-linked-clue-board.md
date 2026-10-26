# Linked clue pinboard

> Use superpowers:executing-plans inline in the existing open Unity checkout.

Goal: identify which puzzle owns each information card and show missing knowledge as empty slots, with target-to-clue strings on a cork backing.

- [ ] RED: authored rule cards carry questID/ruleIndex; legacy descriptions resolve uniquely; mixed-puzzle pins fail; switching targets preserves each group's notes and missing slots.
- [ ] Add a small shared clue-association helper using the existing questID and ruleIndex save fields. Recover old unlabeled notes only for unique text matches; keep ambiguous notes unassigned.
- [ ] Group notebook information by collected target, show empty clue slots, label normal-page rule cards, and focus the selected board puzzle when opening information.
- [ ] Render the active physical target and its numbered clue slots on a cork strip with red strings. Show found/expected count and reject information belonging to another target.
- [ ] Run new and existing editor suites; Play-check two puzzles, one missing clue, wrong-group rejection, switching and save/load; inspect unsolved screenshots.
- [ ] Fresh review, fixes, remove temporary runner, update usage notes.

Constraints: preserve movable documents, physical mounting, board work area and hidden formula identities. Empty slots reveal count/position only, not missing clue content. Existing uncommitted work stays in place.

Ruling: the physical strip shows the selected puzzle; the notebook keeps all puzzle groups. Selecting another target changes the strip without deleting its stored information.

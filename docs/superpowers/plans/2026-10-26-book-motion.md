# Natural Book Motion Implementation Plan

**Goal:** Give ordinary books a continuous pickup/open/return motion and flexible page turns with printed content attached throughout.

**Design:** Approved in chat on 26 October 2026: improve the existing rig and materials, use an arced pickup with delayed rotation and overlapping cover opening, retain ink on turning pages, add restrained settling, moving shadows and synchronized existing sounds. No new dependencies or replacement book assets.

**Architecture:** Keep interaction ownership in InteractableBook. Extend the existing UV-mapped BookPageBlockSurface for animated presentation, with noninteractive outgoing/incoming page layers during turns. Share the page curl calculation between the material vertex stage and printed geometry. Preserve current notebook collection and unrelated working changes.

**Tech Stack:** Unity 6000.3.10f1, C#, uGUI/TMP, HDRP Shader Graph.

## Tasks
- [ ] Add editor checks for continuous printed presentation, forward/backward page mapping, curl endpoints, interrupted/paused transitions and restored poses.
- [ ] Replace sequential pickup/open and close/return waits with bounded overlapping motion; synchronize existing audio to motion.
- [ ] Curl the page with matching normals/shadows and carry the visible printed content across both faces, retaining underlying page contents.
- [ ] Run Unity compilation and book checks; verify representative animation frames and notebook interaction; review the focused diff.

## Review Focus
- Both turn directions and first/last/loop page boundaries.
- No disappearing stationary ink or duplicate interactable drag targets during motion.
- Opening an already-open book; no extra cover animation.
- Pause/exit during motion, stable final transform, no coroutine hangs.
- Existing UV projection under nonuniform scales and existing notebook collection.

## Execution notes
Implement in the user's existing Unity checkout: it contains the current, uncommitted integrated content needed by this change. No branch reset, unrelated staging or automatic commits.

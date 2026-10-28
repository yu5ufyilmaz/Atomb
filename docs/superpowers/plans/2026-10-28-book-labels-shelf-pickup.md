# Book labels and shelf pickup implementation plan

**Goal:** Legible authored book titles, shelf signs outside the shelf face, and closed books sliding straight clear of their shelf before rotating/opening toward the player.

**Architecture:** Keep the existing TMP label generator and bone attachments. Use the project's Special Elite font with explicit spacing, padding and controlled wrapping. Derive shelf-front placement and extraction from actual shelf/books geometry rather than the initial camera. Extend the existing book coroutine with an initial translation-only phase; preserve pause, input ownership, page presentation and exact return pose. Keep the extraction distance/time configurable in the Inspector. No new runtime library or per-frame physics search.

**Tech stack:** Existing Unity6000/TMP/HDRP/C# editor menus and runtime coroutines.

The user's request is the spec. Preserve their currently dirty scene and all earlier work. Save a scene-copy checkpoint before label application; apply only labels and pickup settings, without rerunning puzzle setup or changing identities/distribution.

- [x] Inspect actual shelf, book and label geometry; capture baseline label evidence and a failing early-pickup check.
- [x] Restyle existing book cover/spine labels; place each shelf sign beyond the actual front surface and use readable font sizing in TMP units. Check all47book titles/Turkish glyphs and all6shelf signs, then inspect representative screenshots.
- [x] Add straight, closed-book shelf extraction before approach rotation/opening; use the inverse phase when returning to the same slot. Desk books retain normal pickup. Test middle/end books, a differently oriented shelf, pause and exact return pose.
- [x] Run actual Play movement/readability checks, verify compilation and scene changes, preserve evidence outside Assets, save the requested scene changes and report results.

Validation: labels/font check first failed on the default font; old live movement failed with "kitap raftan kurtulmadan dönmeye başladı". After implementation all47cover/spinepairs and6signs passed font/Turkish/full-title/physical-front/text-size checks. Actual edge/middle/rotated-shelf pickup passed closed straight extraction, full-depth clearance, pause, readable content and exact return. Existing natural-book live check passed forward/back pages, pause, collection, queued exit and home restoration. Error console empty. Read-only independent final review found no actionable correctness regression.

Scene was already dirty when work began. Original scene-copy backup and timestamped copies live under UserSettings/PuzzleWorkshop; no puzzle setup is rerun. Signs use SM_BookCase geometry and book spine direction, never the initial camera. Font SpecialElite-Regular supports all Turkish letters; its atlas is populated/saved at authoring time. Inspector offers shelfPullDuration(.28s), shelfPullClearance(.08m) and shelfPullDirection relative to shelf parent. Evidence images are outside Assets.

Final: scene saved at Assets/Scenes/scn_LaboratorySynthesis.unity; isDirty=false, rootCount21 preserved. All6sign images and book-spine view inspected. Captures use a1.2m aisle view so the diagnostic camera does not enter the opposite cabinet. Font source asset saved with authoring glyphs. Code/docs diff whitespace check passed; Unity's native YAML empty-value spacing is left intact. Final error console empty.

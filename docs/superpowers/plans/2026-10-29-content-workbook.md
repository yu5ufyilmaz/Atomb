# Content Workbook Implementation Plan

> Implement inline using executing-plans. User approved the linked design and execution on 2026-10-29. Preserve this checkout's existing edits. No commit/push requested.

**Goal:** One populated Excel master for texts, sequential subtitles, scene dialogue assignments and real input bindings with live hints.

**Architecture:** Editor-only OOXML reader/writer adapter validates a staged import before changing existing JSON, SubtitleData, DialogAction fields and InputActionAsset. Runtime reuses existing localization/event systems and shares input action codes with hints.

**Tech Stack:** Unity/C#, System.IO.Compression/XML, existing Input System; bundled artifact-tool for initial XLSX authoring.

**Spec:** ../specs/2026-10-29-content-workbook-design.md

## Global Constraints
- Keep tutorial event conditions, camera ownership and dialogue history intact.
- Authoring workbook is not read at runtime; no new runtime package.
- Keep existing identifiers and saved overrides stable. User edits outside scope remain untouched.
- No new controls settings screen in this delivery.

## Review Focus
- Invalid or partial imports must not modify project data.
- Excel row sorting, multiline cells and missing trailing cells must be handled.
- Live hints must match actual effective bindings, including persisted overrides.
- Scene connection refresh must preserve pending workbook edits and dirty scenes.
- Reordering subtitle segments must update JSON keys and durations together.

## Tasks

- [x] 1. Write and run failing OOXML/validation checks; implement strict Editor-only workbook model, schema and XML handling. Test duplicate codes, sequence/duration, formula cells, missing links, placeholder mismatches and round trip.
- [x] 2. Implement Unity export, preview/validate/apply, scoped dialogue connections and input binding export. Stage all changes and roll back on write failure. Add workbook fingerprint/build stale guard and preserve pending content on connection refresh.
- [x] 3. Add shared action access, persisted rebind/reset API and localized input tokens. Migrate actual gameplay controls/hints and event input conditions. Verify changed key affects both behavior and text.
- [x] 4. Export current project data, author populated formatted XLSX with artifact-tool, render all six tabs and verify round trip/no losses. Include a readable guide in workbook and docs.
- [x] 5. Run editor/runtime build, workbook regressions and existing localization/tutorial checks. Independent final code review, address material findings, report exact evidence and limitations.

## Ledger

- Design independently reviewed: no blocking questions. Human approval received.
- Existing branch SNZ-003 reused because Unity is open against this checkout and contains user changes; no isolation that would omit them. No Git mutations.
- OOXML and input-token checks observed red, then green. Initial formatted workbook authored with artifact-tool and all six tabs visually reviewed.
- Snapshot: 685 general texts, 98 dialogues, 149 segments, 86 consumer links, 41 bindings. Initial data has 29 warnings (missing translations / optional German token mismatch / existing missing read-only megaphone IDs). These are surfaced, not silently translated.
- Ruling: keep old canonical [F]/[Tab] source aliases, tokenize displayed translations. English defines input-token identity when the canonical source predates tokens. This preserves existing callers.
- Review-driven fixes in progress: reject dirty database/prefab stages and missing old text keys; write prefab connections through the same persistent assets used by export; restore database memory and explicitly reload affected clean scenes on transaction rollback; validate binding value types.
- Review fixes completed. Final independent review confirmed audio-clip priority, untyped Sprint value-type checking and malformed composite-format validation resolved; no remaining material findings.
- Editor-native input state cannot be asserted outside Play: configuration/live-text/persistence checks run in edit mode, input-edge/real-consumer checks run in Play mode.
- One fault-injection attempt locked a scene during native SaveScene, which terminated Unity. Restored all test changes from master workbook (TR first text and duration3 checked). Changed strategy: managed write-lock preflight before any mutation, late verifier rejection inside rollback boundary. Core now confirms both safe lock rejection and restoration of disk/database/scene memory. No fixture scene left.
- Unity core PASS 2026-10-29 07:16:11, 29 content warnings surfaced. Play PASS 07:19:42: G closes actual book and old F does not; input pressed/held/released, saved override/default reset, normal/conditional/reminder audio priority, subtitle order/durations/live language, and prior tutorial regressions.
- Final Editor/runtime C# build: 20 warnings, 0 errors (final UI report sizing included). Existing offline localization (834 entries) and tutorial clock checks passed. Final semantic import stamp regenerated; CONTENT SYNC PASS confirms workbook/data/connections agree. No temporary test scene remains.
- Delivery: Content/GameContent.xlsx (one master), docs/ContentWorkbook.md, Senzora > İçerik Tablosu > Aç. 29 existing authoring warnings remain visible rather than being silently invented/translated. No controls settings UI was added, as approved.

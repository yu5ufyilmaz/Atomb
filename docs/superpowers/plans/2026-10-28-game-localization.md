# Game Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans and superpowers:dispatching-parallel-agents for the independent owned presentation files.

**Goal:** Unify UI, subtitle and research language with complete English/Turkish tables, extensible language packs, per-locale UI overrides and working settings selectors.

**Architecture:** Keep canonical content and save identities untouched. Pure LocalizationCatalog resolves JSON table entries and legacy presentation formats; GameLocalization owns resource language packs and change events. LocalizedText/UIVariant preserve original styling and refresh presentation. Existing settings persist a locale code while migrating the legacy index.

**Tech Stack:** Unity 6000.3.10f1, C#, TMP/uGUI, ScriptableObjects, JSON; no new packages.

**Spec:** docs/superpowers/specs/2026-10-28-game-localization-design.md

## Global Constraints

- Fully cover English and Turkish; retain existing German subtitle content.
- Do not modify canonical definition/note/page fields, fingerprints, IDs or measurement codes for translation.
- New languages require assets/tables, not enum/source changes.
- Apply locale changes to inactive and open presentation without resetting notebook/page/session state.
- Preserve saved user choice; no commit/push; use current checkout and existing Unity editor.

## Review Focus

- A repeated language switch must restore original layout/font/variant state.
- Dynamic source/format args must remain canonical for later retranslation.
- JSON fallback chains must terminate and missing/empty translations must be readable.
- Changing the settings dropdown must persist the code and not create duplicate callbacks/controls.
- Physical page caches, raster labels and already-playing subtitles must follow the chosen language.

### Task 1: Catalog and binding foundation

- [ ] Add a failing standalone test for real catalog behavior; implement pure resolver, language assets/service and TMP/variant bindings.
- [ ] Prepare EN/TR tables from actual content/UI and preserve subtitle language data; validate missing keys/placeholders and fallback behavior.
- [ ] Run pure tests and Unity compilation/core checks.

### Task 2: Presentation and settings integration

- [ ] Integrate owned content renderers and UI writers through canonical source/format bindings.
- [ ] Migrate settings to language codes, add both selectors, bind static scene text/sprite labels and menu access.
- [ ] Update current subtitles and invalidate physical pages on locale change; preserve all gameplay data.

### Task 3: Authoring and verification

- [ ] Add editor authoring/validation entry points and a concise guide for adding language/UI variants.
- [ ] Verify EN/TR live in menu/settings, book, notebook, board, machine and subtitle; test independent new locale and baseline restoration.
- [ ] Review combined change, run relevant regression checks, restore user's saved language and leave edit mode.

## Ledger

User confirmed English and Turkish. Existing checkout includes earlier tutorial changes and is required input. Parallel workers own separate presentation files and draft translation catalogs; root owns core/settings/subtitles and Unity verification. No other Unity instance is launched.

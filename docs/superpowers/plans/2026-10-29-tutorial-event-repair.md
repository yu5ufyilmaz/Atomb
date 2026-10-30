# Tutorial event repair

Goal: prevent camera/book contention and replayed tutorial introductions, using the existing scene event system.

Architecture: LookAction owns a world-interaction lock from Execute (including its delay) until release/cancellation. DialogAction gains optional condition-following reminders, with history owned by each scene action. A tutorial ICondition adapter exposes puzzle progress; subtitle IDs, reminder delays and variants live in serialized scene components. Remove TutorialManager's global dialogue override.

- [x] Reproduce early book opening and book open/close dialogue replay in Unity; leave regression checks.
- [x] Add camera interaction ownership and cancellation; verify early input is rejected and later interaction works.
- [x] Migrate dialogue to EventLogicController/ICondition/DialogAction scene nodes; retain per-node introduction/reminder history and external speech/pause ownership.
- [x] Verify actual opening focus, repeated book cycles, configured reminders, external dialogue, tutorial disable, existing tutorial flow and compilation. Review final changes.

Validation: real Unity regression passed with delayed focus, queued/cancelled focus, actual book close/reopen, same-session re-enable, reminders, pause and external dialogue. Scene/data core passed for 25 cues / 75 translated lines. Full integration passed through book, board, lock/hiding/enemies, Turing, valve and breaker. The first integration run failed at Lees sight acquisition; the instrumented rerun passed without further production changes. Keep the sight diagnostic in the integration check for future reproduction. Final review found no blockers.

Constraints: preserve authored event actions and existing puzzle progress, subtitle translations and optional clips. No commits or new dependencies. Inspect/save only the requested scene changes.

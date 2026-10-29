# Tutorial event repair

Goal: prevent camera/book contention and replayed tutorial introductions, using the existing scene event system.

Architecture: LookAction owns a world-interaction lock from Execute (including its delay) until release/cancellation. DialogAction gains optional condition-following reminders, with history owned by each scene action. A tutorial ICondition adapter exposes puzzle progress; subtitle IDs, reminder delays and variants live in serialized scene components. Remove TutorialManager's global dialogue override.

- [ ] Reproduce early book opening and book open/close dialogue replay in Unity; leave regression checks.
- [ ] Add camera interaction ownership and cancellation; verify early input is rejected and later interaction works.
- [ ] Migrate dialogue to EventLogicController/ICondition/DialogAction scene nodes; retain per-node introduction/reminder history and external speech/pause ownership.
- [ ] Verify actual opening focus, repeated book cycles, configured reminders, external dialogue, tutorial disable, existing tutorial flow and compilation. Review final changes.

Constraints: preserve authored event actions and existing puzzle progress, subtitle translations and optional clips. No commits or new dependencies. Inspect/save only the requested scene changes.

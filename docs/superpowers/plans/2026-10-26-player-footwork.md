# Player Footwork Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement the runtime and verify it in the current Unity project. Parallel agents may prepare isolated editor setup/checks and review the runtime; only the root agent operates Unity.

**Goal:** Natural alternating idle turn steps and independent stair foot contact without changing authored interaction poses.

**Architecture:** A single opt-in Humanoid `PlayerFootwork` component on the copied laboratory player uses native Animator IK. The copied scene uses its own unchanged-state Animator controller with base IK pass enabled. CharacterController exposes read-only freeze/look state and uses intended look yaw/bounded idle body turn only when this component permits normal locomotion.

**Tech Stack:** Unity 6000.3.10f1, existing Humanoid Animator and Physics, C#; no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-26-player-footwork-design.md`

## Global constraints

- Only `scn_LaboratorySynthesis` opts in. Preserve original scenes, controller, animation clips, existing package changes and local save.
- Never move the character root or reparent the camera. A bounded smooth pelvis bend is allowed only during normal locomotion to reach the lower stair; inspect the resulting head-camera movement.
- Suspend for frozen/disabled player, disabled capsule, inactive/paused/menu gameplay, special/transition states, active interactions and animator speed zero.
- Bound support reach and yaw, release swing feet, reset stale anchors after teleport/interaction.

## Tasks

- [x] Add a runnable real-player Play check that detects absent independent ground support and absent idle turn stepping before enabling the feature. Baseline failed: right lower foot .180m above support.
- [x] Implement native per-foot support with reach/slope/ground-mask checks, animation-aware weights and alternating idle pivot steps. Opt-in controller yaw feedback and idle turn speed only.
- [x] Create an editor setup command which copies the controller, enables base IK pass and attaches the component to the copied scene player. Preserve state/clip/transforms.
- [x] Compile and verify real player on flat/split-height ground, turning both directions, missing support, walking and special-state suppression; inspect visible feet.
- [x] Run existing book stability/full board/measurement/Turing flow checks and inspect camera/root stability. Original scene/controller/build settings verified unchanged.
- [x] Independent review, fix findings, run affected checks again, save scene and record verification.

## Review focus

Foot sole offset under root scale 3.75; first-person camera attached to head; update/IK timing; animated swing preservation; fast yaw change without twisted support leg; Turing approach before activeInteraction is set; restoring control after book pause; existing source scenes/assets.

## Rulings / evidence

- Existing live Unity checkout and copied scene remain the isolation boundary; the editor cannot validate a separate checkout without reopening the project. No worktree or unrelated package changes.
- The request already specifies desired steps, stair contact and preservation of other animations. Independent builder review found no remaining user decision; proceed within that scope rather than adding a design approval round.
- Baseline flat ankle offsets measured on the real player: .1048/.0995m. Raw Animator feetBottomHeight .0339m, naive scale multiplication .127m. Calibrate from a supported flat rest pose instead of assuming the IK goal is the ankle.
- No-pelvis restriction was an initial implementation choice, not a user constraint. Lower-step contact may require a stance bend; allow max .2m smoothed pelvis offset only in whitelisted free locomotion, with first-person camera and interaction regression checks. Keep root/capsule/camera parenting unchanged.
- Fresh review identified idle anchors surviving sprint deceleration and slope contact re-probing from the ankle instead of its contact point. Fixed by using actual capsule planar speed, preserving animated landing support, and re-probing the stored contact point with its normal.
- Final Unity Play check passed: both 18cm split-step cases within2.5cm; +/-90 and180 degree lifts/replants without root translation; existing walking starts/stops;12/35 degree slope contact/stability; unsupported feet; frozen and SitIdle suppression. Visual stair capture inspected at `Temp/player-footwork-step.png`.
- Existing full laboratory flow passed with footwork enabled: all four books/body and camera stability, notebook drags, board pointer/measurements, original Turing approach/sit/paper reader/exit, return to live gameplay. Local save remained at its prior modification time; original scenes/controller/build settings unchanged.

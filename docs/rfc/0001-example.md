---
codex: 1
project: MindAttic.Launcher
code: MCO
layer: rfc
status: planned
updated: 2026-06-07
---

# RFC 0001 — Make the interactive menu flows testable

## Problem
The pure services are well covered (118 green tests), but the interactive Spectre.Console menus
(`OpenProjectMenu`, `BackupMenu`, `RunProjectMenu`, `SettingsMenu`, `PullMenu`) are
exercised only by hand. The largest gap is "Open Project Tab" ([MCO-US-A5](../USER_STORIES.md)),
where the `wt` tab spec is assembled inside the menu rather than in a pure seam.

## Options compared
- **A. Extract a pure tab-spec builder** from each menu (project + provider + color → `wt` Tab /
  command line) and unit-test that, leaving prompt I/O thin. Low risk, matches the existing
  injectable-seam pattern (`DeployService`, `RemoteControlBroadcaster`).
- **B. Drive Spectre prompts via its test console harness.** Higher fidelity, but couples tests to
  prompt wording and ordering.
- **C. Full end-to-end process tests** spawning `wt`/`git`. Highest fidelity, slowest, Windows- and
  environment-fragile.

## Decision
Pending. Default lean is **A** — it closes MCO-US-A5 with the smallest, most stable change and is
consistent with [MCO-§8](../BIBLE.md#MCO-§8) point 3.

## What NOT to do
Do not assert on Spectre prompt strings, and do not spawn real `wt`/`git`/`robocopy` in unit tests.
Keep external-process invocation behind an injectable seam.

## Phased plan (with risk)
1. Extract `OpenProjectMenu`'s tab-spec assembly into a pure method/helper (risk: low).
2. Add unit tests asserting title/color/scheme/working-dir for representative projects (risk: low).
3. Repeat for the remaining menus as coverage warrants (risk: low; effort scales with menu count).

## Graduates into
[BIBLE §6](../BIBLE.md#MCO-§6) (verified state), [USER_STORIES](../USER_STORIES.md) (MCO-US-A5 → ✅,
plus the backlog menu-smoke item).

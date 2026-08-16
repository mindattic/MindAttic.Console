---
codex: 1
project: MindAttic.Launcher
code: MCO
layer: amendments
status: living
updated: 2026-06-19
---

# MindAttic.Launcher — Amendments (append-only; amendment wins over the bible)

> Append only. Never rewrite an amendment — supersede it with a new one. Beyond ~25, fold into
> [BIBLE.md](BIBLE.md) and start a new epoch (note the git tag).

## MCO-A4 — Provider is an ephemeral per-launch choice, not persisted config (supersedes —)
Removed the entire "saved provider" surface: `Project.Provider`, `AppSettings.Provider`,
`AgentProviderRegistry.EffectiveProvider`/`EffectiveProviderKey`/`Next`/`SetDefault`/
`SetProjectProvider`, the `P` cycle-hotkey in `OpenProjectMenu`, the "Provider" row in
`ProjectSetupMenu`, and the "Default Agent" + per-project rows in `SettingsMenu`. In their place,
`OpenProjectMenu` now prompts for which agent CLI to launch with (Claude, Codex, Gemini, Kimi — see
`AgentProviderRegistry.Defaults` order) immediately after a project is picked; the choice is used for
that one launch only and nothing is written to settings. Callers with no launch-time choice to make
(Overlord, Status, a bare `mindattic host` with no `--provider`) now always resolve to the
first-listed provider via `AgentProviderRegistry.Current()`.

## MCO-A3 — Project renamed from MindAttic.Console to MindAttic.Launcher (supersedes —)
The GitHub repo, solution, project folders, assembly name, exe output, all C# namespaces, settings
bucket (`AppBucket`), and every doc reference have been renamed from `MindAttic.Console` to
`MindAttic.Launcher`. The exe is now `artifacts\MindAttic.Launcher.exe`; the launcher bat is
`MindAttic.Launcher.bat`. Settings previously stored at
`%APPDATA%\MindAttic\MindAttic.Console\settings.json` must be manually copied to
`%APPDATA%\MindAttic\MindAttic.Launcher\settings.json` on first run (the legacy-seed path at
`D:\Projects\MindAttic\settings.json` remains functional as a fallback). The git remote was updated
to `https://github.com/mindattic/MindAttic.Launcher.git`. The `mindatticconsole` deploy slug in
`MindAttic.Deploy/projects.json` is unchanged (it is that project's responsibility to update if needed).

## MCO-A2 — Overlord multi-step prompt flow; subprocess LLM exception to MCO-LAW-5 (supersedes —)
`OverlordMenu.Run()` now collects a multi-line draft (blank-line commit), asks Y/N to send it to the
refiner, calls `claude -p <system> <draft>` via `Process.Start` to produce a tighter directive,
shows the refined text, asks Y/N to accept/fall-back/cancel, then launches the WT tab. The
`claude` subprocess is purely orchestration (an external process the binary spawns, same class as
`wt`); it does not use an LLM SDK and does not make HTTP calls from within this binary. MCO-LAW-5
("no code path calls an LLM") is hereby amended to read: _no code path links an LLM SDK or makes
LLM API calls; spawning a CLI tool that happens to call an LLM is permitted as orchestration_.

## MCO-A1 — Codex documentation standard adopted (supersedes —)
Installed the MindAttic Codex canon: `docs/BIBLE.md` (L0), `docs/USER_STORIES.md` (L2), this file
(L1), `docs/rfc/`, the generated `docs/BIBLE.digest.md`, `tools/codex.ps1` (doctor + digest), and
the `.claude` SessionStart digest-injection hook. No application/source code changed. The bible was
reconstructed from the README, csproj/solution, the source tree, and the green test suite (118
passing); it did not previously exist as a doc. Org-wide laws are inherited by reference from
`MindAttic.HouseRules.md` (not restated). No structured tabular canon exists, so no L5
`docs/data/*.json` was created (this is an `app`, not a `game`/`narrative`).

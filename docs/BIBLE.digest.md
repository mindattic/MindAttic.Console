AUTHORITATIVE - full detail in docs/BIBLE.md
<!-- generatedFrom: MCO-bible -->

# MindAttic.Launcher (MCO) - Bible digest

## The one sentence (#MCO-§1)
MindAttic.Launcher is a single Windows binary (`MindAttic.Launcher.exe`) that is the launcher and
orchestrator for the whole MindAttic workspace: an interactive [Spectre.Console](https://spectreconsole.net/)
menu plus a small set of CLI sub-commands that spawn per-project agent tabs in Windows Terminal,
commit/push repos, and back the workspace up.

## What it is NOT (#MCO-§3)
- NOT an agent itself. It hosts and launches agents (`mindattic host` execs the provider with
  inherited stdio); it does not call any LLM, and no code path references an LLM SDK.
- NOT a phone/iPad web terminal. That role belongs to the sibling **MindAttic.Mobile** repo — a
  WebSocket + xterm.js bridge that streams a Windows terminal session to a mobile browser. This
  repo only manages launching agents and orchestrating the workspace.
- NOT a deploy engine. Landing-page deploys are delegated to the sibling **MindAttic.Deploy** repo
  (`MindAttic.Deploy.exe all` / the `/deploy` command); this repo owns no FTP pipeline or per-project
  deploy state.
- NOT cross-platform. It targets `net10.0-windows` / `win-x64` and depends on Windows Terminal (`wt`),
  `robocopy`, and `sqlcmd`.
- NOT a general settings UI. It edits only its own roster/providers and the Windows Terminal
  `schemes` array (idempotent splice).
- NOT hosting a workspace-wide "Overlord" agent session anymore. That feature (`OverlordMenu`, a
  single agent rooted at the whole workspace with an optional LLM-refined opening order) never
  worked reliably and was removed outright — see MCO-A5.

## The Laws (#MCO-§5)
This project **inherits** the org-wide laws in
[`MindAttic.HouseRules.md`](../../MindAttic.HouseRules.md) by reference — they are not restated here.
The directly load-bearing inherited laws for this repo:
- [HOUSE-LAW-1](../../MindAttic.HouseRules.md#HOUSE-LAW-1) — whole-number versioning (the csproj is
  pinned at `1.0.0` with a major-only bump comment).
- [HOUSE-LAW-3](../../MindAttic.HouseRules.md#HOUSE-LAW-3) — credentials resolve through MindAttic.Vault
  (settings + FTP creds via Vault, never hard-coded).
- [HOUSE-LAW-6](../../MindAttic.HouseRules.md#HOUSE-LAW-6) — one engine, many front doors (the
  interactive menu and the `commit`/`host` sub-commands share the same services + DI graph).
- [HOUSE-LAW-8](../../MindAttic.HouseRules.md#HOUSE-LAW-8) — done is verified, not asserted (every
  `✅` in [USER_STORIES.md](USER_STORIES.md) cites a test).
- [HOUSE-LAW-9](../../MindAttic.HouseRules.md#HOUSE-LAW-9) — `psst` only on explicit request.

Project-specific laws:

### {#MCO-LAW-1} A failed external command fails loudly, never silently.
An explicit `--provider` that doesn't resolve, an empty `RunCommand`, or a missing project is a
distinct non-zero exit code (4 / 2 / 1), not a quiet fallback to a default — hiding the mistake would
start the wrong agent. (`Commands/HostAgentCommand.cs`.)

### {#MCO-LAW-2} Settings round-trip is loss-free.
Every model that Vault persists carries `[JsonExtensionData] Extra` so keys a sibling tool wrote, or
a future schema adds, survive a Save instead of being silently dropped. (`Models/AppSettings.cs`,
`Models/Project.cs`, `Models/AgentProvider.cs`.)

### {#MCO-LAW-3} A backup means a real database backup, not a file copy.
robocopy snapshotting a live `.mdf` is not a backup, so backup runs `sqlcmd BACKUP DATABASE`
(schema + data, checksummed, copy-only) per configured database alongside the file snapshot.
(`Services/SqlBackupService.cs`, `Services/BackupService.cs`.)

### {#MCO-LAW-4} Windows Terminal edits are idempotent splices.
Writing a `MindAttic-<Name>` scheme into WT `settings.json` returns the contents unchanged if a
scheme with that name already exists — never duplicating or clobbering the user's WT config.
(`Services/WindowsTerminalSchemes.cs`.)

### {#MCO-LAW-5} Orchestration only; no agent, no LLM here.
This binary launches and hosts agents and delegates deploys to MindAttic.Deploy. No code path calls
an LLM or owns an FTP/deploy pipeline. (`Commands/HostAgentCommand.cs`, `Services/DeployService.cs`.)

## Glossary (#MCO-§9)
- **Workspace root** — `D:\Projects\MindAttic`, the parent dir holding every MindAttic repo.
- **Roster** — the `Projects` list in settings; the set of managed repos.
- **Provider** — a launchable agent CLI (`AgentProvider.RunCommand`, e.g. `claude`, `codex`).
- **Host / host tab** — a `wt` tab running `mindattic host`, which execs a provider with inherited
  stdio rooted at a repo (or `--path` directory, e.g. the Status tab).
- **Pinner** — `TitlePinner`, the per-tab loop that keeps a busy/idle glyph in the tab title.
- **Discovery** — startup scan for git repos under the root not yet in the roster.
- **Vault** — MindAttic.Vault, the `%APPDATA%\MindAttic\...` settings/secret store.
- **Scheme** — a `MindAttic-<Name>` Windows Terminal color scheme (shared ANSI palette, per-project
  background).
- **Sibling repo** — another repo under the workspace root (e.g. MindAttic.Deploy).

## Status index (docs/USER_STORIES.md)
- done: 21
- partial: 5
- planned: 3
- cut: 1

## Latest amendment
MCO-A1 — Codex documentation standard adopted (supersedes —)


---
codex: 1
project: MindAttic.Launcher
code: MCO
layer: bible
status: living
updated: 2026-08-25
---

# MindAttic.Launcher — Project Bible
> Single source of truth for what MindAttic.Launcher IS, is NOT, and the rules that keep it coherent.
> README says how to build/run; this says how to think about the system.

## 1. The one sentence {#MCO-§1}
MindAttic.Launcher is a single Windows binary (`MindAttic.Launcher.exe`) that is the launcher and
orchestrator for the whole MindAttic workspace: an interactive [Spectre.Console](https://spectreconsole.net/)
menu plus a small set of CLI sub-commands that spawn per-project agent tabs in Windows Terminal,
commit/push repos, and back the workspace up.

## 2. The product promise {#MCO-§2}
- Pick a project, then pick an agent CLI (Claude/Codex/Gemini/Kimi) — a working agent session opens
  in a titled, colored Windows Terminal tab rooted at the right repo. The provider choice is
  per-launch, not saved: nothing about "which CLI for this project" is persisted anywhere.
- One menu (or `mindattic commit`) to commit + push one or every MindAttic project, with an
  auto-generated message when none is supplied.
- A real backup: a robocopy snapshot of `D:\Projects\MindAttic` plus full `sqlcmd` SQL Server
  database backups, into a collision-safe dated folder.
- Discovery that surfaces new repos under the workspace root without a hand-edit of settings.
- Settings that round-trip cleanly through [MindAttic.Vault](https://github.com/mindattic/MindAttic.Vault),
  never silently dropping keys this version doesn't model.

## 3. What it is NOT {#MCO-§3}
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

## 4. Architecture canon {#MCO-§4}
A single-file, framework-dependent `win-x64` exe. Spectre.Console.Cli routes `args` to a default
interactive menu command or to named sub-commands. The interactive menu drives a set of menus that
call stateless/injectable services; the services own all external-process and filesystem work.

```
                         args
                          |
                  Spectre.Console.Cli (Program.cs)
                          |
        +-----------------+------------------+--------------+
        |                 |                  |              |
   (default)          host              commit          version
        |                 |                  |
  MainMenuCommand   HostAgentCommand   CommitCommand
        |                 |                  |
   Menus/* ------> Services/* ------> external tools
   (Spectre UI)    (logic+IO)         wt | git | robocopy | sqlcmd | provider exe
        |                 |
   Ui/* (Menu,        TitlePinner / HostInputPipeServer
   Screen, Theme)     (per-tab background loops)
        |
   Models/* (Project, AgentProvider, AppSettings)  <--> SettingsStore <--> MindAttic.Vault
```

### 4.1 Projects
- `MindAttic.Launcher/MindAttic.Launcher.csproj` — the exe (`net10.0-windows`, `OutputType=Exe`,
  `Version 1.0.0`; references MindAttic.Vault 1.0.0, Spectre.Console + Spectre.Console.Cli 0.49.1).
- `MindAttic.Launcher.Tests/MindAttic.Launcher.Tests.csproj` — NUnit 4 test project.
- `scripts/publish.ps1`, `scripts/ensure-fresh.ps1`, `scripts/restart.ps1` — publish/refresh tooling.

### 4.2 Domain model — NOUNS (`MindAttic.Launcher/Models/`)
- **Project** (`Models/Project.cs`) — a managed repo: `Name`, `Path`, `RepoUrl`, tab alias/color/scheme,
  `SqlServer` + `Databases`, `[JsonExtensionData] Extra`. `TabTitle` strips the shared `MindAttic.`
  prefix. Carries no provider — which CLI to launch with is chosen at Open-Project-Tab time, not
  stored per project.
- **AgentProvider** (`Models/AgentProvider.cs`) — a launchable agent: `Key`, `Name`, `RunCommand`,
  `Extra`.
- **AppSettings** (`Models/AppSettings.cs`) — the persisted root: WT settings path, `AgentProviders`,
  `Projects`, `DiscoveryIgnore`, `Extra`. No default-provider field — see MCO-A4.
- Service-local records: `GitStatus`/`GitChange` (`Services/GitService.cs`), `DiscoveredRepo`
  (`Services/ProjectDiscovery.cs`), `PaletteColor` (`Services/ColorPalette.cs`),
  `DatabaseBackupResult`/`BackupTarget` (`Services/SqlBackupService.cs`).

### 4.3 Key services — VERBS (`MindAttic.Launcher/Services/`)
- **SettingsStore** — load/save `AppSettings` via Vault; one-time legacy-file seed.
- **AgentProviderRegistry** — resolve the configured/default provider list (`All()`, `ByKey()`,
  `Current()` = first-listed provider); per-launch provider choice is a menu-layer concern, not
  state this service persists.
- **GitService** — `git status --porcelain` snapshot, short summary, auto commit message.
- **WindowsTerminalLauncher** — build + invoke `wt` tab command lines (title/color/scheme).
- **WindowsTerminalSchemes** — idempotent splice of a `MindAttic-<Name>` scheme into WT settings.
- **ColorPalette** — curated tab colors + pure hex→scheme helpers.
- **ProjectDiscovery** — find unregistered git repos under the workspace root.
- **ProjectRoster** — sort / find roster entries.
- **BackupService** — robocopy snapshot to a collision-safe dated folder.
- **SqlBackupService** — `sqlcmd` full (`BACKUP DATABASE`) per database.
- **DeployService** — locate sibling `MindAttic.Deploy.exe`, compose its `all` command line.
- **TitlePinner** — per-tab watchdog that pins a busy/idle tab title.
- **HostInputPipeServer** / **RemoteControlBroadcaster** — per-tab named-pipe input + broadcast.
- **BuildFreshness** / **ExePath** — detect a stale running binary; resolve self/release exe paths.
- Interop: **CommandLineParser** (argv split honoring quotes), **ConsoleBuffer**,
  **ConsoleInputInjector**.

## 5. The Laws {#MCO-§5}
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

## 6. Verified state {#MCO-§6}
Evidence (2026-06-07, `net10.0-windows`):
- **Build:** `dotnet build` succeeds (`TreatWarningsAsErrors=true`, `Nullable=enable`).
- **Tests:** `dotnet test` → **118 passed, 0 failed, 0 skipped** (NUnit 4), ~263 ms.
- Coverage spans: settings/Vault round-trip + legacy seed + unknown-key preservation
  (`SettingsStoreTests`); provider list resolution + model-flag rewriting (`AgentProviderRegistryTests`); `git
  --porcelain` parsing incl. renames/untracked/both-modified + auto message
  (`GitServiceTests`); the dated-backup-folder allocator + exclude lists (`BackupServiceTests`);
  SQL backup path/SQL composition (`SqlBackupServiceTests`); WT scheme splice idempotency
  (`WindowsTerminalSchemesTests`); argv quoting (`CommandLineParserTests`); discovery
  (`ProjectDiscoveryTests`); tab-title rules (`ProjectTests`); deploy command-line composition
  (`DeployServiceTests`); title-pinner busy detection (`TitlePinnerTests`); remote-control
  broadcast (`RemoteControlBroadcasterTests`); build freshness (`BuildFreshnessTests`); color
  palette (`ColorPaletteTests`); WT launcher (`WindowsTerminalLauncherTests`); roster
  (`ProjectRosterTests`).
- See [USER_STORIES.md](USER_STORIES.md) for the per-capability status + verifying test names.

## 7. Active frontier {#MCO-§7}
- No open RFCs at this time — see [`docs/rfc/`](rfc/) (template at `rfc/0001-example.md`).
- Backlog and partial/planned capabilities live in [USER_STORIES.md](USER_STORIES.md) under
  **Priority backlog**. The headline goal is a frictionless single-binary workspace orchestrator;
  the menus exercised only interactively (Backup/Run/Open/Pull wiring) are the
  least test-covered surface and the next place to add coverage.

## 8. Quality bar {#MCO-§8}
A feature is done (`✅`) only when:
1. It builds clean under `TreatWarningsAsErrors=true` with nullable enabled.
2. Pure logic (parsing, path/SQL/command composition, resolution, allocation, idempotency) is
   covered by an NUnit test, and that test is green.
3. External-process invocations are factored into a pure, testable seam (injectable runner /
   predicate / enumerator) so the decision is tested even when the process isn't run.
4. The capability's story in [USER_STORIES.md](USER_STORIES.md) cites the verifying test.
   Anything proven only by hand stays `🟡`.

## 9. Glossary {#MCO-§9}
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

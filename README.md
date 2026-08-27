# MindAttic.Launcher

A single Windows binary (`MindAttic.Launcher.exe`) that launches and orchestrates the whole
MindAttic workspace — every sibling repo under `D:\Projects\MindAttic`. It is one interactive
[Spectre.Console](https://spectreconsole.net/) menu plus a handful of CLI sub-commands
(`host` / `commit` / `version`), all built on `net10.0-windows`, published `win-x64`.

For architecture, invariants, and verified state see **[docs/BIBLE.md](docs/BIBLE.md)** (L0),
**[docs/AMENDMENTS.md](docs/AMENDMENTS.md)** (L1, amendments win over the bible), and
**[docs/USER_STORIES.md](docs/USER_STORIES.md)** (L2, every `✅` cites its test). This README is
the engineering tour — how to build it, run it, and what every screen and flag does.

## Table of contents

- [What it is / what it's for](#what-it-is--what-its-for)
- [Architecture overview](#architecture-overview)
- [CLI reference](#cli-reference)
- [Interactive menu reference](#interactive-menu-reference)
- [Settings & persistence](#settings--persistence)
- [Agent providers & host tabs](#agent-providers--host-tabs)
- [Remote control](#remote-control)
- [Backup — file + SQL](#backup--file--sql)
- [Windows Terminal color schemes](#windows-terminal-color-schemes)
- [Deploy delegation](#deploy-delegation)
- [Sibling repos & what this repo does NOT do](#sibling-repos--what-this-repo-does-not-do)
- [Build, publish & test](#build-publish--test)
- [Directory layout](#directory-layout)
- [Glossary](#glossary)

## What it is / what it's for

Pick a project, then pick an agent CLI — a working Claude/Codex/Gemini/Kimi session opens in a
titled, colored Windows Terminal tab rooted at the right repo. One menu (or `mindattic commit`)
commits and pushes one project or every project, auto-generating a message when you give none. A
real backup runs a `robocopy` snapshot of the workspace *and* full `sqlcmd BACKUP DATABASE` dumps
of every project's SQL Server databases, into a collision-safe dated folder. Newly created repos
under the workspace root are discovered and offered for the roster automatically, with a matching
Windows Terminal color scheme spliced in. Settings round-trip losslessly through
[MindAttic.Vault](https://mindattic.com/mindatticvault.htm) — this app never has to know about
every key another MindAttic tool writes to the same settings file to preserve them.

It is **not** an agent: it execs a provider CLI with inherited stdio and never calls an LLM SDK or
makes an LLM API call directly.

## Architecture overview

`Program.cs` wires a `Spectre.Console.Cli` `CommandApp<MainMenuCommand>` with two named
sub-commands (`host`, `commit`) plus `version`/`--version`. Running the exe with no arguments (or
via `MainMenuCommand`, the app's default) drives the interactive menu; every named sub-command is a
scriptable front door onto **the same services** — there is no separate code path for "menu mode"
vs. "CLI mode" (HOUSE-LAW-6, "one engine, many front doors").

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
   (Spectre UI)    (logic + IO)       wt | git | robocopy | sqlcmd | provider exe
        |                 |
   Ui/* (Menu,        TitlePinner / HostInputPipeServer
   Screen, Theme)     (per-tab background loops)
        |
   Models/* (Project, AgentProvider, AppSettings)  <--> SettingsStore <--> MindAttic.Vault
```

Every external-process invocation (`wt`, `git`, `robocopy`, `sqlcmd`, an agent provider exe) is
factored into an injectable service with a pure logic core (parsing, path/SQL/argv composition,
idempotency checks) so the decision can be unit-tested even when the process never runs — the
project's quality bar (see [BIBLE §8](docs/BIBLE.md#MCO-§8)) requires this before a story is marked
`✅`.

## CLI reference

All sub-commands are declared in `MindAttic.Launcher/Program.cs` and implemented in
`MindAttic.Launcher/Commands/`.

| Command | Flags | Behavior | Exit codes |
|---|---|---|---|
| *(none)* | — | Runs `MainMenuCommand` — the interactive Spectre.Console menu. | `0` |
| `host` | `--name <NAME>` — project name from settings (optional if `--path` given)<br>`--path <PATH>` — root the agent at an arbitrary directory instead of a registered project; **takes precedence over `--name`** (this is how the Status menu item roots a session at the workspace root)<br>`--title <TITLE>` — tab title (defaults to `--name`, or the directory's leaf name)<br>`--provider <PROVIDER>` — provider key (`Claude`/`Codex`/`Gemini`/`Kimi` by default); omit to resolve to the first-listed provider<br>`--prompt <PROMPT>` — seeds the agent's first turn (pre-fills input; does not auto-submit) | Splits the resolved provider's `RunCommand` into argv (`CommandLineParser.Split`), pushes any Vault-stored credential for that provider (env var for Gemini, `config.toml` splice for Kimi — see [ProviderCredentials](#agent-providers--host-tabs)), starts `TitlePinner` (busy/idle tab-title watchdog) and `HostInputPipeServer` (per-tab named pipe for remote-control injection), then `Process.Start`s the provider exe with inherited stdio and waits for exit. | `0` provider ran and exited cleanly (its own exit code is passed through)<br>`1` unknown `--name`<br>`2` provider resolved but its `RunCommand` is empty<br>`3` provider process failed to start (exception)<br>`4` explicit `--provider` key doesn't resolve to a configured provider<br>`64` neither `--name` nor `--path` given |
| `commit` | `-p\|--project <PROJECT>` — limit to one project (defaults to every roster project)<br>`-m\|--message <MESSAGE>` — commit message (defaults to an auto-generated summary of `git status --porcelain`) | For each target: reads git status, skips clean repos, auto-generates a message from added/modified/deleted/renamed files (capped to 200 chars, falling back to counts) when none is given, then `git add -A && git commit -m <msg> && git push --quiet`. | `0` every targeted project committed/pushed cleanly (or was already clean)<br>`1` at least one project had a missing path, an invalid git status, or a failed add/commit/push |
| `version` (alias `--version`) | — | Prints the assembly's informational version and the running exe's process path. | `0` |

`host` is what a Windows Terminal tab actually runs — the interactive menu never launches a
provider directly; it always builds a `wt … -- MindAttic.Launcher.exe host …` command line via
`WindowsTerminalLauncher` and lets `wt` fork the tab. That's the "one engine, many front doors"
seam: the exact same `HostAgentCommand.Execute` path fires whether the tab was opened from the menu
or by hand from a script.

## Interactive menu reference

Launching the exe with no arguments runs `MainMenuCommand`, which loads settings (surfacing a
one-time legacy-file seed if needed — see [Settings](#settings--persistence)), then — **before**
the first menu paint — silently runs the startup **Discover Projects** walkthrough
(`DiscoverProjectsMenu`, not itself a menu item) offering to add any git repo found directly under
the workspace root that isn't yet in the roster or on the "never ask again" ignore list. It also
computes a one-time build-staleness check (`BuildFreshness.Check()` — warns when the running exe is
behind the latest commit in this repo) and renders a live Claude usage/rate-limit status block
(`ClaudeStatusService`, cached 60s, read from `~/.claude/*`) above every menu redraw.

### Top-level menu

| Item | Tag | Description |
|---|---|---|
| Commit and sync | `commit` | Opens `CommitMenu`: commit + push one project or all, prompting for an optional message (blank = auto-generated). |
| Pull | `pull` | Opens `PullMenu`: `git pull --ff-only` one project or all. |
| Open Project Tab | `open` | Opens `OpenProjectMenu`: pick a project, then pick which agent CLI to launch it with, then `ProjectActionMenu`. |
| Backup | `backup` | Runs `BackupMenu` directly: confirms source/target/database list, then runs the file + SQL backup. |
| Settings | `settings` | Opens `SettingsMenu`: the model each agent CLI runs with (only). |
| Status | `status` | Opens a host tab at the resolved MindAttic workspace root using the first-listed provider, pre-filled with `/status`. |
| Open Command Prompt (Admin) | `cmd` | Opens an elevated `cmd` tab at the workspace root via UAC (`wt … -- cmd`, `Verb=runas`). |
| Open PowerShell (Admin) | `ps` | Same, but `powershell`. |
| Restart | `restart` | Launches `scripts\restart.ps1` in a fresh tab (waits for this process's PID to exit, force-republishes, then re-execs) and exits this instance — needed because Windows locks the running exe image so it can't republish itself in place. |
| Exit | `exit` | Closes the menu; other tabs are untouched. |

The header also shows a staleness notice (when the running build is behind the latest commit) and
the Claude usage/rate-limit block; neither is a menu item.

### Open Project Tab → provider picker → Project Action Menu

`OpenProjectMenu` lists every roster project sorted by name.

- **A project** — prompts "Open `<Project>` with which agent?" over every configured provider
  (Claude, Codex, Gemini, Kimi by default, in that order — **this choice is never persisted**, see
  [MCO-A4](docs/AMENDMENTS.md)), then opens `ProjectActionMenu` for that project + provider pair:

  | Item | Tag | Description |
  |---|---|---|
  | Start Editing | `run` | Opens a host tab (`wt … -- MindAttic.Launcher.exe host --name <Project> --provider <Key>`) rooted at the project path, titled `<TabTitle> [<ProviderKey>]`, colored per the project's `TabColor`/`ColorScheme`. |
  | Run Command | `runcmd` | Opens a plain `cmd /c <Project.RunCommand>` tab (or a `cmd /k echo …` placeholder tab if none is configured, so the tab doesn't just flash and die). |
  | Settings | `setup` | Opens `ProjectSetupMenu` for this project. |

  `ProjectSetupMenu` edits four free-text fields, each a blank-to-clear prompt persisted via
  `SettingsStore.Update`: **Alias** (`TabAlias`), **Description**, **Color Scheme**
  (`ColorScheme`), **Tab Color** (`TabColor`). There is no "Provider" row here — provider choice is
  ephemeral (picked fresh every Open Project Tab), not project config.

### Settings menu

Despite the generic name, `SettingsMenu` today edits exactly one thing per provider: **the model
each agent CLI runs with**. For each configured provider it shows `<Name> model` → the model parsed
out of its `RunCommand` (or "(CLI default)" if none is set). Selecting one opens a picker over
`AgentProviderRegistry.KnownModels[key]` (currently populated only for `Claude`: Fable 5, Opus
4.8/4.7/4.6, Sonnet 5, Sonnet 4.6, Haiku 4.5) plus "Enter model id…" (free text) and "Use CLI
default" (clears the flag). `ProviderModel.Set` rewrites the `--model`/`-m` token in place inside
`RunCommand` (or appends/removes it), and `AgentProviderRegistry.SetModel` persists the change,
materializing the code-level `Defaults` into settings on first edit so there's a row to mutate.
There is no "Default Agent" row and no per-project provider override here — both were removed in
[MCO-A4](docs/AMENDMENTS.md).

## Settings & persistence

Settings are a single `AppSettings` object, loaded/saved through `SettingsStore`
(`MindAttic.Launcher/Services/SettingsStore.cs`) via **MindAttic.Vault**, at:

```
%APPDATA%\MindAttic\MindAttic.Launcher\settings.json
```

On first run, if that Vault file doesn't yet exist, `SettingsStore` looks for a legacy file at
`<repo root>\settings.json` (falling back to the historical `D:\Projects\MindAttic\settings.json`
when the exe isn't running from a checkout) and seeds Vault from it once. A malformed legacy file
logs a message to stderr rather than silently producing an empty roster.

`AppSettings` shape (`MindAttic.Launcher/Models/AppSettings.cs`):

| Field | Type | Notes |
|---|---|---|
| `WindowsTerminalSettingsPath` | `string?` | Path to the user's WT `settings.json`, used by `WindowsTerminalSchemes` when splicing a project color scheme. |
| `AgentProviders` | `List<AgentProvider>` | Configured provider list; empty/missing falls back to `AgentProviderRegistry.Defaults` (Claude/Codex/Gemini/Kimi) at read time — nothing is written to disk until a model edit materializes it. |
| `Projects` | `List<Project>` | The roster — every managed repo. |
| `DiscoveryIgnore` | `List<string>?` | Full paths of repos the user chose "never ask again" for during startup discovery. |
| `Extra` | `Dictionary<string, JsonElement>?` | `[JsonExtensionData]` — any top-level key this version doesn't model (e.g. a `"mobile"` block a sibling tool writes) round-trips through a Save untouched. |

`Project` (`MindAttic.Launcher/Models/Project.cs`) carries `Name`, `Repo`, `RepoUrl` (captured from
`origin` at discovery time, not consumed by launch logic), `Path`, `Description`, `OpenWith`
(detected `.slnx`/`.sln` filename), `RunCommand`, `TabAlias`, `TabColor`, `ColorScheme`, `SqlServer`
+ `Databases` (SQL backup targets), and its own `Extra` extension-data bag. **No provider field** —
which CLI to launch a project with is chosen fresh at Open-Project-Tab time and never persisted
per project.

`AgentProvider` (`MindAttic.Launcher/Models/AgentProvider.cs`) carries `Key`, `Name`, `RunCommand`,
and `Extra`. Every model with an `Extra` bag round-trips unknown keys losslessly — this is
project law [MCO-LAW-2](docs/BIBLE.md#MCO-LAW-2).

## Agent providers & host tabs

`AgentProviderRegistry.Defaults` (`MindAttic.Launcher/Services/AgentProviderRegistry.cs`) ships four
built-in providers, used whenever settings has none configured:

| Key | Name | Default `RunCommand` |
|---|---|---|
| `Claude` | Claude Code | `claude --dangerously-skip-permissions --model claude-sonnet-5` |
| `Codex` | OpenAI Codex | `codex --dangerously-bypass-approvals-and-sandbox` |
| `Gemini` | Google Gemini | `gemini --yolo` |
| `Kimi` | Kimi Code | `kimi --yolo` |

`AgentProviderRegistry.Current()` (the first-listed provider — Claude, by ordering) is what any
launch path with no explicit choice resolves to: a bare `mindattic host` with no `--provider`,
and the Status menu item. Opening a project tab from the menu always prompts fresh instead
(see [Open Project Tab](#open-project-tab--provider-picker--project-action-menu)); the pick is
never saved.

**Credential injection** (`Services/ProviderCredentials.cs`) runs right before `HostAgentCommand`
execs the provider: it resolves a per-provider API key from the shared MindAttic LLM credential
keyring (`MindAttic.Vault.Credentials.LlmCredentialStore`, per
[HOUSE-LAW-3](../MindAttic.HouseRules.md#HOUSE-LAW-3)) and pushes it wherever that CLI expects to
find it —

- **Gemini** reads its key from the `GEMINI_API_KEY` environment variable, so `ProviderCredentials`
  sets it directly on the child process's environment.
- **Kimi** only reads its own `~/.kimi-code/config.toml`, so `Services/KimiConfigSync.cs` performs a
  targeted text splice: it locates `[providers."managed:kimi-code"]` and rewrites just its
  `api_key = "…"` line, leaving every other table/model/comment in the user's file untouched
  (idempotent — a no-op if the value already matches). It refuses to write when that provider
  already has an `.oauth` sub-table, since Kimi rejects a provider with both `api_key` and `oauth`
  set.
- Any other provider (or a missing/blank Vault entry) is a no-op — the CLI falls back to however
  it's already configured (its own login flow, an existing config key, etc.). This is never a hard
  failure.

**Host tabs.** Every agent session is a Windows Terminal tab running
`MindAttic.Launcher.exe host --name <Project> --provider <Key> --title "<Title> [<Key>]"` (or
`--path <dir>` for Status), built by `WindowsTerminalLauncher.BuildAgentTab` /
`BuildAgentTabAtPath` and opened with `--tabColor`/`--colorScheme` from the project. Inside that
process, two background loops run for the tab's lifetime:

- **`TitlePinner`** polls the console's bottom rows every 250ms (`ConsoleBuffer.ReadBottomRows`)
  looking for a busy signature ("esc to interrupt", "ctrl+c to cancel", or Claude Code's "N
  shell(s)" background-job footer) and reasserts the tab title as `▶  <title>` (busy) or
  `⏸  <title>` (idle) whenever the CLI's own OSC title write has clobbered it — Windows Terminal
  exposes no API for the launcher process to set another tab's title from outside, so the watchdog
  has to live inside the hosted process. This requires the tab be opened *without*
  `--suppressApplicationTitle` (`BuildAgentTab` sets `SuppressApplicationTitle = false`).
- **`HostInputPipeServer`** listens on a per-tab named pipe (`mindattic-host-{provider}-{pid}`,
  single-instance) and injects any text it receives into the console's input buffer via
  `ConsoleInputInjector` — the delivery mechanism for [Remote control](#remote-control).

## Remote control

Driving an agent tab interactively from a phone or iPad is handled by Claude Code's own built-in
`/remote-control` — typed inside the tab like any other slash command. The previous
`MindAttic.Mobile` SignalR bridge has been removed from the workspace; that role no longer lives
anywhere in this repo.

Separately, `Services/RemoteControlBroadcaster.cs` implements a pipe-based fan-out: given a provider
key and a payload, it enumerates `\\.\pipe\` for every live `mindattic-host-{provider}-*` pipe
(one per open host tab for that provider — see [Host tabs](#agent-providers--host-tabs) above) and
writes the payload to each in parallel, so a single call could type `/remote-control` (or anything
else) into every open Claude tab at once. This class is implemented and unit-tested
(`RemoteControlBroadcasterTests` — pipe-prefix filtering, zero-match reporting), but as of this
read of the source it is **not currently wired into a menu item or CLI sub-command** — nothing in
`MainMenuCommand`/`Menus/*` constructs a `RemoteControlBroadcaster`. Treat it as a tested building
block awaiting a front door, not an exposed feature yet.

## Backup — file + SQL

The **Backup** menu item runs `BackupMenu.Run()` directly (no submenu). It:

1. Resolves a collision-safe target folder via `BackupService.ResolveTargetFolder()`:
   `R:\Backup\MindAttic\<yyyy-MM-dd>`, or the first free `<date>_a` … `<date>_z` if today's plain
   folder is taken (throws only if all 27 slots are exhausted).
2. Collects SQL backup targets via `SqlBackupService.CollectTargets(settings)` — every non-blank
   name in each project's `Databases`, paired with that project's `SqlServer` (or
   `SqlBackupService.DefaultInstance`, `"localhost"`), deduplicated case-insensitively per
   (server, database) pair.
3. Shows the source/target/database list and asks for confirmation.
4. Runs the file backup: `robocopy <source> <target> /E …` excluding
   `Library, Temp, Logs, obj, bin, Build, Builds, node_modules, .vs, .idea, .git` and
   `*.log, *.tmp`, with a live byte-count status tick. Exit codes 0–7 are success; 8+ (or a
   cancellation) is failure, regardless of what the raw exit code alone would suggest.
5. Runs the database backups (unconditionally, even if the file backup failed) via
   `SqlBackupService.Backup`: each target gets `sqlcmd -S <server> -E -C -b -Q "<BACKUP DATABASE …
   WITH FORMAT, INIT, COPY_ONLY, CHECKSUM, NAME = N'MindAttic.Launcher backup';"`, written to
   `<target>\Databases\<server>\<database>.bak` (both path segments sanitized of illegal filename
   characters). A failed/cancelled backup deletes its own half-written `.bak` rather than leaving a
   corrupt file behind masquerading as a real backup.
6. Reports both outcomes (byte totals/elapsed for the file copy; per-database OK/FAILED with
   `sqlcmd` exit codes and a truncated error tail).

This exists because a `robocopy` snapshot of a live `.mdf` file is not a real database backup
([MCO-LAW-3](docs/BIBLE.md#MCO-LAW-3)) — the SQL step runs a genuine, checksummed, schema+data
`BACKUP DATABASE` per configured database, alongside the file snapshot, into the same dated folder.

## Windows Terminal color schemes

Every project tab gets a `--tabColor` (a plain hex the tab strip itself renders) and optionally a
matching `MindAttic-<Name>` **scheme** in Windows Terminal's own `settings.json` (all ANSI colors
shared; only `background` differs, derived by scaling the tab color to ~16% brightness —
`ColorPalette.DarkBackground`). `Services/WindowsTerminalSchemes.cs` writes this scheme with a
targeted text splice — locate the `schemes` array, insert the block after its `[` — rather than a
JSON parse/reserialize, so the user's large, hand-maintained WT settings file isn't reflowed by a
round-trip. The splice is idempotent: re-running it with a scheme name that already exists in the
array returns the file unchanged ([MCO-LAW-4](docs/BIBLE.md#MCO-LAW-4)).

`Services/ProjectDiscovery.cs` scans the immediate subdirectories of the resolved MindAttic
workspace root for git repos (`.git` directory or, for worktrees, a `.git` file) not already in
`Projects` or `DiscoveryIgnore`, surfacing them at startup via `DiscoverProjectsMenu`. For each
candidate it prompts for the git URL (pre-filled from `origin` if detectable), a tab color from
`ColorPalette.Colors` (a curated 16-entry palette) or a typed custom hex, and an optional
description, then appends the project to the roster and writes its WT scheme — with `S` = skip all
remaining candidates this run, `N` = never ask about this one again (added to `DiscoveryIgnore`),
and `Esc` = skip just this one.

## Deploy delegation

`Services/DeployService.cs` locates the sibling **MindAttic.Deploy** repo's published artifact
(`../MindAttic.Deploy/artifacts/MindAttic.Deploy.exe`, relative to this repo's parent directory)
and composes the command line for its non-interactive `all` sub-command (which itself iterates
catalog + site + app batches and tallies failures). `WindowsTerminalLauncher.BuildDeployAllTab`
wraps that command line in a `cmd /k` tab so the pane stays open to read the summary. Both are
implemented and unit-tested (`DeployServiceTests` — exe resolution, null-safety, command-line
composition), but as of this read of the source **no menu item or CLI sub-command in this repo
currently constructs a `DeployService`/`BuildDeployAllTab` call** — the `.claude/commands/deploy.md`
skill in this repo instead shells out to `MindAttic.Deploy`'s own `npm run deploy -- --only
mindatticconsole` to publish this repo's landing page, and separately documents an in-app "Deploy
All" menu item that is not present in `MainMenuCommand.cs` today. This binary owns no FTP pipeline
and no per-project deploy state regardless ([MCO-LAW-5](docs/BIBLE.md#MCO-LAW-5)) — deploying is,
and will remain, MindAttic.Deploy's job.

## Sibling repos & what this repo does NOT do

MindAttic.Launcher is the orchestrator, not the thing being orchestrated. Concretely, it is **NOT**:

- **An agent.** It execs a provider CLI with inherited stdio (`mindattic host`); no code path here
  links an LLM SDK or makes an LLM API call.
- **A phone/iPad web terminal.** That was `MindAttic.Mobile` (a WebSocket + xterm.js bridge); it has
  been removed from the workspace. Remote driving of a tab is Claude Code's own `/remote-control`.
- **A deploy engine.** Landing-page and per-project deploys are **MindAttic.Deploy**'s job
  (`MindAttic.Deploy.exe all`, or the `/deploy` slash command in this repo, which shells out to it).
  This repo owns no FTP pipeline.
- **A general settings UI.** It edits only its own roster/provider settings and the Windows
  Terminal `schemes` array (an idempotent splice) — it is not a Windows Terminal settings editor.
- **Cross-platform.** It targets `net10.0-windows`/`win-x64` and hard-depends on Windows Terminal
  (`wt`), `robocopy`, and `sqlcmd` all being on `PATH`.
- **A credential store.** Vault/API keys are resolved through **MindAttic.Vault**'s shared keyring
  (`MindAttic.Vault.Credentials.LlmCredentialStore`); this repo never hard-codes a secret.

## Build, publish & test

```pwsh
# Build (TreatWarningsAsErrors=true, Nullable=enable, LangVersion=latest)
dotnet build

# Test — NUnit 4, run from the repo root
dotnet test

# Publish a single-file, framework-dependent win-x64 exe to artifacts\
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1
#   -Clean   : wipe bin/obj first for a full, timestamp-independent rebuild
```

`scripts\ensure-fresh.ps1` republishes `artifacts\MindAttic.Launcher.exe` only when it's missing or
a source file is newer than it (a fast no-op otherwise); `-Force` skips that heuristic. It's called
on every launch (by whatever wrapper invokes the exe) and by the in-app Restart flow.
`scripts\restart.ps1` is what "Restart" actually launches in a fresh tab: it waits for the old
process's PID to exit (releasing the Windows-held lock on the running exe image), force-republishes,
then hands the tab to the fresh binary — loudly refusing to fall back to a stale exe on a failed
rebuild.

Test coverage (`MindAttic.Launcher.Tests/`, NUnit 4) spans: settings/Vault round-trip + legacy-seed
migration + unknown-key preservation; agent-provider list resolution, default ordering, and
`--model` token rewriting; `ProviderCredentials`/`KimiConfigSync` credential injection (env var +
config.toml splice, oauth-guard, idempotency); `CommandLineToArgvW`-style argv quoting; `git
--porcelain` parsing (renames, both-modified, untracked, quoted paths) and auto commit-message
composition/truncation; the dated backup-folder allocator and exclude lists; SQL backup path/SQL/arg
composition; Windows Terminal scheme-splice idempotency and launcher tab-building; project discovery
and roster sorting; tab-title alias/prefix rules; deploy command-line composition; title-pinner busy
detection (including the background-shell footer); remote-control pipe broadcast filtering; and
build-freshness day-floor/timezone comparison.

After editing anything under `docs/`, run `powershell -File tools/codex.ps1 doctor` (regenerates
`docs/BIBLE.digest.md` and validates IDs/links/front-matter/cited tests). See
[CLAUDE.md](CLAUDE.md) for the full documentation-layering rules.

## Directory layout

```
MindAttic.Launcher/                  the exe project
  Commands/                          Spectre.Console.Cli sub-commands (host, commit, version, main menu)
  Menus/                             interactive Spectre.Console screens
  Services/                          logic + external-process/filesystem seams (git, wt, robocopy, sqlcmd, Vault, …)
  Models/                            persisted nouns (Project, AgentProvider, AppSettings)
  Ui/                                Menu/Screen/Theme — the keyboard-driven prompt widget + chrome
  Interop/                           CommandLineParser, ConsoleBuffer, ConsoleInputInjector (Win32-adjacent helpers)
  M.ico                              exe icon
MindAttic.Launcher.Tests/            NUnit 4 test project (one *Tests.cs per service/model, roughly)
docs/
  BIBLE.md                           L0 — architecture, Laws, verified state, glossary
  AMENDMENTS.md                      L1 — append-only change log (wins over the bible)
  USER_STORIES.md                    L2 — test-cited stories
  BIBLE.digest.md                    GENERATED — never hand-edit
  rfc/                                design notes awaiting graduation
scripts/
  publish.ps1                        dotnet publish -> artifacts\MindAttic.Launcher.exe
  ensure-fresh.ps1                   conditional republish (staleness heuristic)
  restart.ps1                        used by the in-app Restart flow (waits out the exe lock)
tools/
  codex.ps1                          docs doctor/digest tooling
  build-readme.ps1                   thin wrapper -> workspace-shared README->HTML engine
artifacts/                           publish output (gitignored)
Directory.Build.props                shared MSBuild settings (Nullable, TreatWarningsAsErrors, …)
MindAttic.Launcher.slnx               solution file
NuGet.config, global.json             package source + SDK pin
```

## Glossary

| Term | Meaning |
|---|---|
| **Workspace root** | `D:\Projects\MindAttic` — the parent directory holding every MindAttic repo. |
| **Roster** | The `Projects` list in settings; the set of managed repos. |
| **Provider** | A launchable agent CLI (`AgentProvider.RunCommand`) — e.g. Claude, Codex, Gemini, Kimi. |
| **Host / host tab** | A `wt` tab running `mindattic host`, which execs a provider with inherited stdio, rooted at a repo (or `--path` directory, e.g. the Status tab). |
| **Pinner** | `TitlePinner` — the per-tab background loop that keeps a busy/idle glyph in the tab title. |
| **Discovery** | The startup scan for git repos under the workspace root that aren't yet in the roster. |
| **Vault** | MindAttic.Vault — the shared `%APPDATA%\MindAttic\…` settings/secret store this repo persists through. |
| **Scheme** | A `MindAttic-<Name>` Windows Terminal color scheme — one shared ANSI palette, a per-project background. |
| **Sibling repo** | Another repo under the workspace root (e.g. MindAttic.Deploy, MindAttic.Vault). |

---

See [docs/BIBLE.md](docs/BIBLE.md) for the architecture canon and the Laws, [docs/AMENDMENTS.md](docs/AMENDMENTS.md)
for what has changed since, and [docs/USER_STORIES.md](docs/USER_STORIES.md) for the per-capability
status and the test that verifies each one.

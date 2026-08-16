using System.Diagnostics.CodeAnalysis;
using MindAttic.Launcher.Menus;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MindAttic.Launcher.Commands;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by Spectre.Console.Cli")]
public sealed class MainMenuCommand : AsyncCommand<MainMenuCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var store = new SettingsStore();
        _ = store.Load(); // surface legacy-seed migration on launch

        var providers = new AgentProviderRegistry(store);
        var wt = new WindowsTerminalLauncher();
        var git = new GitService();

        var commit   = new CommitMenu(store, git);
        var pull     = new PullMenu(store, git);
        var open     = new OpenProjectMenu(store, providers, wt);
        var backup   = new BackupMenu(new BackupService(), store, new SqlBackupService());
        var settingsMenu = new SettingsMenu(providers);

        // Checked once at launch: running git every menu redraw would be wasteful,
        // and the build can't change underneath a running process anyway.
        var staleness = BuildFreshness.Check();
        var status = new ClaudeStatusService();

        // Offer any git repos found under the workspace that aren't in the roster
        // yet, so a freshly-created repo is added (with its color scheme) instead
        // of staying invisible to every menu until someone edits settings by hand.
        new DiscoverProjectsMenu(store, git, new WindowsTerminalSchemes()).Run();

        var items = new List<MenuItem>
        {
            new() { Name = "Commit and sync",               Description = "commit and push changes per project or across all", Tag = "commit" },
            new() { Name = "Pull",                          Description = "git pull --ff-only per project or across all", Tag = "pull" },
            new() { Name = "Open Project Tab",              Description = "select a project, then pick which agent CLI to open it with", Tag = "open" },
            new() { Name = "Backup",                        Description = "back up MindAttic to R:\\Backup\\MindAttic", Tag = "backup" },
            new() { Name = "Settings",                      Description = "CLI development: the model each agent CLI runs with", Tag = "settings" },
            new() { Name = "Status",                        Description = "open a Claude tab at the workspace root with /status pre-filled", Tag = "status" },
            new() { Name = "Open Command Prompt (Admin)",   Description = "open cmd as Administrator at the workspace root", Tag = "cmd" },
            new() { Name = "Open PowerShell (Admin)",       Description = "open PowerShell as Administrator at the workspace root", Tag = "ps" },
            new() { Name = "Restart",                       Description = "reload this console in a new tab; other tabs are untouched", Tag = "restart" },
            new() { Name = "Exit",                          Description = "close this menu (other tabs are untouched)", Tag = "exit" }
        };

        var resumeIndex = 0;
        while (true)
        {
            Screen.Header();
            RenderStaleness(staleness);
            status.Render();

            // Cancel after 60 s so the menu redraw loop wakes up and reprints
            // fresh status even when the user is idle.
            using var refreshCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var result = Ui.Menu.PromptWithKeys(
                "MindAttic Launcher — choose an action:",
                items,
                customKeys: null,
                allowBack: false,
                refreshToken: refreshCts.Token,
                initialIndex: resumeIndex);

            resumeIndex = result.Index;
            if (result.Timeout) continue;
            if (result.Selected is null) return Task.FromResult(0);

            switch (result.Selected.Tag)
            {
                case "commit":   commit.Run(); break;
                case "pull":     pull.Run(); break;
                case "open":     open.Run(); break;
                case "backup":   backup.Run(); break;
                case "settings": settingsMenu.Run(); break;
                case "status":
                {
                    var statusRoot = Menus.OverlordMenu.ResolveMindAtticRoot();
                    if (!Directory.Exists(statusRoot)) statusRoot = MindAtticRoot();
                    var provider = providers.Current();
                    Screen.Working("Opening status tab…  Please wait.");
                    ExePath.EnsureFresh();
                    wt.Open(wt.BuildAgentTabAtPath(
                        $"Status [{provider.Key}]", statusRoot, provider, ExePath.Release,
                        prompt: "/status"));
                    Thread.Sleep(800);
                    break;
                }
                case "cmd":
                case "ps":
                {
                    // Open at the MindAttic workspace root (the parent that holds
                    // every repo), matching "the root directory" in the hint —
                    // MindAtticRoot() is the Launcher *repo* (Deploy needs it that
                    // way), which is a subfolder, not the workspace.
                    var adminRoot = Menus.OverlordMenu.ResolveMindAtticRoot();
                    if (!Directory.Exists(adminRoot)) adminRoot = MindAtticRoot();
                    var tab = result.Selected.Tag is "ps"
                        ? wt.BuildPowerShellTab(adminRoot)
                        : wt.BuildCmdTab(adminRoot);
                    Screen.Working("Opening elevated tab…  Please wait.");
                    wt.OpenElevated(tab);
                    Thread.Sleep(600);
                    break;
                }
                case "restart":
                    Screen.Working("Rebuilding and restarting…  Please wait.");
                    RestartInNewTab(wt);
                    Thread.Sleep(1000);
                    return Task.FromResult(0);
                case "exit":     return Task.FromResult(0);
            }
        }
    }

    private static void RenderStaleness(BuildFreshness.Result? r)
    {
        if (r is null) return;

        // The menu process can't rebuild itself; Restart republishes and reloads.
        // DaysBehind floors to whole days, so 0 means "behind by less than a day"
        // — not "built today" (the build itself may be days old; it's the *gap*
        // to HEAD that's under a day).
        var age = r.DaysBehind >= 1
            ? $"[yellow]{r.DaysBehind} day{(r.DaysBehind == 1 ? "" : "s")}[/] behind the latest commit"
            : "[yellow]less than a day[/] behind the latest commit";
        AnsiConsole.MarkupLine($"  [grey50]Heads up:[/] this menu build is {age}. [grey50]Choose Restart to republish & reload.[/]");
        AnsiConsole.WriteLine();
    }

    private static string MindAtticRoot()
    {
        var legacy = SettingsStore.DefaultLegacySettingsPath;
        var root = Path.GetDirectoryName(legacy);
        return Directory.Exists(root) ? root! : Environment.CurrentDirectory;
    }

    private static void RestartInNewTab(WindowsTerminalLauncher wt)
    {
        // We CAN'T republish here: this menu runs from artifacts\MindAttic.Launcher.exe,
        // and Windows locks a running exe image — dotnet publish can't overwrite it, so
        // an in-process EnsureFresh() silently fails and we'd respawn the stale binary.
        // Instead launch scripts\restart.ps1 in a fresh tab; it waits for THIS process
        // to exit (releasing the lock), republishes, then runs the fresh exe. We return
        // 0 right after, so this tab closes and the lock drops.
        var root = ExePath.RepoRoot;
        var restartScript = root is null ? null : Path.Combine(root, "scripts", "restart.ps1");

        if (restartScript is null || !File.Exists(restartScript))
        {
            // No repo/script to publish from (e.g. an installed copy) — just relaunch
            // whatever exe currently exists; there's nothing to rebuild against.
            wt.Open(new WindowsTerminalLauncher.Tab
            {
                Title = "MindAttic.Launcher",
                WorkingDirectory = MindAtticRoot(),
                Command = [ExePath.Release]
            });
            return;
        }

        wt.Open(new WindowsTerminalLauncher.Tab
        {
            Title = "MindAttic.Launcher",
            WorkingDirectory = MindAtticRoot(),
            Command =
            [
                "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass",
                "-File", restartScript,
                "-WaitPid", Environment.ProcessId.ToString()
            ]
        });
    }
}

using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

public sealed class ProjectActionMenu(
    SettingsStore store,
    WindowsTerminalLauncher wt,
    Project project,
    AgentProvider provider)
{
    public void Run()
    {
        var resumeIndex = 0;
        while (true)
        {
            var settings = store.Load();
            var current = ProjectRoster.FindByName(settings, project.Name) ?? project;

            var hasCmd = !string.IsNullOrWhiteSpace(current.RunCommand);
            var items = new List<MenuItem>
            {
                new() { Name = "Start Editing", Description = $"open agent tab ({provider.Name})", Tag = "run" },
                new() { Name = "Run Command",   Description = hasCmd ? current.RunCommand! : "(none — configure in Settings)", Tag = "runcmd" },
                new() { Name = "Settings",      Description = "alias, description, color, provider", Tag = "setup" },
            };

            Screen.Header(current.Name);
            var result = Menu.PromptWithKeys($"Choose an action for {Markup.Escape(current.Name)}:", items, customKeys: null, initialIndex: resumeIndex);
            resumeIndex = result.Index;
            var sel = result.Selected;
            if (sel is null) return;

            switch (sel.Tag)
            {
                case "run":
                    if (!Directory.Exists(current.Path))
                    {
                        Screen.Notice($"[red]Path not found:[/] [grey50]{Markup.Escape(current.Path)}[/]");
                        Screen.PressAnyKey();
                        break;
                    }
                    Screen.Working();
                    ExePath.EnsureFresh();
                    wt.Open(wt.BuildAgentTab(current, provider, ExePath.Release));
                    Thread.Sleep(800);
                    return;

                case "runcmd":
                    if (string.IsNullOrWhiteSpace(current.RunCommand))
                    {
                        Screen.Notice("[yellow]No run command configured — use Settings to add one.[/]");
                        Screen.PressAnyKey();
                        break;
                    }
                    if (!Directory.Exists(current.Path))
                    {
                        Screen.Notice($"[red]Path not found:[/] [grey50]{Markup.Escape(current.Path)}[/]");
                        Screen.PressAnyKey();
                        break;
                    }
                    wt.Open(wt.BuildRunCommandTab(current));
                    Screen.Notice($"[green]Started:[/] [cyan1]{Markup.Escape(current.Name)}[/] → {Markup.Escape(current.RunCommand!)}");
                    Thread.Sleep(800);
                    return;

                case "setup":
                    new ProjectSetupMenu(store, current.Name).Run();
                    break;
            }
        }
    }
}

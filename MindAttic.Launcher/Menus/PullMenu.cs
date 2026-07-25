using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

public sealed class PullMenu(SettingsStore store, GitService git)
{
    public void Run()
    {
        var resumeIndex = 0;
        while (true)
        {
            var sortedProjects = ProjectRoster.Sorted(store.Load());
            var statuses = git.FetchShortStatuses(sortedProjects);

            var items = new List<MenuItem>
            {
                new()
                {
                    Name = "All Projects",
                    Description = "git pull --ff-only across every project",
                    Tag = "all"
                }
            };
            foreach (var p in sortedProjects)
                items.Add(new MenuItem { Name = p.Name, Description = statuses[p.Name], Tag = p });

            Screen.Header("Pull");
            var result = Menu.PromptWithKeys("Choose a project to pull:", items, customKeys: null, initialIndex: resumeIndex);
            resumeIndex = result.Index;
            var sel = result.Selected;
            if (sel is null) return;

            if (Equals(sel.Tag, "all"))
                PullAll(sortedProjects);
            else if (sel.Tag is Project project)
                PullOne(project);
        }
    }

    private void PullAll(IReadOnlyList<Project> projects)
    {
        Screen.Header("Pull", "All Projects");
        AnsiConsole.MarkupLine("  [yellow]Pulling latest across all projects...[/]");
        AnsiConsole.WriteLine();

        foreach (var p in projects) PullProject(p);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [green]Done.[/]");
        Screen.PressAnyKey();
    }

    private void PullOne(Project project)
    {
        Screen.Header("Pull", project.Name);
        PullProject(project);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [green]Done.[/]");
        Screen.PressAnyKey();
    }

    private void PullProject(Project p)
    {
        if (!Directory.Exists(p.Path))
        {
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/] [red]path not found[/]");
            return;
        }

        var (ok, message) = git.Pull(p.Path);
        if (!ok)
        {
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/] [red]pull failed[/]");
            foreach (var line in (message ?? "").Split('\n'))
                if (!string.IsNullOrWhiteSpace(line))
                    AnsiConsole.MarkupLine($"      [grey50]{Markup.Escape(line.TrimEnd())}[/]");
            return;
        }

        var color = message == "up to date" ? "grey50" : "green";
        AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/] [{color}]{Markup.Escape(message ?? "")}[/]");
    }
}

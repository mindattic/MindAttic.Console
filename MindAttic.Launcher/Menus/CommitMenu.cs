using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

public sealed class CommitMenu(SettingsStore store, GitService git)
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
                    Description = "commit and push across all projects",
                    Tag = "all"
                }
            };

            foreach (var p in sortedProjects)
                items.Add(new MenuItem { Name = p.Name, Description = statuses[p.Name], Tag = p });

            Screen.Header("Commit and sync");
            var result = Menu.PromptWithKeys("Choose a project to commit:", items, customKeys: null, initialIndex: resumeIndex);
            resumeIndex = result.Index;
            var sel = result.Selected;
            if (sel is null) return;

            if (Equals(sel.Tag, "all"))
                CommitAll(sortedProjects);
            else if (sel.Tag is Project project)
                CommitOne(project);
        }
    }

    private void CommitAll(IReadOnlyList<Project> projects)
    {
        Screen.Header("Commit and sync", "All Projects");
        AnsiConsole.MarkupLine("  [yellow]Enter a commit message, or leave blank to auto-generate.[/]");
        AnsiConsole.WriteLine();
        var userMessage = AnsiConsole.Prompt(
            new TextPrompt<string>("  > ").AllowEmpty());
        AnsiConsole.WriteLine();

        foreach (var p in projects) CommitProject(p, userMessage);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [green]Done.[/]");
        Screen.PressAnyKey();
    }

    private void CommitOne(Project project)
    {
        Screen.Header("Commit and sync", project.Name);
        AnsiConsole.MarkupLine("  [yellow]Enter a commit message, or leave blank to auto-generate.[/]");
        AnsiConsole.WriteLine();
        var userMessage = AnsiConsole.Prompt(
            new TextPrompt<string>("  > ").AllowEmpty());
        AnsiConsole.WriteLine();

        CommitProject(project, userMessage);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [green]Done.[/]");
        Screen.PressAnyKey();
    }

    private void CommitProject(Project p, string? userMessage)
    {
        if (!Directory.Exists(p.Path))
        {
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/] [red]path not found[/]");
            return;
        }

        var status = git.Status(p.Path);
        if (!status.IsValid)
        {
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/] [red]{Markup.Escape(status.Error ?? "")}[/]");
            return;
        }
        if (status.IsClean)
        {
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/] [grey50]nothing to commit[/]");
            return;
        }

        var (ok, message, commitMessage) = git.Commit(p.Path, userMessage);
        var label = string.IsNullOrEmpty(commitMessage) ? "" : $" [darkorange]{Markup.Escape(commitMessage)}[/]";
        if (ok)
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/]{label} [green]{Markup.Escape(message)}[/]");
        else
            AnsiConsole.MarkupLine($"    [white]{Markup.Escape(p.Name)}:[/]{label} [red]{Markup.Escape(message)}[/]");
    }
}

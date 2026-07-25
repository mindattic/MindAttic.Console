using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

public sealed class ProjectSetupMenu(SettingsStore store, AgentProviderRegistry providers, string projectName)
{
    public void Run()
    {
        var resumeIndex = 0;
        while (true)
        {
            var settings = store.Load();
            var project = ProjectRoster.FindByName(settings, projectName);
            if (project is null) return;

            var effectiveKey = providers.EffectiveProviderKey(project);
            var items = new List<MenuItem>
            {
                new()
                {
                    Name = "Provider",
                    Description = string.IsNullOrWhiteSpace(project.Provider)
                        ? $"default: {effectiveKey}"
                        : effectiveKey,
                    Tag = "provider"
                },
                new()
                {
                    Name = "Alias",
                    Description = string.IsNullOrWhiteSpace(project.TabAlias) ? "(uses project name)" : project.TabAlias,
                    Tag = "alias"
                },
                new()
                {
                    Name = "Description",
                    Description = string.IsNullOrWhiteSpace(project.Description) ? "(none)" : project.Description,
                    Tag = "desc"
                },
                new()
                {
                    Name = "Color Scheme",
                    Description = string.IsNullOrWhiteSpace(project.ColorScheme) ? "(none)" : project.ColorScheme,
                    Tag = "scheme"
                },
                new()
                {
                    Name = "Tab Color",
                    Description = string.IsNullOrWhiteSpace(project.TabColor) ? "(none)" : project.TabColor,
                    Tag = "color"
                },
            };

            Screen.Header(projectName, "Setup");
            var result = Menu.PromptWithKeys($"Configure {Markup.Escape(projectName)}:", items, customKeys: null, initialIndex: resumeIndex);
            resumeIndex = result.Index;
            var sel = result.Selected;
            if (sel is null) return;

            switch (sel.Tag)
            {
                case "provider": PickProvider(project); break;
                case "alias":    EditField(project, "Alias",        p => p.TabAlias,    (p, v) => p.TabAlias    = v); break;
                case "desc":     EditField(project, "Description",  p => p.Description, (p, v) => p.Description = v); break;
                case "scheme":   EditField(project, "Color Scheme", p => p.ColorScheme, (p, v) => p.ColorScheme = v); break;
                case "color":    EditField(project, "Tab Color",    p => p.TabColor,    (p, v) => p.TabColor    = v); break;
            }
        }
    }

    private void EditField(Project project, string label, Func<Project, string?> get, Action<Project, string?> set)
    {
        var current = get(project);
        Screen.Header(projectName, "Setup", label);
        AnsiConsole.MarkupLine($"  Current: [cyan1]{Markup.Escape(string.IsNullOrWhiteSpace(current) ? "(none)" : current!)}[/]");
        AnsiConsole.WriteLine();

        var entered = AnsiConsole.Prompt(
            new TextPrompt<string>($"  [cyan1]{Markup.Escape(label)}[/] [grey50](blank to clear):[/]")
                .AllowEmpty()
                .DefaultValue(current ?? "")
                .ShowDefaultValue(!string.IsNullOrWhiteSpace(current)));

        var trimmed = entered.Trim();
        store.Update(s =>
        {
            var p = ProjectRoster.FindByName(s, projectName);
            if (p is null) return;
            set(p, string.IsNullOrWhiteSpace(trimmed) ? null : trimmed);
        });

        Screen.Notice($"[green]{Markup.Escape(label)} saved.[/]");
        Thread.Sleep(600);
    }

    private void PickProvider(Project project)
    {
        var defaultProvider = providers.Current();
        var projectKey = string.IsNullOrWhiteSpace(project.Provider) ? null : project.Provider;

        var items = new List<MenuItem>
        {
            new()
            {
                Name = "Use Default",
                Description = projectKey is null
                    ? $"current - use default: {defaultProvider.Name}"
                    : $"use default: {defaultProvider.Name}",
                Tag = "default"
            }
        };
        foreach (var p in providers.All())
        {
            items.Add(new MenuItem
            {
                Name = p.Name,
                Description = string.Equals(p.Key, projectKey, StringComparison.OrdinalIgnoreCase)
                    ? $"current - {p.RunCommand}"
                    : p.RunCommand,
                Tag = p
            });
        }

        Screen.Header(projectName, "Setup", "Provider");
        var sel = Menu.Prompt($"Pick an agent for {Markup.Escape(projectName)}:", items);
        if (sel is null) return;

        if (sel.Tag is AgentProvider chosen)
        {
            providers.SetProjectProvider(projectName, chosen.Key);
            Screen.Notice($"[green]{Markup.Escape(projectName)} agent set to[/] [cyan1]{Markup.Escape(chosen.Name)}[/]");
        }
        else
        {
            providers.SetProjectProvider(projectName, null);
            Screen.Notice($"[green]{Markup.Escape(projectName)} reset to default agent.[/]");
        }
        Thread.Sleep(600);
    }
}

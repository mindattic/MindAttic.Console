using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

/// <summary>
/// Global settings for CLI development: the default coding agent, the model each
/// agent CLI runs with, and per-project agent overrides.
/// </summary>
public sealed class SettingsMenu(SettingsStore store, AgentProviderRegistry providers)
{
    // Tag wrapper so a model row is distinguishable from the per-project rows
    // (which tag the Project itself) when both carry providers/projects.
    private sealed record ModelTarget(AgentProvider Provider);

    public void Run()
    {
        var resumeIndex = 0;
        while (true)
        {
            var settings = store.Load();
            var defaultKey = providers.CurrentDefaultKey();
            var all = providers.All();
            // Snapshot the provider keys once so the per-project label resolution
            // below is plain in-memory work — calling EffectiveProviderKey per
            // project would reload settings N times to render a single screen.
            var knownKeys = all.Select(a => a.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var items = new List<MenuItem>
            {
                new() { Name = "Default Agent", Description = defaultKey, Tag = "default" }
            };

            // Model per agent CLI — the headline of this screen.
            foreach (var p in all)
            {
                var model = ProviderModel.Get(p.RunCommand);
                items.Add(new MenuItem
                {
                    Name = $"{p.Name} model",
                    Description = string.IsNullOrWhiteSpace(model) ? "(CLI default)" : model!,
                    Tag = new ModelTarget(p)
                });
            }

            // Per-project agent override.
            foreach (var p in ProjectRoster.Sorted(settings))
            {
                // Reflect what would actually launch: a blank override shows the
                // default, a valid override shows itself, and a stale override
                // (a key no longer in AgentProviders) is flagged rather than
                // shown as if live — the launcher falls back to the default.
                var label =
                    string.IsNullOrWhiteSpace(p.Provider) ? $"default: {defaultKey}"
                    : knownKeys.Contains(p.Provider!) ? p.Provider!
                    : $"default: {defaultKey}  (ignoring unknown '{p.Provider}')";
                items.Add(new MenuItem { Name = p.Name, Description = label, Tag = p });
            }

            Screen.Header("Settings");
            var result = Menu.PromptWithKeys("Configure CLI development:", items, customKeys: null, initialIndex: resumeIndex);
            resumeIndex = result.Index;
            var sel = result.Selected;
            if (sel is null) return;

            switch (sel.Tag)
            {
                case "default":
                    PickDefaultProvider();
                    break;
                case ModelTarget target:
                    EditModel(target.Provider);
                    break;
                case Project project:
                    PickProjectProvider(project);
                    break;
            }
        }
    }

    private void EditModel(AgentProvider provider)
    {
        var current = ProviderModel.Get(provider.RunCommand);
        AgentProviderRegistry.KnownModels.TryGetValue(provider.Key, out var knownModels);

        var items = new List<MenuItem>();
        foreach (var (id, label) in knownModels ?? [])
        {
            items.Add(new MenuItem
            {
                Name = id,
                Description = string.Equals(id, current, StringComparison.OrdinalIgnoreCase)
                    ? $"{label}  ← current"
                    : label,
                Tag = id
            });
        }
        items.Add(new() { Name = "Enter model id…", Description = "type the exact CLI model id", Tag = "custom" });
        items.Add(new() { Name = "Use CLI default", Description = "remove --model so the CLI picks", Tag = "clear" });

        Screen.Header("Settings", provider.Name, "Model");
        AnsiConsole.MarkupLine(
            $"  Current model: [cyan1]{Markup.Escape(string.IsNullOrWhiteSpace(current) ? "(CLI default)" : current!)}[/]");
        AnsiConsole.MarkupLine($"  [grey50]Command:[/] [grey50]{Markup.Escape(provider.RunCommand)}[/]");
        AnsiConsole.WriteLine();

        var sel = Menu.Prompt($"Set the model for {Markup.Escape(provider.Name)}:", items);
        if (sel is null) return;

        string? model;
        switch (sel.Tag)
        {
            case "clear":
                model = null;
                break;
            case "custom":
                AnsiConsole.WriteLine();
                model = AnsiConsole.Prompt(
                    new TextPrompt<string>("  [cyan1]Model id[/]:")
                        .AllowEmpty()
                        .DefaultValue(current ?? "")
                        .ShowDefaultValue(false));
                break;
            default:
                model = (string)sel.Tag!;
                break;
        }

        providers.SetModel(provider.Key, model);

        var saved = ProviderModel.Get(providers.ByKey(provider.Key)?.RunCommand);
        Screen.Notice(string.IsNullOrWhiteSpace(saved)
            ? $"[green]{Markup.Escape(provider.Name)} now uses the CLI default model.[/]"
            : $"[green]{Markup.Escape(provider.Name)} model set to[/] [cyan1]{Markup.Escape(saved)}[/]");
        Thread.Sleep(800);
    }

    private void PickDefaultProvider()
    {
        var currentKey = providers.CurrentDefaultKey();
        var items = providers.All()
            .Select(p => new MenuItem
            {
                Name = p.Name,
                Description = string.Equals(p.Key, currentKey, StringComparison.OrdinalIgnoreCase)
                    ? $"current - {p.RunCommand}"
                    : p.RunCommand,
                Tag = p
            })
            .ToList();

        Screen.Header("Settings", "Default");
        var sel = Menu.Prompt("Pick the default agent for all projects:", items);
        if (sel is null) return;

        providers.SetDefault(((AgentProvider)sel.Tag!).Key);
        Screen.Notice($"[green]Default agent set to[/] [cyan1]{Markup.Escape(((AgentProvider)sel.Tag!).Name)}[/]");
        Thread.Sleep(600);
    }

    private void PickProjectProvider(Project project)
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

        Screen.Header("Settings", project.Name);
        var sel = Menu.Prompt($"Pick an agent for {Markup.Escape(project.Name)}:", items);
        if (sel is null) return;

        if (sel.Tag is AgentProvider chosen)
        {
            providers.SetProjectProvider(project.Name, chosen.Key);
            Screen.Notice($"[green]{Markup.Escape(project.Name)} agent set to[/] [cyan1]{Markup.Escape(chosen.Name)}[/]");
        }
        else
        {
            providers.SetProjectProvider(project.Name, null);
            Screen.Notice($"[green]{Markup.Escape(project.Name)} reset to default agent.[/]");
        }
        Thread.Sleep(600);
    }
}

using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

/// <summary>
/// Global settings for CLI development: the model each agent CLI runs with.
/// </summary>
public sealed class SettingsMenu(AgentProviderRegistry providers)
{
    // Tag wrapper so a model row's Tag type doesn't collide with a raw AgentProvider.
    private sealed record ModelTarget(AgentProvider Provider);

    public void Run()
    {
        var resumeIndex = 0;
        while (true)
        {
            var all = providers.All();
            var items = new List<MenuItem>();

            // Model per agent CLI — the headline (and only row) of this screen.
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

            Screen.Header("Settings");
            var result = Menu.PromptWithKeys("Configure CLI development:", items, customKeys: null, initialIndex: resumeIndex);
            resumeIndex = result.Index;
            var sel = result.Selected;
            if (sel is null) return;

            if (sel.Tag is ModelTarget target)
                EditModel(target.Provider);
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
}

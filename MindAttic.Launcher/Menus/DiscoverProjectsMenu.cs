using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

/// <summary>
/// Startup walk-through that offers to add git repos found under the MindAttic
/// root that aren't yet in the roster. For each one the user confirms the git URL
/// (pre-filled from <c>origin</c>), picks a tab color from the curated palette
/// (or types a custom hex), and the project is appended to settings.json with a
/// matching <c>MindAttic-&lt;Name&gt;</c> Windows Terminal color scheme written
/// automatically.
/// </summary>
public sealed class DiscoverProjectsMenu(SettingsStore store, GitService git, WindowsTerminalSchemes schemes)
{
    // Tag on the "Custom hex…" row, distinguishing it from the PaletteColor rows.
    private static readonly object CustomHexTag = new();
    private static readonly IReadOnlySet<ConsoleKey> SkipKeys =
        new HashSet<ConsoleKey> { ConsoleKey.S, ConsoleKey.N };

    private enum Outcome { Added, Skipped, SkipAll, Never }

    public void Run()
    {
        var root = ExePath.WorkspaceRoot;
        if (!Directory.Exists(root)) return;

        var candidates = ProjectDiscovery.FindUnregistered(store.Load(), root);
        if (candidates.Count == 0) return;

        try
        {
            foreach (var repo in candidates)
            {
                var outcome = AddOne(repo, candidates.Count);
                if (outcome == Outcome.SkipAll) break;
                if (outcome == Outcome.Never)
                    store.Update(s => (s.DiscoveryIgnore ??= []).Add(repo.Path));
            }
        }
        catch (InvalidOperationException)
        {
            // stdin redirected (piped run / CI) — no interactive user to drive the
            // prompts. Bail cleanly.
        }
    }

    private Outcome AddOne(DiscoveredRepo repo, int total)
    {
        var settings = store.Load();

        Screen.Header("Discover Projects");
        AnsiConsole.MarkupLine($"  [green]New repo found:[/] [cyan1]{Markup.Escape(repo.Name)}[/] [grey50]at {Markup.Escape(repo.Path)}[/]");
        AnsiConsole.MarkupLine($"  [grey50]({total} unregistered repo(s) under the workspace.)[/]");
        AnsiConsole.WriteLine();

        var url = AnsiConsole.Prompt(BuildUrlPrompt(git.RemoteUrl(repo.Path)));

        var colorResult = PickColor(settings);
        if (colorResult.Back) return Outcome.Skipped;
        if (colorResult.CustomKey == ConsoleKey.S) return Outcome.SkipAll;
        if (colorResult.CustomKey == ConsoleKey.N) return Outcome.Never;

        string hex;
        if (ReferenceEquals(colorResult.Selected?.Tag, CustomHexTag))
        {
            if (!TryPromptCustomHex(out hex)) return Outcome.Skipped;
        }
        else if (colorResult.Selected?.Tag is PaletteColor pc)
        {
            hex = pc.Hex;
        }
        else
        {
            return Outcome.Skipped;
        }

        var description = AnsiConsole.Prompt(
            new TextPrompt<string>("  [cyan1]Description[/] [grey50](optional)[/]:").AllowEmpty());

        var schemeName = ColorPalette.SchemeName(repo.Name);
        var project = new Project
        {
            Name = repo.Name,
            Repo = repo.Name,
            RepoUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            Path = repo.Path,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            OpenWith = DetectOpenWith(repo.Path),
            TabColor = hex,
            ColorScheme = schemeName,
        };

        store.Update(s => (s.Projects ??= []).Add(project));

        var wrote = schemes.EnsureScheme(settings.WindowsTerminalSettingsPath, schemeName, ColorPalette.DarkBackground(hex));

        Screen.Notice($"[green]Added[/] [cyan1]{Markup.Escape(repo.Name)}[/] [grey50]({hex}).[/]");
        if (!wrote)
            Screen.Notice($"[yellow]Note:[/] [grey50]couldn't write the[/] {Markup.Escape(schemeName)} [grey50]scheme to the Windows Terminal settings — the tab color still applies.[/]");
        Screen.PressAnyKey();
        return Outcome.Added;
    }

    private static TextPrompt<string> BuildUrlPrompt(string? detectedOrigin)
    {
        var prompt = new TextPrompt<string>("  [cyan1]Git URL[/]:").AllowEmpty();
        return string.IsNullOrWhiteSpace(detectedOrigin) ? prompt : prompt.DefaultValue(detectedOrigin);
    }

    private static MenuResult PickColor(AppSettings settings)
    {
        var used = UsedColors(settings);
        var items = ColorPalette.Colors
            .Select(c => new MenuItem
            {
                Name = c.Name,
                Description = used.TryGetValue(c.Hex, out var owner)
                    ? $"{c.Hex}  (used by {owner})"
                    : c.Hex,
                Tag = c,
            })
            .ToList();
        items.Add(new MenuItem { Name = "Custom hex…", Description = "enter any #RRGGBB", Tag = CustomHexTag });

        return Menu.PromptWithKeys(
            "  Pick a tab color:",
            items,
            SkipKeys,
            extraHint: "[green]S[/][grey50] skip all  [/][green]N[/][grey50] never ask  [/][green]Esc[/][grey50] skip this[/]");
    }

    private static bool TryPromptCustomHex(out string hex)
    {
        while (true)
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("  [cyan1]Hex color[/] [grey50](#RRGGBB, blank to cancel)[/]:").AllowEmpty());
            if (string.IsNullOrWhiteSpace(input)) { hex = ""; return false; }
            if (ColorPalette.TryNormalizeHex(input, out hex)) return true;
            AnsiConsole.MarkupLine("  [red]Not a valid #RRGGBB hex color.[/]");
        }
    }

    // hex -> project name, for flagging palette colors already in use. First
    // project to claim a hex wins the label (good enough for a hint).
    private static Dictionary<string, string> UsedColors(AppSettings settings)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in settings.Projects ?? [])
            if (!string.IsNullOrWhiteSpace(p.TabColor) && !map.ContainsKey(p.TabColor!))
                map[p.TabColor!] = p.Name;
        return map;
    }

    private static string? DetectOpenWith(string repoPath)
    {
        try
        {
            var slnx = Directory.GetFiles(repoPath, "*.slnx");
            if (slnx.Length == 1) return Path.GetFileName(slnx[0]);
            var sln = Directory.GetFiles(repoPath, "*.sln");
            if (sln.Length == 1) return Path.GetFileName(sln[0]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort convenience; a probe failure just leaves OpenWith null.
        }
        return null;
    }
}

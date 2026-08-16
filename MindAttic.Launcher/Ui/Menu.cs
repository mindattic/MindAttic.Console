using Spectre.Console;

namespace MindAttic.Launcher.Ui;

public static class Menu
{
    /// <summary>
    /// Selection prompt with real keyboard handling: arrow keys / Home / End to
    /// move, Enter to select, Esc to go back. Returns the selected item, or
    /// null if the user pressed Esc (or the "&lt; Back" sentinel).
    /// </summary>
    public static MenuItem? Prompt(string title, IReadOnlyList<MenuItem> items, bool allowBack = true)
    {
        var result = PromptWithKeys(title, items, customKeys: null, allowBack: allowBack);
        return result.Selected;
    }

    /// <summary>
    /// Like <see cref="Prompt"/>, but also surfaces a configured set of "custom"
    /// keys (e.g. S/N to skip color prompts during project discovery). When a
    /// custom key is pressed the prompt exits with <see cref="MenuResult.CustomKey"/>
    /// set and <see cref="MenuResult.KeyTarget"/> pointing at the highlighted item,
    /// so the caller can react and re-enter the loop with fresh items.
    /// </summary>
    public static MenuResult PromptWithKeys(
        string title,
        IReadOnlyList<MenuItem> items,
        IReadOnlySet<ConsoleKey>? customKeys,
        bool allowBack = true,
        string? extraHint = null,
        CancellationToken refreshToken = default,
        int initialIndex = 0)
    {
        var rows = new List<MenuItem>(items);
        if (allowBack)
        {
            rows.Add(new MenuItem
            {
                Name = "< Back",
                Description = "return to previous menu",
                Tag = MenuSentinel.Back
            });
        }

        if (rows.Count == 0) return new MenuResult { Back = true };

        var nameWidth = rows.Max(i => i.Name.Length);
        var index = Math.Clamp(initialIndex, 0, rows.Count - 1);
        // CursorTop throws on redirected stdout; fall back to 0 so the re-render
        // path still works (it just overwrites from the top of the stream).
        var startTop = 0;
        try { startTop = System.Console.CursorTop; } catch { /* see above */ }

        var priorCursorVisible = true;
        try { priorCursorVisible = System.Console.CursorVisible; } catch { /* not supported on this stream */ }

        try
        {
            System.Console.CursorVisible = false;
            Render(title, rows, nameWidth, index, extraHint);

            while (true)
            {
                // Poll for a keypress so a refreshToken cancellation (e.g. the
                // 60-second main-menu redraw timer) can interrupt the wait.
                ConsoleKeyInfo? next = null;
                while (next is null)
                {
                    bool available;
                    try { available = System.Console.KeyAvailable; }
                    catch (InvalidOperationException) { return new MenuResult { Back = true }; }

                    if (available)
                    {
                        try { next = System.Console.ReadKey(intercept: true); }
                        catch (InvalidOperationException) { return new MenuResult { Back = true }; }
                    }
                    else if (refreshToken.IsCancellationRequested)
                    {
                        return new MenuResult { Timeout = true, Index = index };
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
                var keyInfo = next.Value;
                var navigated = false;

                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        index = index == 0 ? rows.Count - 1 : index - 1;
                        navigated = true;
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        index = (index + 1) % rows.Count;
                        navigated = true;
                        break;
                    case ConsoleKey.Home:
                        index = 0;
                        navigated = true;
                        break;
                    case ConsoleKey.End:
                        index = rows.Count - 1;
                        navigated = true;
                        break;
                    case ConsoleKey.Enter:
                    {
                        var chosen = rows[index];
                        if (ReferenceEquals(chosen.Tag, MenuSentinel.Back))
                            return new MenuResult { Back = true, Index = index };
                        return new MenuResult { Selected = chosen, Index = index };
                    }
                    case ConsoleKey.Escape:
                        if (!allowBack) break;
                        return new MenuResult { Back = true, Index = index };
                    default:
                        if (customKeys is not null && customKeys.Contains(keyInfo.Key))
                        {
                            var target = rows[index];
                            if (!ReferenceEquals(target.Tag, MenuSentinel.Back))
                                return new MenuResult { CustomKey = keyInfo.Key, KeyTarget = target, Index = index };
                        }
                        break;
                }

                if (!navigated) continue;

                System.Console.SetCursorPosition(0, startTop);
                System.Console.Write("\x1b[J");
                Render(title, rows, nameWidth, index, extraHint);
            }
        }
        finally
        {
            try { System.Console.CursorVisible = priorCursorVisible; } catch { /* see above */ }
        }
    }

    private static void Render(string title, IReadOnlyList<MenuItem> rows, int nameWidth, int highlighted, string? extraHint)
    {
        AnsiConsole.MarkupLine(title);
        AnsiConsole.WriteLine();
        for (var i = 0; i < rows.Count; i++)
            RenderItem(rows[i], nameWidth, i == highlighted);
        AnsiConsole.WriteLine();
        var hints = "[green]Up/Down[/][grey50] navigate  [/][green]Enter[/][grey50] select  [/][green]Esc[/][grey50] back[/]";
        if (!string.IsNullOrWhiteSpace(extraHint))
            hints = $"{extraHint}  [grey50]·[/]  {hints}";
        AnsiConsole.MarkupLine($"  {hints}");
    }

    private static void RenderItem(MenuItem item, int nameWidth, bool highlighted)
    {
        // Name + Description come from user-controlled settings (project names,
        // RunCommand strings, etc.) — escape before interpolating into markup so
        // a stray '[' doesn't crash the prompt with InvalidOperationException.
        // Pad on the raw string so column alignment matches the visible width,
        // since Markup.Escape doubles '[' which renders back as a single char.
        var name = Markup.Escape(item.Name.PadRight(nameWidth));
        var desc = string.IsNullOrWhiteSpace(item.Description)
            ? ""
            : $"  [grey50]{Markup.Escape(item.Description)}[/]";
        if (highlighted)
            AnsiConsole.MarkupLine($"[yellow]> {name}[/]{desc}");
        else
            AnsiConsole.MarkupLine($"  {name}{desc}");
    }
}

public sealed class MenuResult
{
    public MenuItem? Selected { get; init; }
    public bool Back { get; init; }
    public bool Timeout { get; init; }
    public ConsoleKey? CustomKey { get; init; }
    public MenuItem? KeyTarget { get; init; }
    public int Index { get; init; }
}

/// <summary>Marker tags for menu sentinels (Back, etc.).</summary>
public static class MenuSentinel
{
    public static readonly object Back = new();
}

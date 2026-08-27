using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Launcher.Ui;
using Spectre.Console;

namespace MindAttic.Launcher.Menus;

public sealed class OpenProjectMenu(SettingsStore store, AgentProviderRegistry providers, WindowsTerminalLauncher wt)
{
    public void Run()
    {
        var resumeIndex = 0;
        while (true)
        {
            var settings = store.Load();
            var sortedProjects = ProjectRoster.Sorted(settings);

            var items = sortedProjects.Select(p => new MenuItem
            {
                Name = p.Name,
                Description = p.Description ?? "",
                Tag = p
            }).ToList();

            Screen.Header("Open Project Tab");
            if (sortedProjects.Count == 0)
            {
                Screen.Notice("[grey50]No projects configured yet.[/]");
                Screen.PressAnyKey();
                return;
            }

            var result = Menu.PromptWithKeys(
                "Choose a project to open:",
                items,
                customKeys: null,
                initialIndex: resumeIndex);

            resumeIndex = result.Index;

            if (result.Back) return;

            if (result.Selected is { } sel)
            {
                var project = (Project)sel.Tag!;
                // Provider is an ephemeral, per-launch choice — nothing is
                // persisted. Esc/Back from the picker just returns here.
                var provider = PickProvider(project);
                if (provider is null) continue;

                new ProjectActionMenu(store, wt, project, provider).Run();
                continue;
            }
        }
    }

    /// <summary>
    /// Which CLI to launch this project with, decided fresh every time — Claude
    /// always sorts first (see <see cref="AgentProviderRegistry.Defaults"/>),
    /// then Codex, then Gemini, then Kimi.
    /// </summary>
    private AgentProvider? PickProvider(Project project)
    {
        var items = providers.All()
            .Select(p => new MenuItem { Name = p.Name, Description = p.RunCommand, Tag = p })
            .ToList();

        Screen.Header("Open Project Tab", project.Name);
        var sel = Menu.Prompt($"Open {Markup.Escape(project.Name)} with which agent?", items);
        return sel?.Tag as AgentProvider;
    }
}

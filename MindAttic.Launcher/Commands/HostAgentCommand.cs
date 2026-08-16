using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using MindAttic.Launcher.Interop;
using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using Spectre.Console.Cli;

namespace MindAttic.Launcher.Commands;

/// <summary>
/// Per-tab agent host: resolves where to root the agent (a registered project by
/// <c>--name</c>, or any directory by <c>--path</c> — the latter is how Overlord
/// roots one session at the whole MindAttic workspace), splits the provider's
/// RunCommand into argv, sets the tab title and starts the title-pinner, then
/// execs the agent with inherited stdio.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by Spectre.Console.Cli")]
public sealed class HostAgentCommand : Command<HostAgentCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--name <NAME>")]
        [Description("Project name as listed in settings.json. Optional when --path is given.")]
        public string Name { get; init; } = "";

        [CommandOption("--path <PATH>")]
        [Description("Root the agent at this directory instead of a registered project (Overlord uses the MindAttic workspace root). Takes precedence over --name.")]
        public string? Path { get; init; }

        [CommandOption("--title <TITLE>")]
        [Description("Tab title (defaults to --name).")]
        public string? Title { get; init; }

        [CommandOption("--provider <PROVIDER>")]
        [Description("Override the project's configured agent provider.")]
        public string? Provider { get; init; }

        [CommandOption("--prompt <PROMPT>")]
        [Description("Seed the agent's first turn with this text (e.g. the Overlord order). Pre-fills the input; the CLI does not auto-submit.")]
        public string? Prompt { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = new SettingsStore();
        var registry = new AgentProviderRegistry(store);

        // Two ways to root the agent. --path wins: it hosts a session at an
        // arbitrary directory with no roster entry, which is how Overlord opens
        // one agent over the whole MindAttic workspace. Otherwise --name looks a
        // registered project up the usual way.
        Project? project = null;
        string workingDir;
        if (!string.IsNullOrWhiteSpace(settings.Path))
        {
            workingDir = settings.Path!;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Name))
        {
            project = ProjectRoster.FindByName(store.Load(), settings.Name);
            if (project is null)
            {
                System.Console.Error.WriteLine($"Unknown project: {settings.Name}");
                return 1;
            }
            workingDir = project.Path;
        }
        else
        {
            System.Console.Error.WriteLine("--name or --path is required.");
            return 64;
        }

        AgentProvider provider;
        if (!string.IsNullOrWhiteSpace(settings.Provider))
        {
            // An explicit --provider that doesn't resolve is a caller bug (a
            // typo), not "quietly launch the project default" — that would hide
            // the mistake and start the wrong agent. Fail loudly instead.
            var requested = registry.ByKey(settings.Provider);
            if (requested is null)
            {
                System.Console.Error.WriteLine($"Unknown provider: {settings.Provider}");
                return 4;
            }
            provider = requested;
        }
        else
        {
            // No explicit --provider: there's no per-project override anymore
            // (that's now an ephemeral, menu-time choice — see OpenProjectMenu),
            // so a direct/scripted `host` call always resolves to the
            // first-listed provider (Claude, per AgentProviderRegistry.Defaults).
            provider = registry.Current();
        }

        var title = !string.IsNullOrWhiteSpace(settings.Title) ? settings.Title!
                  : !string.IsNullOrWhiteSpace(settings.Name) ? settings.Name
                  : System.IO.Path.GetFileName(workingDir.TrimEnd(System.IO.Path.DirectorySeparatorChar));

        var parts = CommandLineParser.Split(provider.RunCommand);
        if (parts.Length == 0)
        {
            System.Console.Error.WriteLine($"Provider {provider.Key} has an empty RunCommand.");
            return 2;
        }

        using var pinner = new TitlePinner(title);
        // Per-tab pipe lets the launcher's "Remote Control" menu type
        // /remote-control into every running Claude/Codex tab at once via
        // ConsoleInputInjector. Started before Process.Start so a broadcast
        // landing during agent startup isn't missed.
        using var inputPipe = new HostInputPipeServer(provider.Key);

        var psi = new ProcessStartInfo(parts[0])
        {
            UseShellExecute = false,
            // Working directory matches the wt tab so the agent starts in the
            // right root; menu code already passes -d to wt, but be defensive in
            // case someone runs `mindattic host` directly.
            WorkingDirectory = Directory.Exists(workingDir) ? workingDir : Environment.CurrentDirectory
        };
        for (var i = 1; i < parts.Length; i++) psi.ArgumentList.Add(parts[i]);
        // A seed prompt is the agent's first positional arg — `claude <flags>
        // "<order>"` / `codex <flags> "<order>"`. Both start interactive with
        // the prompt loaded; Overlord uses this so one session at the workspace
        // root opens with the order ready to send.
        if (!string.IsNullOrWhiteSpace(settings.Prompt))
            psi.ArgumentList.Add(settings.Prompt!);

        // Push this provider's API key (if Vault has one) to wherever its CLI
        // expects it — an env var for Gemini, config.toml for Kimi. A missing
        // Vault entry is a no-op; the CLI falls back to its own configured auth.
        ProviderCredentials.Apply(psi, provider.Key);

        try
        {
            using var p = Process.Start(psi)
                          ?? throw new InvalidOperationException($"Failed to start {parts[0]}");
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine($"Failed to launch {provider.Key}: {ex.Message}");
            return 3;
        }
    }
}

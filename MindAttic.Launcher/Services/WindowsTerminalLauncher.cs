using System.Diagnostics;
using MindAttic.Launcher.Models;

namespace MindAttic.Launcher.Services;

/// <summary>
/// Builds and invokes <c>wt</c> command lines. Centralises tab title / color /
/// scheme handling so menu code never quotes <c>wt</c> args by hand.
/// </summary>
public sealed class WindowsTerminalLauncher
{
    public sealed class Tab
    {
        public string? Title { get; init; }
        public string? WorkingDirectory { get; init; }
        public string? TabColor { get; init; }
        public string? ColorScheme { get; init; }
        public bool SuppressApplicationTitle { get; init; } = true;
        public required IReadOnlyList<string> Command { get; init; }
    }

    public void Open(Tab tab)
    {
        if (tab.Command.Count == 0)
            throw new ArgumentException("Tab.Command must contain at least the executable.", nameof(tab));

        var psi = new ProcessStartInfo("wt")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in BuildArgList(tab)) psi.ArgumentList.Add(a);

        // wt forks the new tab and exits immediately. Dispose the launcher
        // Process handle right away — callers were leaking it.
        using var launcher = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start wt.");
    }

    /// <summary>
    /// Like <see cref="Open"/> but launches <c>wt</c> elevated via UAC so the new
    /// tab runs as Administrator. If the user cancels the UAC prompt the call returns
    /// silently (Win32 error 1223 is swallowed).
    /// </summary>
    public void OpenElevated(Tab tab)
    {
        if (tab.Command.Count == 0)
            throw new ArgumentException("Tab.Command must contain at least the executable.", nameof(tab));

        var psi = new ProcessStartInfo("wt")
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var a in BuildArgList(tab)) psi.ArgumentList.Add(a);

        try { using var _ = Process.Start(psi); }
        catch (System.ComponentModel.Win32Exception) { /* user cancelled UAC */ }
    }

    private static IEnumerable<string> BuildArgList(Tab tab)
    {
        yield return "-w"; yield return "0"; yield return "new-tab";
        if (!string.IsNullOrWhiteSpace(tab.Title))
            { yield return "--title"; yield return tab.Title; }
        if (tab.SuppressApplicationTitle)
            yield return "--suppressApplicationTitle";
        if (!string.IsNullOrWhiteSpace(tab.WorkingDirectory))
            { yield return "-d"; yield return tab.WorkingDirectory; }
        if (!string.IsNullOrWhiteSpace(tab.TabColor))
            { yield return "--tabColor"; yield return tab.TabColor; }
        if (!string.IsNullOrWhiteSpace(tab.ColorScheme))
            { yield return "--colorScheme"; yield return tab.ColorScheme; }
        yield return "--";
        foreach (var c in tab.Command) yield return c;
    }

    /// <summary>
    /// Builds an "agent host" tab — invokes `mindattic host …` for the given
    /// project + provider. When <paramref name="prompt"/> is set it's passed
    /// through as <c>--prompt</c> so the agent opens with that text pre-loaded.
    /// </summary>
    public Tab BuildAgentTab(Project project, AgentProvider provider, string hostExePath, string? prompt = null)
    {
        var agentTitle = $"{project.TabTitle} [{provider.Key}]";
        var command = new List<string>
        {
            hostExePath,
            "host",
            "--name", project.Name,
            "--title", agentTitle,
            "--provider", provider.Key
        };
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            command.Add("--prompt");
            command.Add(prompt);
        }

        return new Tab
        {
            Title = agentTitle,
            WorkingDirectory = project.Path,
            TabColor = project.TabColor,
            ColorScheme = project.ColorScheme,
            // TitlePinner toggles "Paused" into the tab title when the agent
            // isn't running; --suppressApplicationTitle would block those writes.
            SuppressApplicationTitle = false,
            Command = command
        };
    }

    /// <summary>
    /// Builds an "agent host" tab rooted at an arbitrary directory rather than a
    /// registered project — invokes `mindattic host --path …` (e.g. the Status
    /// tab, rooted at the whole MindAttic workspace). When <paramref name="prompt"/>
    /// is set it's forwarded as <c>--prompt</c> so the session opens with that
    /// text pre-loaded.
    /// </summary>
    public Tab BuildAgentTabAtPath(
        string title, string workingDirectory, AgentProvider provider, string hostExePath,
        string? tabColor = null, string? colorScheme = null, string? prompt = null)
    {
        var command = new List<string>
        {
            hostExePath,
            "host",
            "--path", workingDirectory,
            "--title", title,
            "--provider", provider.Key
        };
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            command.Add("--prompt");
            command.Add(prompt);
        }

        return new Tab
        {
            Title = title,
            WorkingDirectory = workingDirectory,
            TabColor = tabColor,
            ColorScheme = colorScheme,
            // Match BuildAgentTab: let TitlePinner write "Paused" into the title.
            SuppressApplicationTitle = false,
            Command = command
        };
    }

    public Tab BuildRunCommandTab(Project project)
    {
        // A blank RunCommand as `cmd /c ""` opens a tab that flashes and dies with
        // no explanation. Keep the pane open (`/k`) with a message instead so the
        // user can see why nothing ran — RunProjectMenu filters these out, but the
        // method is public and shouldn't manufacture a silently-broken tab.
        IReadOnlyList<string> command = string.IsNullOrWhiteSpace(project.RunCommand)
            ? ["cmd", "/k", $"echo No RunCommand configured for {project.Name}."]
            : ["cmd", "/c", project.RunCommand];
        return new Tab
        {
            Title = project.TabTitle,
            WorkingDirectory = project.Path,
            TabColor = project.TabColor,
            ColorScheme = project.ColorScheme,
            Command = command
        };
    }

    public Tab BuildCmdTab(string workingDirectory) => new()
    {
        Title = "Command Prompt (Administrator)",
        WorkingDirectory = workingDirectory,
        Command = ["cmd"]
    };

    public Tab BuildPowerShellTab(string workingDirectory) => new()
    {
        Title = "PowerShell (Administrator)",
        WorkingDirectory = workingDirectory,
        Command = ["powershell"]
    };

    /// <summary>
    /// Builds a tab that runs every MindAttic.Deploy category back-to-back
    /// via <c>cmd /k</c> so the pane stays open after the run for the user
    /// to read the per-batch summaries. <paramref name="commandLine"/> comes
    /// from <see cref="DeployService.BuildDeployAllCommandLine(string)"/>.
    /// </summary>
    public Tab BuildDeployAllTab(string workingDirectory, string commandLine) => new()
    {
        Title = "MindAttic.Deploy — all",
        WorkingDirectory = workingDirectory,
        Command = ["cmd", "/k", commandLine]
    };
}

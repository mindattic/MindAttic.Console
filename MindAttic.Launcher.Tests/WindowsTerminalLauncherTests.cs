using System.Linq;
using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using NUnit.Framework;

namespace MindAttic.Launcher.Tests;

[TestFixture]
public sealed class WindowsTerminalLauncherTests
{
    private static readonly Project Project = new() { Name = "Workspace", Path = @"D:\Projects\MindAttic" };
    private static readonly AgentProvider Provider = new() { Key = "Claude", Name = "Claude Code", RunCommand = "claude" };

    [Test]
    public void BuildAgentTab_appends_prompt_as_last_host_arg_when_provided()
    {
        var tab = new WindowsTerminalLauncher().BuildAgentTab(Project, Provider, "mindattic.exe", "tidy every repo");

        Assert.That(tab.Command, Does.Contain("--prompt"));
        // The order text must be the final argument so the host forwards it as
        // the agent's positional seed prompt.
        Assert.That(tab.Command[^2], Is.EqualTo("--prompt"));
        Assert.That(tab.Command[^1], Is.EqualTo("tidy every repo"));
    }

    [Test]
    public void BuildAgentTab_omits_prompt_when_null_or_blank()
    {
        var none  = new WindowsTerminalLauncher().BuildAgentTab(Project, Provider, "mindattic.exe");
        var blank = new WindowsTerminalLauncher().BuildAgentTab(Project, Provider, "mindattic.exe", "   ");

        Assert.That(none.Command, Does.Not.Contain("--prompt"));
        Assert.That(blank.Command, Does.Not.Contain("--prompt"));
    }

    [Test]
    public void BuildAgentTabAtPath_hosts_by_path_not_name()
    {
        var tab = new WindowsTerminalLauncher()
            .BuildAgentTabAtPath("Status [Claude]", @"D:\Projects\MindAttic", Provider, "mindattic.exe");

        // Path mode roots the host at a directory with no roster entry — it must
        // pass --path (never --name).
        Assert.That(tab.Command, Does.Contain("--path"));
        Assert.That(tab.Command, Does.Not.Contain("--name"));
        var pathIdx = tab.Command.ToList().IndexOf("--path");
        Assert.That(tab.Command[pathIdx + 1], Is.EqualTo(@"D:\Projects\MindAttic"));
        Assert.That(tab.WorkingDirectory, Is.EqualTo(@"D:\Projects\MindAttic"));
    }

    [Test]
    public void BuildAgentTabAtPath_appends_prompt_as_last_host_arg_when_provided()
    {
        var tab = new WindowsTerminalLauncher()
            .BuildAgentTabAtPath("Status [Claude]", @"D:\Projects\MindAttic", Provider, "mindattic.exe",
                prompt: "tidy every repo");

        Assert.That(tab.Command[^2], Is.EqualTo("--prompt"));
        Assert.That(tab.Command[^1], Is.EqualTo("tidy every repo"));
    }

    [Test]
    public void BuildAgentTabAtPath_omits_prompt_when_null_or_blank()
    {
        var none  = new WindowsTerminalLauncher()
            .BuildAgentTabAtPath("Status", @"D:\Projects\MindAttic", Provider, "mindattic.exe");
        var blank = new WindowsTerminalLauncher()
            .BuildAgentTabAtPath("Status", @"D:\Projects\MindAttic", Provider, "mindattic.exe", prompt: "  ");

        Assert.That(none.Command, Does.Not.Contain("--prompt"));
        Assert.That(blank.Command, Does.Not.Contain("--prompt"));
    }
}

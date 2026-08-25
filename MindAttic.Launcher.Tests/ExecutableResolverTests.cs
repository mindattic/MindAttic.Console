using MindAttic.Launcher.Services;
using NUnit.Framework;

namespace MindAttic.Launcher.Tests;

[TestFixture]
public sealed class ExecutableResolverTests
{
    private string _dir = "";
    private string _originalPath = "";

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ExecutableResolverTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);
        _originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        Environment.SetEnvironmentVariable("PATH", _dir + Path.PathSeparator + _originalPath);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        Directory.Delete(_dir, recursive: true);
    }

    [Test]
    public void Resolve_finds_cmd_shim_when_no_exe_exists()
    {
        var shim = Path.Combine(_dir, "gemini.cmd");
        File.WriteAllText(shim, "@echo off\r\n");

        Assert.That(ExecutableResolver.Resolve("gemini"), Is.EqualTo(shim).IgnoreCase);
    }

    [Test]
    public void Resolve_prefers_exe_over_cmd_per_PATHEXT_order()
    {
        var exe = Path.Combine(_dir, "claude.exe");
        File.WriteAllText(Path.Combine(_dir, "claude.cmd"), "@echo off\r\n");
        File.WriteAllText(exe, "");

        Assert.That(ExecutableResolver.Resolve("claude"), Is.EqualTo(exe).IgnoreCase);
    }

    [Test]
    public void Resolve_returns_original_name_when_not_found()
    {
        Assert.That(ExecutableResolver.Resolve("definitely-not-a-real-command"),
            Is.EqualTo("definitely-not-a-real-command"));
    }

    [Test]
    public void Resolve_leaves_paths_untouched()
    {
        var withSlash = Path.Combine(_dir, "unresolved-tool");
        Assert.That(ExecutableResolver.Resolve(withSlash), Is.EqualTo(withSlash));
    }

    [Test]
    public void Resolve_leaves_names_that_already_have_an_extension_untouched()
    {
        Assert.That(ExecutableResolver.Resolve("gemini.ps1"), Is.EqualTo("gemini.ps1"));
    }

    [Test]
    public void Resolve_returns_blank_input_unchanged()
    {
        Assert.That(ExecutableResolver.Resolve(""), Is.EqualTo(""));
    }
}

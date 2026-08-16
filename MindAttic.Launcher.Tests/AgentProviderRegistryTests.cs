using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Vault.Settings;
using NUnit.Framework;

namespace MindAttic.Launcher.Tests;

[TestFixture]
public sealed class AgentProviderRegistryTests
{
    private string tempRoot = "";
    private SettingsStore store = null!;
    private AgentProviderRegistry registry = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var json = new JsonSettingsStore<AppSettings>(Path.Combine(tempRoot, "settings"));
        store = new SettingsStore(json);
        registry = new AgentProviderRegistry(store);

        store.Save(new AppSettings
        {
            AgentProviders =
            {
                new AgentProvider { Key = "Claude", Name = "Claude Code",  RunCommand = "claude --dangerously-skip-permissions" },
                new AgentProvider { Key = "Codex",  Name = "OpenAI Codex", RunCommand = "codex --dangerously-bypass-approvals-and-sandbox" }
            }
        });
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(tempRoot, recursive: true); } catch { }
    }

    [Test]
    public void Defaults_are_returned_when_AgentProviders_is_empty()
    {
        store.Save(new AppSettings());
        Assert.That(new AgentProviderRegistry(store).All(), Has.Count.EqualTo(AgentProviderRegistry.Defaults.Count));
    }

    [Test]
    public void Defaults_order_is_Claude_Codex_Gemini_Kimi()
    {
        // Claude always sorts first, then Codex, Gemini, Kimi — this order drives
        // both the fallback provider (Current()) and the ephemeral picker shown
        // when a project tab is opened (OpenProjectMenu).
        var keys = AgentProviderRegistry.Defaults.Select(p => p.Key).ToList();
        Assert.That(keys, Is.EqualTo(new[] { "Claude", "Codex", "Gemini", "Kimi" }));
    }

    [Test]
    public void Current_is_the_first_configured_provider()
    {
        Assert.That(registry.Current().Key, Is.EqualTo("Claude"));
        Assert.That(registry.CurrentDefaultKey(), Is.EqualTo("Claude"));
    }

    [Test]
    public void SetModel_appends_flag_and_persists()
    {
        registry.SetModel("Claude", "claude-sonnet-4-6");
        var claude = new AgentProviderRegistry(store).ByKey("Claude")!;
        Assert.That(claude.RunCommand, Is.EqualTo("claude --dangerously-skip-permissions --model claude-sonnet-4-6"));
    }

    [Test]
    public void SetModel_blank_clears_the_flag()
    {
        registry.SetModel("Claude", "claude-opus-4-8");
        registry.SetModel("Claude", "");
        var claude = new AgentProviderRegistry(store).ByKey("Claude")!;
        Assert.That(claude.RunCommand, Is.EqualTo("claude --dangerously-skip-permissions"));
    }

    [Test]
    public void SetModel_unknown_key_throws()
    {
        Assert.Throws<ArgumentException>(() => registry.SetModel("Bogus", "x"));
    }

    [Test]
    public void KnownModels_Claude_includes_Fable5_and_current_models()
    {
        Assert.That(AgentProviderRegistry.KnownModels, Contains.Key("Claude"));
        var ids = AgentProviderRegistry.KnownModels["Claude"].Select(m => m.Id).ToList();
        Assert.That(ids, Contains.Item("claude-fable-5"));
        Assert.That(ids, Contains.Item("claude-opus-4-8"));
        Assert.That(ids, Contains.Item("claude-sonnet-5"));
        Assert.That(ids[0], Is.EqualTo("claude-fable-5"), "Fable 5 must be first (newest)");
    }

    [Test]
    public void SetModel_materializes_defaults_when_none_configured()
    {
        store.Save(new AppSettings());
        var fresh = new AgentProviderRegistry(store);
        fresh.SetModel("Claude", "model-under-test");

        // The static Defaults must be untouched — only the persisted copy changes.
        Assert.That(AgentProviderRegistry.Defaults.First(p => p.Key == "Claude").RunCommand,
            Does.Not.Contain("model-under-test"));
        Assert.That(new AgentProviderRegistry(store).ByKey("Claude")!.RunCommand,
            Does.Contain("--model model-under-test"));
    }
}

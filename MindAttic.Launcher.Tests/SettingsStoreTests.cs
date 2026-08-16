using MindAttic.Launcher.Models;
using MindAttic.Launcher.Services;
using MindAttic.Vault.Settings;
using NUnit.Framework;

namespace MindAttic.Launcher.Tests;

[TestFixture]
public sealed class SettingsStoreTests
{
    private string tempRoot = "";
    private SettingsStore subject = null!;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var store = new JsonSettingsStore<AppSettings>(Path.Combine(tempRoot, "settings"));
        subject = new SettingsStore(store);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(tempRoot, recursive: true); }
        catch { }
    }

    [Test]
    public void Load_returns_defaults_when_vault_is_empty()
    {
        var settings = subject.Load();

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.Projects, Is.Empty);
        Assert.That(settings.AgentProviders, Is.Empty);
    }

    [Test]
    public void Save_persists_settings_round_trip()
    {
        var input = new AppSettings
        {
            Projects =
            {
                new Project { Name = "Alpha", Path = @"C:\a" },
                new Project { Name = "Beta",  Path = @"C:\b" }
            }
        };

        subject.Save(input);
        var roundTripped = subject.Load();

        Assert.That(roundTripped.Projects, Has.Count.EqualTo(2));
        Assert.That(roundTripped.Projects[0].Name, Is.EqualTo("Alpha"));
    }

    [Test]
    public void Save_preserves_unknown_top_level_and_per_project_keys()
    {
        // The live settings.json carries keys this app's models don't define
        // (a top-level "mobile" block, per-project "mobileEnabled") — written by
        // a sibling tool / future schema. A naive POCO round-trip would drop them
        // on the next Save (e.g. changing a provider), silently wiping the user's
        // mobile config. They must survive a load -> save cycle.
        Directory.CreateDirectory(Path.GetDirectoryName(subject.SettingsFilePath)!);
        File.WriteAllText(subject.SettingsFilePath, """
        {
            "provider": "Claude",
            "mobile": { "serverUrl": "http://127.0.0.1:7780", "enabled": true },
            "agentProviders": [],
            "projects": [
                { "name": "Alpha", "path": "C:\\a", "mobileEnabled": true }
            ]
        }
        """);

        var loaded = subject.Load();
        subject.Save(loaded); // simulate any settings mutation

        var raw = File.ReadAllText(subject.SettingsFilePath);
        Assert.Multiple(() =>
        {
            Assert.That(raw, Does.Contain("mobile"), "top-level 'mobile' block was dropped on save");
            Assert.That(raw, Does.Contain("127.0.0.1:7780"), "mobile contents were dropped on save");
            Assert.That(raw, Does.Contain("mobileEnabled"), "per-project 'mobileEnabled' was dropped on save");
        });
    }

    [Test]
    public void Load_seeds_from_legacy_file_when_vault_is_empty()
    {
        var legacyPath = Path.Combine(tempRoot, "legacy-settings.json");
        File.WriteAllText(legacyPath, """
        {
            "Provider": "Claude",
            "AgentProviders": [
                { "Key": "Claude", "Name": "Claude Code", "RunCommand": "claude" }
            ],
            "Projects": [
                { "Name": "Alpha", "Path": "C:\\a", "Provider": "Claude" }
            ]
        }
        """);

        var freshStore = new JsonSettingsStore<AppSettings>(Path.Combine(tempRoot, "fresh-settings"));
        var seeded = new SettingsStore(freshStore, legacyPath);

        var loaded = seeded.Load();

        Assert.That(loaded.Projects, Has.Count.EqualTo(1));
        Assert.That(loaded.AgentProviders, Has.Count.EqualTo(1));
    }
}

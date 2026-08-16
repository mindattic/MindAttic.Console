using MindAttic.Launcher.Services;
using NUnit.Framework;

namespace MindAttic.Launcher.Tests;

[TestFixture]
public sealed class KimiConfigSyncTests
{
    // Shaped like the real ~/.kimi-code/config.toml WITHOUT an oauth sub-table
    // under managed:kimi-code — the state where setting api_key is safe. Also
    // carries a sibling moonshot-cn table so tests prove the splice stays
    // inside the matching table and doesn't bleed into its neighbor.
    private static readonly string Sample = string.Join('\n',
    [
        "default_model = \"kimi-code/k3\"",
        "",
        "[providers.moonshot-cn]",
        "type = \"kimi\"",
        "api_key = \"sk-old-moonshot-key\"",
        "base_url = \"https://api.moonshot.cn/v1\"",
        "",
        "[providers.\"managed:kimi-code\"]",
        "type = \"kimi\"",
        "api_key = \"\"",
        "base_url = \"https://api.kimi.com/coding/v1\"",
    ]);

    // Same as Sample, but managed:kimi-code already carries an oauth sub-table —
    // the real shape Kimi ships by default (OAuth login, no api_key). Kimi
    // itself refuses to start with both api_key and oauth set on one provider.
    private static readonly string SampleWithOAuth = Sample + "\n" + string.Join('\n',
    [
        "",
        "[providers.\"managed:kimi-code\".oauth]",
        "storage = \"file\"",
        "key = \"oauth/kimi-code\"",
    ]);

    [Test]
    public void TrySetApiKey_rewrites_the_matching_tables_key_only()
    {
        var found = KimiConfigSync.TrySetApiKey(Sample, "managed:kimi-code", "sk-new-key", out var result);

        Assert.That(found, Is.True);
        Assert.That(result, Does.Contain("[providers.\"managed:kimi-code\"]\ntype = \"kimi\"\napi_key = \"sk-new-key\""));
        // The sibling moonshot-cn table's key must survive untouched.
        Assert.That(result, Does.Contain("api_key = \"sk-old-moonshot-key\""));
    }

    [Test]
    public void TrySetApiKey_is_idempotent_when_value_already_matches()
    {
        KimiConfigSync.TrySetApiKey(Sample, "managed:kimi-code", "sk-new-key", out var once);
        var found = KimiConfigSync.TrySetApiKey(once, "managed:kimi-code", "sk-new-key", out var twice);

        Assert.That(found, Is.True);
        Assert.That(twice, Is.EqualTo(once));
    }

    [Test]
    public void TrySetApiKey_refuses_a_provider_already_on_oauth()
    {
        // Kimi rejects a provider with both api_key and oauth set ("mutually
        // exclusive — remove one") and fails to start entirely. SampleWithOAuth
        // already has a managed:kimi-code oauth sub-table, so this must be a
        // hard no-op — never silently create a config Kimi will refuse to load.
        var found = KimiConfigSync.TrySetApiKey(SampleWithOAuth, "managed:kimi-code", "sk-new-key", out var result);

        Assert.That(found, Is.False);
        Assert.That(result, Is.EqualTo(SampleWithOAuth));
    }

    [Test]
    public void TrySetApiKey_returns_false_when_table_missing()
    {
        var found = KimiConfigSync.TrySetApiKey(Sample, "no-such-provider", "sk-x", out var result);

        Assert.That(found, Is.False);
        Assert.That(result, Is.EqualTo(Sample));
    }

    [Test]
    public void TrySetApiKey_returns_false_for_empty_contents()
    {
        var found = KimiConfigSync.TrySetApiKey("", "managed:kimi-code", "sk-x", out var result);

        Assert.That(found, Is.False);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void EnsureApiKey_writes_the_file_only_when_the_value_changes()
    {
        var path = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N") + ".toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, Sample);

            Assert.That(KimiConfigSync.EnsureApiKey(path, "managed:kimi-code", "sk-new-key"), Is.True);
            Assert.That(File.ReadAllText(path), Does.Contain("api_key = \"sk-new-key\""));

            // Second call with the same key is a no-op — idempotent, no rewrite.
            Assert.That(KimiConfigSync.EnsureApiKey(path, "managed:kimi-code", "sk-new-key"), Is.False);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void EnsureApiKey_refuses_a_provider_already_on_oauth()
    {
        var path = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N") + ".toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, SampleWithOAuth);

            Assert.That(KimiConfigSync.EnsureApiKey(path, "managed:kimi-code", "sk-new-key"), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(SampleWithOAuth));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void EnsureApiKey_is_false_for_missing_file()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N") + ".toml");
        Assert.That(KimiConfigSync.EnsureApiKey(missingPath, "managed:kimi-code", "sk-x"), Is.False);
    }

    [Test]
    public void EnsureApiKey_is_false_for_blank_key()
    {
        var path = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N") + ".toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, Sample);
            Assert.That(KimiConfigSync.EnsureApiKey(path, "managed:kimi-code", "  "), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(Sample));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

using System.Diagnostics;
using MindAttic.Launcher.Services;
using MindAttic.Vault.Credentials;
using NUnit.Framework;

namespace MindAttic.Launcher.Tests;

[TestFixture]
public sealed class ProviderCredentialsTests
{
    private sealed class FakeCredentialStore(Dictionary<string, string>? seed = null) : ICredentialStore
    {
        private readonly Dictionary<string, string> keys = new(seed ?? new(), StringComparer.OrdinalIgnoreCase);

        public string Directory => "(fake)";
        public string ProvidersFilePath => "(fake)";
        public bool ProvidersFileExists() => keys.Count > 0;
        public string? GetKey(string providerId) => keys.GetValueOrDefault(providerId);
        public void SetKey(string providerId, string apiKey) => keys[providerId] = apiKey;
        public Dictionary<string, string> LoadAll() => new(keys, StringComparer.OrdinalIgnoreCase);
        public List<string> ListProviders() => keys.Keys.ToList();
        public Dictionary<string, string> LoadAllRaw() => new(StringComparer.OrdinalIgnoreCase);
        public void SaveAllRaw(IDictionary<string, string> providers) { }
        public void SaveRaw(string providerId, string rawProviderJson) { }
    }

    [Test]
    public void Apply_sets_the_env_var_for_a_provider_with_a_vault_key()
    {
        var store = new FakeCredentialStore(new() { ["gemini"] = "AIza-test-key" });
        var psi = new ProcessStartInfo("gemini");

        ProviderCredentials.Apply(psi, "Gemini", store);

        Assert.That(psi.Environment["GEMINI_API_KEY"], Is.EqualTo("AIza-test-key"));
    }

    [Test]
    public void Apply_is_a_noop_when_vault_has_no_key_for_the_provider()
    {
        var store = new FakeCredentialStore();
        var psi = new ProcessStartInfo("gemini");

        ProviderCredentials.Apply(psi, "Gemini", store);

        Assert.That(psi.Environment.ContainsKey("GEMINI_API_KEY"), Is.False);
    }

    [Test]
    public void Apply_never_forwards_a_key_for_a_provider_with_no_injection_mapping()
    {
        // Codex isn't in the env-var map and isn't Kimi — a Vault entry for it
        // (however it got there) must never be pushed anywhere.
        var store = new FakeCredentialStore(new() { ["codex"] = "sk-should-be-ignored" });
        var psi = new ProcessStartInfo("codex");

        ProviderCredentials.Apply(psi, "Codex", store);

        Assert.That(psi.Environment.Values, Does.Not.Contain("sk-should-be-ignored"));
    }

    [Test]
    public void Apply_does_not_throw_for_Kimi_even_when_config_toml_is_absent()
    {
        var store = new FakeCredentialStore(new() { ["kimi"] = "sk-kimi-test" });
        var psi = new ProcessStartInfo("kimi");
        // A path under a fresh temp dir — this must NEVER be the real
        // KimiConfigSync.DefaultConfigPath (~/.kimi-code/config.toml). Hitting
        // the real file here previously overwrote a working Kimi install with a
        // throwaway test value; every Kimi-path test must redirect the config
        // path explicitly instead of relying on the machine not having one.
        var missingConfigPath = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N"), "config.toml");

        Assert.DoesNotThrow(() => ProviderCredentials.Apply(psi, "Kimi", store, missingConfigPath));
    }

    [Test]
    public void Apply_syncs_the_kimi_key_into_the_given_config_path_only()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MindAttic.Launcher.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var configPath = Path.Combine(dir, "config.toml");
        try
        {
            File.WriteAllText(configPath, string.Join('\n',
            [
                "[providers.\"managed:kimi-code\"]",
                "type = \"kimi\"",
                "api_key = \"\"",
            ]));

            var store = new FakeCredentialStore(new() { ["kimi"] = "sk-kimi-real" });
            var psi = new ProcessStartInfo("kimi");

            ProviderCredentials.Apply(psi, "Kimi", store, configPath);

            Assert.That(File.ReadAllText(configPath), Does.Contain("api_key = \"sk-kimi-real\""));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

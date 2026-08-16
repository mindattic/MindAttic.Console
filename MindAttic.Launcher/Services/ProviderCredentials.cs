using System.Diagnostics;
using MindAttic.Vault.Credentials;

namespace MindAttic.Launcher.Services;

/// <summary>
/// Pushes each agent provider's API key — resolved from the shared MindAttic LLM
/// keyring (<see cref="LlmCredentialStore"/>, HOUSE-LAW-3) — to wherever that
/// provider's CLI expects to find it, right before it's launched. Gemini reads
/// <c>GEMINI_API_KEY</c> directly from its process environment; Kimi only reads
/// its own <c>config.toml</c>, so that file is synced instead (see
/// <see cref="KimiConfigSync"/>). A missing/blank Vault entry is a no-op — the
/// CLI falls back to however it's already configured (its own login, an
/// existing config.toml key, etc.), never a hard failure.
/// </summary>
public static class ProviderCredentials
{
    /// <summary>Provider keys whose CLI reads its API key directly from this env var.</summary>
    private static readonly IReadOnlyDictionary<string, string> EnvVarByProviderKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gemini"] = "GEMINI_API_KEY",
        };

    /// <summary>
    /// Resolves <paramref name="providerKey"/>'s Vault entry and applies it —
    /// as an env var on <paramref name="psi"/>, or via <see cref="KimiConfigSync"/>
    /// for Kimi. <paramref name="store"/> defaults to the real shared keyring;
    /// <paramref name="kimiConfigPath"/> defaults to the real
    /// <c>~/.kimi-code/config.toml</c>. Tests pass both overrides so a run never
    /// touches the real Vault file or the user's actual Kimi config.
    /// </summary>
    public static void Apply(
        ProcessStartInfo psi,
        string providerKey,
        ICredentialStore? store = null,
        string? kimiConfigPath = null)
    {
        var key = (store ?? LlmCredentialStore.Default).GetKey(providerKey);
        if (string.IsNullOrWhiteSpace(key)) return;

        if (EnvVarByProviderKey.TryGetValue(providerKey, out var envVar))
        {
            psi.Environment[envVar] = key;
            return;
        }

        if (string.Equals(providerKey, "Kimi", StringComparison.OrdinalIgnoreCase))
            KimiConfigSync.EnsureApiKey(kimiConfigPath ?? KimiConfigSync.DefaultConfigPath, KimiConfigSync.ProviderTable, key);
    }
}

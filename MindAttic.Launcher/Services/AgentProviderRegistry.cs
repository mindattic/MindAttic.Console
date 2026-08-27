using MindAttic.Launcher.Models;

namespace MindAttic.Launcher.Services;

public sealed class AgentProviderRegistry(SettingsStore store)
{
    public static IReadOnlyList<AgentProvider> Defaults { get; } =
    [
        new AgentProvider { Key = "Claude", Name = "Claude Code",  RunCommand = "claude --dangerously-skip-permissions --model claude-sonnet-5" },
        new AgentProvider { Key = "Codex",  Name = "OpenAI Codex", RunCommand = "codex --dangerously-bypass-approvals-and-sandbox" },
        new AgentProvider { Key = "Gemini", Name = "Google Gemini", RunCommand = "gemini --yolo" },
        new AgentProvider { Key = "Kimi",   Name = "Kimi Code",    RunCommand = "kimi --yolo" }
    ];

    /// <summary>Known model IDs per provider key, newest first, shown as presets in the model picker.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<(string Id, string Label)>> KnownModels { get; } =
        new Dictionary<string, IReadOnlyList<(string Id, string Label)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Claude"] = new (string Id, string Label)[]
            {
                ("claude-fable-5",    "Fable 5    · 1M ctx · highest intelligence"),
                ("claude-opus-4-8",   "Opus 4.8   · 1M ctx · reasoning + coding"),
                ("claude-opus-4-7",   "Opus 4.7   · 1M ctx"),
                ("claude-opus-4-6",   "Opus 4.6   · 1M ctx"),
                ("claude-sonnet-5",   "Sonnet 5   · 1M ctx · balanced"),
                ("claude-sonnet-4-6", "Sonnet 4.6 · 1M ctx"),
                ("claude-haiku-4-5",  "Haiku 4.5  · 200K ctx · fast"),
            }
        };

    public IReadOnlyList<AgentProvider> All() => ProvidersFrom(store.Load());

    private static IReadOnlyList<AgentProvider> ProvidersFrom(AppSettings settings)
    {
        // Coalesce: an explicit "agentProviders": null in settings.json
        // deserializes to null and would NRE here (same trap as Projects).
        var configured = (settings.AgentProviders ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Key)
                     && !string.IsNullOrWhiteSpace(a.Name)
                     && !string.IsNullOrWhiteSpace(a.RunCommand))
            .ToList();
        return configured.Count > 0 ? configured : Defaults;
    }

    public AgentProvider? ByKey(string? key) => ByKey(All(), key);

    private static AgentProvider? ByKey(IReadOnlyList<AgentProvider> providers, string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : providers.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// There is no persisted "default provider" anymore — the interactive menu
    /// asks fresh every time a project tab is opened (see OpenProjectMenu). This
    /// is only for callers with no launch-time choice to make (Status, a bare
    /// `host` with no --provider) and always resolves to the
    /// first-listed provider (Claude, per <see cref="Defaults"/> order).
    /// </summary>
    public string CurrentDefaultKey() => All()[0].Key;

    public AgentProvider Current() => All()[0];

    /// <summary>
    /// Sets the model for a provider by rewriting the <c>--model</c> token in its
    /// RunCommand (see <see cref="ProviderModel"/>). A blank/null model clears the
    /// flag so the CLI uses its own default.
    /// </summary>
    public void SetModel(string providerKey, string? model)
    {
        if (ByKey(providerKey) is null) throw new ArgumentException($"Unknown provider: {providerKey}", nameof(providerKey));

        store.Update(s =>
        {
            // Defaults live in code, not the file. If nothing's configured yet,
            // materialize them (cloned, so the static Defaults aren't mutated)
            // so the model edit has a persisted home.
            if (s.AgentProviders is null || s.AgentProviders.Count == 0)
                s.AgentProviders = Defaults.Select(p => p.Clone()).ToList();

            var p = s.AgentProviders.FirstOrDefault(a =>
                        string.Equals(a.Key, providerKey, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"Unknown provider: {providerKey}", nameof(providerKey));
            p.RunCommand = ProviderModel.Set(p.RunCommand, model);
        });
    }
}

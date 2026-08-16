using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Launcher.Models;

public sealed class AppSettings
{
    public string? WindowsTerminalSettingsPath { get; set; }
    public List<AgentProvider> AgentProviders { get; set; } = new();
    public List<Project> Projects { get; set; } = new();

    /// <summary>
    /// Full paths of git repos under the MindAttic root that the user chose to
    /// never auto-add to the roster ("never ask again" during startup discovery).
    /// <see cref="Services.ProjectDiscovery"/> excludes these from its candidate
    /// list so a deliberately-unmanaged repo isn't re-offered every launch.
    /// </summary>
    public List<string>? DiscoveryIgnore { get; set; }

    /// <summary>
    /// Captures top-level settings keys this app doesn't model (e.g. the "mobile"
    /// block a sibling tool writes) so a Save — triggered by any in-app change —
    /// round-trips them instead of silently wiping them. See
    /// <see cref="Project.Extra"/> for the per-project equivalent.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

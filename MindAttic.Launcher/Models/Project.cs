using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Launcher.Models;

public sealed class Project
{
    public string Name { get; set; } = "";
    public string? Repo { get; set; }

    /// <summary>
    /// The git remote URL (origin) this repo clones from. Captured when a project
    /// is auto-discovered under the MindAttic root; not consumed by launch logic,
    /// but recorded so the roster carries each repo's canonical clone URL.
    /// </summary>
    public string? RepoUrl { get; set; }

    public string Path { get; set; } = "";
    public string? Description { get; set; }
    public string? OpenWith { get; set; }
    public string? RunCommand { get; set; }
    public string? TabAlias { get; set; }
    public string? TabColor { get; set; }
    public string? ColorScheme { get; set; }

    /// <summary>
    /// SQL Server instance hosting this project's databases (e.g. <c>localhost</c>,
    /// <c>.\SQLEXPRESS</c>). Null/blank falls back to
    /// <see cref="Services.SqlBackupService.DefaultInstance"/>. Only consulted when
    /// <see cref="Databases"/> is non-empty.
    /// </summary>
    public string? SqlServer { get; set; }

    /// <summary>
    /// Names of the SQL Server databases that belong to this project. The backup
    /// step issues a full <c>BACKUP DATABASE</c> (schema + data) for each into the
    /// dated backup folder. Empty means the project has no databases to back up.
    /// </summary>
    public List<string> Databases { get; set; } = new();

    /// <summary>
    /// Captures per-project keys this app doesn't model (e.g. "mobileEnabled")
    /// so they survive a settings Save instead of being silently dropped. See
    /// <see cref="AppSettings.Extra"/>.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    private const string NamePrefix = "MindAttic.";

    /// <summary>
    /// Short label for the wt tab title. An explicit <see cref="TabAlias"/> wins;
    /// otherwise the shared "MindAttic." prefix is stripped so "MindAttic.Legion"
    /// shows as "Legion" — tab titles have very little room and the prefix is
    /// redundant when every project carries it.
    /// </summary>
    [JsonIgnore]
    public string TabTitle =>
        !string.IsNullOrWhiteSpace(TabAlias) ? TabAlias!
        : Name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase) ? Name[NamePrefix.Length..]
        : Name;
}

namespace MindAttic.Launcher.Services;

/// <summary>
/// Splices the Vault-stored Kimi API key into a provider entry of
/// <c>~/.kimi-code/config.toml</c> — a targeted text edit (locate the table,
/// replace its <c>api_key</c> line) rather than a TOML parse-and-reserialize, so
/// every other table, model, and comment in the user's file survives untouched.
/// Same idempotent-splice contract as <see cref="WindowsTerminalSchemes"/>
/// (MCO-LAW-4): a no-op write when the key already matches.
/// </summary>
public static class KimiConfigSync
{
    /// <summary>The provider table whose <c>api_key</c> this app keeps in sync with Vault.</summary>
    public const string ProviderTable = "managed:kimi-code";

    public static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi-code", "config.toml");

    /// <summary>
    /// Locates <c>[providers."{table}"]</c> in <paramref name="contents"/> and
    /// rewrites its <c>api_key = "..."</c> line to <paramref name="apiKey"/>.
    /// Returns false (with <paramref name="result"/> = the original contents)
    /// when the table or its <c>api_key</c> line can't be found — this app never
    /// invents config structure the CLI didn't already declare — or when the
    /// provider already has an <c>.oauth</c> sub-table: Kimi rejects a provider
    /// with both an <c>api_key</c> and <c>oauth</c> set ("mutually exclusive —
    /// remove one"), so a provider already on OAuth is left alone rather than
    /// silently written into a config Kimi itself will refuse to load. Returns
    /// true whenever the key is already present with the right value or gets
    /// rewritten to it (mirrors <see cref="WindowsTerminalSchemes.TryInsertScheme"/>:
    /// true means "now correct", not "a write happened").
    /// </summary>
    public static bool TrySetApiKey(string contents, string table, string apiKey, out string result)
    {
        result = contents;
        if (string.IsNullOrEmpty(contents)) return false;

        var header = $"[providers.\"{table}\"]";
        var headerIdx = contents.IndexOf(header, StringComparison.Ordinal);
        if (headerIdx < 0) return false;

        // Never create an api_key + oauth combination Kimi itself rejects at
        // startup — a provider already authenticated via OAuth stays on OAuth.
        if (contents.Contains($"providers.\"{table}\".oauth", StringComparison.Ordinal) ||
            contents.Contains($"providers.{table}.oauth", StringComparison.Ordinal))
            return false;

        // The table's block runs until the next top-level "[" header or EOF.
        var blockStart = headerIdx + header.Length;
        var nextHeaderIdx = contents.IndexOf("\n[", blockStart, StringComparison.Ordinal);
        var blockEnd = nextHeaderIdx < 0 ? contents.Length : nextHeaderIdx;

        var keyIdx = contents.IndexOf("api_key", blockStart, StringComparison.Ordinal);
        if (keyIdx < 0 || keyIdx >= blockEnd) return false;

        var lineEnd = contents.IndexOf('\n', keyIdx);
        if (lineEnd < 0 || lineEnd > blockEnd) lineEnd = blockEnd;

        var oldLine = contents[keyIdx..lineEnd].TrimEnd('\r');
        var newLine = $"api_key = \"{apiKey}\"";
        if (oldLine == newLine) return true; // already correct — no-op

        result = contents[..keyIdx] + newLine + contents[lineEnd..];
        return true;
    }

    /// <returns>
    /// True if the file was rewritten; false on a no-op (key already matched) or
    /// any failure — missing key/file, the table/line not found, or an IO error.
    /// </returns>
    public static bool EnsureApiKey(string configPath, string table, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !File.Exists(configPath)) return false;

        try
        {
            var contents = File.ReadAllText(configPath);
            if (!TrySetApiKey(contents, table, apiKey, out var updated)) return false;
            if (updated == contents) return false; // already correct

            File.WriteAllText(configPath, updated);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

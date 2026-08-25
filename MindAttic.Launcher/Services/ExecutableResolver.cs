namespace MindAttic.Launcher.Services;

/// <summary>
/// <c>Process.Start</c> with <c>UseShellExecute = false</c> calls <c>CreateProcessW</c>
/// directly, which only appends <c>.exe</c> when resolving a bare command name — unlike
/// <c>cmd.exe</c>, it never walks <c>PATHEXT</c> to find a <c>.cmd</c>/<c>.bat</c> shim.
/// npm-installed CLIs (codex, gemini, ...) install as <c>&lt;name&gt;.cmd</c> /
/// <c>&lt;name&gt;.ps1</c> shims with no <c>.exe</c>, so a provider's <c>RunCommand</c> like
/// <c>"codex --yolo"</c> resolves fine when typed in a terminal but fails
/// <see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/> with
/// "the system cannot find the file specified." This mimics <c>cmd.exe</c>'s PATH +
/// PATHEXT search so those shims resolve too; <c>CreateProcessW</c> can launch a resolved
/// <c>.cmd</c>/<c>.bat</c> file directly (it special-cases those extensions internally).
/// </summary>
public static class ExecutableResolver
{
    /// <summary>
    /// Resolves <paramref name="command"/> to a full path via PATH + PATHEXT, the same
    /// order <c>cmd.exe</c> uses. Returns <paramref name="command"/> unchanged if it already
    /// names a path, already carries an extension, or can't be resolved — so the caller's
    /// existing "fail loudly" handling still applies to a genuinely missing command.
    /// </summary>
    public static string Resolve(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return command;
        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            return command;
        if (Path.HasExtension(command)) return command;

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        var searchDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Prepend(Environment.CurrentDirectory);

        foreach (var dir in searchDirs)
        {
            foreach (var ext in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir, command + ext);
                }
                catch (ArgumentException)
                {
                    continue; // a malformed PATH entry shouldn't abort the whole search
                }

                if (File.Exists(candidate)) return candidate;
            }
        }

        return command;
    }
}

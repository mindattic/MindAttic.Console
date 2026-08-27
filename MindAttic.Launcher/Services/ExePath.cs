using System.Diagnostics;

namespace MindAttic.Launcher.Services;

/// <summary>
/// Resolves paths to the running and canonical MindAttic.Launcher executables.
/// <see cref="Self"/> is whatever exe is currently executing (e.g. the Debug
/// bin during dev). <see cref="Release"/> is the published single-file exe at
/// <c>artifacts\MindAttic.Launcher.exe</c> — what the launcher .bat invokes
/// and what "Restart" respawns. <see cref="EnsureFresh"/> shells out to
/// <c>scripts\ensure-fresh.ps1</c> to republish when source has changed.
/// </summary>
public static class ExePath
{
    public static string Self => Environment.ProcessPath ?? "MindAttic.Launcher";

    public static string Release
    {
        get
        {
            var root = FindRepoRoot();
            return root is null
                ? Self
                : Path.Combine(root, "artifacts", "MindAttic.Launcher.exe");
        }
    }

    /// <summary>
    /// The MindAttic.Launcher repo root (the directory containing
    /// <c>scripts\publish.ps1</c>), walking up from the running exe. Null when
    /// the exe lives outside the repo (e.g. an installed copy). Used by
    /// <see cref="BuildFreshness"/> to compare the build against the latest commit.
    /// </summary>
    public static string? RepoRoot => FindRepoRoot();

    /// <summary>
    /// The directory under which every MindAttic project repo lives (e.g.
    /// <c>D:\Projects\MindAttic</c>) — the parent of <see cref="RepoRoot"/>.
    /// Falls back to the historical path when running outside a checkout.
    /// </summary>
    public static string WorkspaceRoot
    {
        get
        {
            var root = FindRepoRoot();
            if (root is null) return @"D:\Projects\MindAttic";
            return Path.GetDirectoryName(root) ?? root;
        }
    }

    public static void EnsureFresh()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var script = Path.Combine(root, "scripts", "ensure-fresh.ps1");
        if (!File.Exists(script)) return;

        var psi = new ProcessStartInfo("powershell")
        {
            UseShellExecute = false,
            WorkingDirectory = root,
            // Capture the publish chatter instead of letting it scribble over the
            // Spectre menu we return to. RedirectStandardOutput also means we must
            // drain both streams, or a verbose publish can deadlock on a full pipe.
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);

        try
        {
            using var p = Process.Start(psi);
            if (p is not null)
            {
                var drainOut = Task.Run(() => p.StandardOutput.ReadToEnd());
                var drainErr = Task.Run(() => p.StandardError.ReadToEnd());
                // Cap the wait so a wedged publish (nuget stall, AV scan) can't
                // freeze Restart / Open Project Tab. On timeout, kill and move on
                // — the caller still tries to launch whatever exe currently exists.
                if (!p.WaitForExit((int)TimeSpan.FromMinutes(2).TotalMilliseconds))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                }
                try { Task.WaitAll(new[] { drainOut, drainErr }, TimeSpan.FromSeconds(5)); } catch { }
            }
        }
        catch
        {
            // Best-effort republish — if powershell is missing or the script
            // fails, the caller still tries to launch whatever exe exists.
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "scripts", "publish.ps1")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}

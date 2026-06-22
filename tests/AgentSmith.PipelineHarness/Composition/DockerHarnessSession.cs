using System.Diagnostics;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: per-test bare git repo + working copy on the host, both wiped
/// on DisposeAsync so a failed test doesn't leak temp dirs. The bare repo
/// is bind-mounted into the sandbox via SandboxSpec.ExtraBinds so the
/// in-sandbox `git clone file:///bare-remote` succeeds without a network.
/// </summary>
public sealed class DockerHarnessSession : IAsyncDisposable
{
    public string BareRepoPath { get; }
    public string WorkingCopyPath { get; }
    public string InSandboxBarePath { get; }
    public string InSandboxBareUrl { get; }
    public string ExtraBind { get; }

    private DockerHarnessSession(string bare, string working, string inSandboxBare)
    {
        BareRepoPath = bare;
        WorkingCopyPath = working;
        InSandboxBarePath = inSandboxBare;
        InSandboxBareUrl = "file://" + inSandboxBare;
        ExtraBind = $"{bare}:{inSandboxBare}";
    }

    public static Task<DockerHarnessSession> CreateAsync(string fixtureSourceDir) =>
        CreateAsync(fixtureSourceDir, includePrivateFeed: false);

    public static async Task<DockerHarnessSession> CreateAsync(
        string fixtureSourceDir, bool includePrivateFeed)
    {
        var slug = Guid.NewGuid().ToString("N")[..8];
        var bare = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-bare-{slug}.git");
        var working = Path.Combine(Path.GetTempPath(), $"agentsmith-harness-work-{slug}");
        Directory.CreateDirectory(bare);
        await RunAsync("git", new[] { "init", "--bare", "--initial-branch=main", bare });
        await SeedWorkingCopyAsync(fixtureSourceDir, working, bare, includePrivateFeed);
        return new DockerHarnessSession(bare, working, $"/bare-remotes/{slug}.git");
    }

    private static async Task SeedWorkingCopyAsync(
        string fixtureSourceDir, string working, string bare, bool includePrivateFeed)
    {
        Directory.CreateDirectory(working);
        CopyDirectory(fixtureSourceDir, working);
        WriteNuGetConfig(working, includePrivateFeed);
        await RunAsync("git", new[] { "init", "--initial-branch=main", working });
        await RunAsync("git", new[] { "-C", working, "config", "user.email", "harness@noreply.local" });
        await RunAsync("git", new[] { "-C", working, "config", "user.name", "Harness" });
        await RunAsync("git", new[] { "-C", working, "add", "-A" });
        await RunAsync("git", new[] { "-C", working, "commit", "-m", "harness fixture seed" });
        await RunAsync("git", new[] { "-C", working, "remote", "add", "origin", bare });
        await RunAsync("git", new[] { "-C", working, "push", "origin", "main" });
    }

    private static void WriteNuGetConfig(string workingDir, bool includePrivateFeed)
    {
        var nuget = includePrivateFeed ? NuGetConfigPrivate : NuGetConfigPublic;
        File.WriteAllText(Path.Combine(workingDir, "nuget.config"), nuget);
    }

    private const string NuGetConfigPublic =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
        </configuration>
        """;

    private const string NuGetConfigPrivate =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
            <add key="fixture-private-feed" value="https://pkgs.dev.azure.com/agent-smith-fixtures/_packaging/private/nuget/v3/index.json" />
          </packageSources>
        </configuration>
        """;

    private static void CopyDirectory(string source, string dest)
    {
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest, StringComparison.Ordinal));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest, StringComparison.Ordinal), overwrite: true);
    }

    public bool BareHasBranch(string branchName)
    {
        var psi = new ProcessStartInfo("git", $"-c safe.bareRepository=all -C \"{BareRepoPath}\" branch --list {branchName}")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10000);
        return !string.IsNullOrWhiteSpace(stdout);
    }

    public IReadOnlyList<string> BareBranches()
    {
        var psi = new ProcessStartInfo("git", $"-c safe.bareRepository=all -C \"{BareRepoPath}\" branch --list")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10000);
        return stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.StartsWith("* ", StringComparison.Ordinal) ? line[2..] : line)
            .ToList();
    }

    private static async Task RunAsync(string command, string[] args)
    {
        var psi = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // The ambient environment injects safe.bareRepository=explicit (GIT_CONFIG_*),
        // which makes git refuse to operate on the per-test bare remote. Override it
        // on our own invocations so the harness is immune to the operator's git config.
        if (command == "git")
        {
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("safe.bareRepository=all");
        }
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(
                $"{command} {string.Join(' ', args)} exited {p.ExitCode}: {err}");
        }
    }

    public ValueTask DisposeAsync()
    {
        TryDelete(BareRepoPath);
        TryDelete(WorkingCopyPath);
        return ValueTask.CompletedTask;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; orphan dirs are tolerable in test temp.
        }
    }
}

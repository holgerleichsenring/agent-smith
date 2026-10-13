using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>p0196: hands out fresh StubSandbox instances; tracks every spawn.</summary>
internal sealed class StubSandboxFactory : ISandboxFactory
{
    public List<(SandboxSpec Spec, StubSandbox Sandbox)> Spawned { get; } = new();

    /// <summary>2026-10-02-3f06c: repo-relative paths every sandbox handed out answers "file not
    /// found" for — empty by default, so every read keeps succeeding as it always has.</summary>
    public HashSet<string> MissingPaths { get; } = new(StringComparer.Ordinal);

    /// <summary>2026-10-12-50bda: when set, every sandbox names its spec's image the way a container
    /// backend does, so the run records SandboxImages and the image preflight checks judge it.</summary>
    public bool NamesImages { get; set; }

    /// <summary>2026-10-12-f5f7c: when set, `git status --porcelain` lists what the sandbox was
    /// written, as a real working tree does — off by default, so every other run keeps reading
    /// a clean tree.</summary>
    public bool ReportsWorkingTree { get; set; }

    /// <summary>Meta files at the base the branch was cut from — null by default, so no sandbox
    /// names a base, 'git show's a file or answers the gate's NUL-separated listings, and the
    /// commit gate passes as it always would.</summary>
    public Dictionary<string, string>? BaseFiles { get; set; }

    /// <summary>2026-10-06-8565c: tracked source files, repo-relative, every sandbox lists and reads —
    /// null by default, so no init finds a source to measure.</summary>
    public Dictionary<string, string>? SourceFiles { get; set; }

    /// <summary>2026-10-13-a347a: a shell command line starting with a key exits with its code and
    /// says its output — empty by default, so every command keeps succeeding.</summary>
    public Dictionary<string, (int ExitCode, string Output)> ShellAnswers { get; } = new(StringComparer.Ordinal);

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var sandbox = new StubSandbox(
            MissingPaths, NamesImages ? spec.ToolchainImage : null, ReportsWorkingTree, BaseFiles, SourceFiles, ShellAnswers);
        Spawned.Add((spec, sandbox));
        return Task.FromResult<ISandbox>(sandbox);
    }
}

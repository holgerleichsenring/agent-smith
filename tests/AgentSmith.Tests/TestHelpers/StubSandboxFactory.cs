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

    /// <summary>2026-11-02-2b26d: every commit lands on a new head — a run that writes a second spec revision
    /// reads the head it moved to. Off by default, so every head stays the one fixed sha.</summary>
    public bool AdvancesHeadOnCommit { get; set; }

    /// <summary>2026-11-04-6768a: a git command whose space-joined argv starts with a key exits with its code
    /// and says its output — empty by default, so every git command keeps its stub answer.</summary>
    public Dictionary<string, (int ExitCode, string Output)> GitAnswers { get; } = new(StringComparer.Ordinal);

    private readonly List<(string PathPart, string CommandPrefix, (int ExitCode, string Output)? Answer)> _afterWrites = [];

    /// <summary>2026-11-18-c8e2c: once a sandbox is written a file whose path contains <paramref name="pathPart"/>, a shell
    /// command starting with <paramref name="commandPrefix"/> answers <paramref name="answer"/> — null takes the answer
    /// back — so a test can make a build red after one spec's work and green again after its fix.</summary>
    public StubSandboxFactory AnswerAfterWriting(string pathPart, string commandPrefix, (int ExitCode, string Output)? answer)
    {
        _afterWrites.Add((pathPart, commandPrefix, answer));
        return this;
    }

    /// <summary>Red with <paramref name="output"/> once a file matching <paramref name="pathPart"/> was written.</summary>
    public StubSandboxFactory RedAfterWriting(string pathPart, string commandPrefix, string output) =>
        AnswerAfterWriting(pathPart, commandPrefix, (1, output));

    private void Observe(AgentSmith.Sandbox.Wire.Step step)
    {
        if (step.Kind != AgentSmith.Sandbox.Wire.StepKind.WriteFile || step.Path is not { } path) return;
        foreach (var (part, prefix, answer) in _afterWrites.Where(a => path.Contains(a.PathPart, StringComparison.Ordinal)))
            if (answer is { } a) ShellAnswers[prefix] = a;
            else ShellAnswers.Remove(prefix);
    }

    public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken) =>
        Task.FromResult<ISandbox>(Spawn(spec, AgentSmith.Domain.Entities.Repository.WorkspaceRoot));

    /// <summary>
    /// 2026-11-25-9992b: a toolchain container of a run's workspace — it serves every repository at /work/&lt;repo&gt;,
    /// each its own tree. Listed in <see cref="Spawned"/> like any sandbox.
    /// </summary>
    public StubSandbox CreateContainer(SandboxSpec spec) => Spawn(spec, root: null);

    /// <summary>
    /// 2026-11-25-9992b: every step any spawned sandbox ran in <paramref name="repo"/>'s tree, /work/&lt;repo&gt; — a
    /// workspace container serves every repository, so a repository's work is told apart by its tree, not its sandbox.
    /// </summary>
    public IReadOnlyList<AgentSmith.Sandbox.Wire.Step> StepsIn(string repo)
    {
        var root = AgentSmith.Domain.Entities.Repository.WorkPathFor(repo);
        return [.. Spawned.SelectMany(s => s.Sandbox.RanSteps).Where(step => StubTree.RootOf(step) == root)];
    }

    private StubSandbox Spawn(SandboxSpec spec, string? root)
    {
        var sandbox = new StubSandbox(
            MissingPaths, NamesImages ? spec.ToolchainImage : null, ReportsWorkingTree, BaseFiles, SourceFiles, ShellAnswers,
            AdvancesHeadOnCommit, GitAnswers, root) { OnStep = Observe };
        Spawned.Add((spec, sandbox));
        return sandbox;
    }
}

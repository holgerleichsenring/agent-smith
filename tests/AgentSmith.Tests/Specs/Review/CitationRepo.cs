using System.Globalization;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: one real-git repository for the citation re-ground — a bare origin and a clone on its
/// default branch (main) behind an in-process sandbox. Every commit carries the date it is given and is
/// pushed, so origin/HEAD is the clone's own first-parent history.
/// </summary>
internal sealed class CitationRepo : IAsyncDisposable
{
    internal const string SpecId = "2026-10-14-1111a";
    internal static readonly DateTimeOffset T0 = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly string _root;

    private CitationRepo(string name, string root)
    {
        Name = name;
        _root = root;
        Work = Path.Combine(root, "work");
        Sandbox = new InProcessSandbox(name, Work, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
    }

    internal string Name { get; }
    internal string Work { get; }
    internal ISandbox Sandbox { get; }

    internal static async Task<CitationRepo?> CreateAsync(string name = "shop")
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return null;
        var root = Path.Combine(Path.GetTempPath(), "as-8762n-" + Guid.NewGuid().ToString("N"));
        var seed = Path.Combine(root, "seed");
        Directory.CreateDirectory(seed);
        await File.WriteAllTextAsync(Path.Combine(seed, "README.md"), $"# {name}\n");
        var repo = new CitationRepo(name, root);
        await repo.GitIn(seed, null, "init", "-q", "-b", "main");
        await repo.GitIn(seed, null, "config", "user.email", "test@example.test");
        await repo.GitIn(seed, null, "config", "user.name", "test");
        await repo.GitIn(seed, null, "add", "-A");
        await repo.GitIn(seed, T0, "commit", "-q", "-m", "seed");
        await repo.GitIn(root, null, "clone", "-q", "--bare", seed, Path.Combine(root, "origin.git"));
        await repo.GitIn(root, null, "clone", "-q", Path.Combine(root, "origin.git"), repo.Work);
        await repo.GitIn(repo.Work, null, "config", "user.email", "test@example.test");
        await repo.GitIn(repo.Work, null, "config", "user.name", "test");
        return repo;
    }

    internal static DateTimeOffset At(int hours) => T0.AddHours(hours);

    internal static string SpecPath(string id = SpecId, string state = "planned") => $".agentsmith/specs/{state}/{id}-label.yaml";

    /// <summary>A schema-shaped spec whose facts cite <paramref name="evidence"/>, one fact each.</summary>
    internal static string SpecYaml(string id, params string[] evidence) =>
        $"spec: {id}\ngoal: \"Move the order rules\"\nfacts:\n"
        + string.Concat(evidence.Select((e, i) => $"  - claim: \"fact {i + 1}\"\n    evidence: \"{e}\"\n"))
        + "steps:\n  - id: one\n    action: \"do it\"\n";

    internal static string Numbered(string stem, int count) =>
        string.Concat(Enumerable.Range(1, count).Select(i => $"{stem} line {i} of the order rules here\n"));

    internal Task WriteAsync(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Work, path))!);
        return File.WriteAllTextAsync(Path.Combine(Work, path), text);
    }

    internal Task<string> ReadAsync(string path) => File.ReadAllTextAsync(Path.Combine(Work, path));

    /// <summary>Commits everything at <paramref name="at"/>, pushes it to origin's main, answers the sha.</summary>
    internal async Task<string> CommitAsync(string message, DateTimeOffset at, bool push = true)
    {
        await GitAsync(null, "add", "-A");
        await GitAsync(at, "commit", "-q", "--allow-empty", "-m", message);
        if (push) await GitAsync(null, "push", "-q", "origin", "HEAD:main");
        return (await GitAsync(null, "rev-parse", "HEAD")).Trim();
    }

    internal Task<string> GitAsync(DateTimeOffset? at, params string[] args) => GitIn(Work, at, args);

    /// <summary>A run's pipeline over these repositories, the first carrying the spec.</summary>
    internal static PipelineContext Pipeline(params CitationRepo[] repos)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            repos.ToDictionary(r => r.Name, r => r.Sandbox, StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            repos.ToDictionary(r => r.Name, r => r.Name, StringComparer.Ordinal));
        pipeline.Set(ContextKeys.SpecRepo, repos[0].Name);
        return pipeline;
    }

    internal static DerivedSpec Spec(string yaml) =>
        new(new Application.Services.SpecDialog.SpecDraftReader().Read(yaml), "label", string.Empty, []);

    internal static SpecSet Set(SpecApproval? approval, params DerivedSpec[] specs) =>
        new("2026-10-14-1111", specs, SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Initial, T0)],
            SpecSource.BranchArtifact, Approval: approval);

    internal static SpecCitationHistory History() => new(NullLogger<SpecCitationHistory>.Instance);

    internal static SpecCitationRegrounder Regrounder() =>
        new(History(), new EvidenceReferences(), NullLogger<SpecCitationRegrounder>.Instance);

    private async Task<string> GitIn(string dir, DateTimeOffset? at, params string[] args)
    {
        var date = at?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss +0000", CultureInfo.InvariantCulture);
        var env = date is null ? null : new Dictionary<string, string> { ["GIT_AUTHOR_DATE"] = date, ["GIT_COMMITTER_DATE"] = date };
        await using var sandbox = new InProcessSandbox("git", _root, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["-C", dir, .. args], Env: env, TimeoutSeconds: 60), null, CancellationToken.None);
        result.ExitCode.Should().Be(0, $"git {string.Join(' ', args)}: {result.ErrorMessage}");
        return result.OutputContent ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        await Sandbox.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

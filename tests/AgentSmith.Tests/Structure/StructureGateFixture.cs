using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>The commit gate over real collaborators and one in-memory repository.</summary>
internal static class StructureGateFixture
{
    public const string Key = "repo";
    public const string Limits = "## Limits\n\n```yaml\nfunction_lines: 4\n```\n";

    public static StructureGate Gate(bool missingGrammar = false) => new(
        new SandboxTargets(), MergeBase(), new StructureRepoContexts(),
        Contexts(NullLogger<StructureGateContexts>.Instance),
        new StructureRepoSandboxes(MergeBase(), StructureBaselineStubs.GitReads(),
            new StructureGateFiles(
                missingGrammar ? StructureEngineFixture.MissingGrammar() : StructureEngineFixture.Engine(),
                new StructureJudge(), new SandboxFileReaderFactory(), new StructureOwnership(),
                NullLogger<StructureGateFiles>.Instance),
            NullLogger<StructureRepoSandboxes>.Instance),
        NullLogger<StructureGate>.Instance);

    /// <summary>2026-10-13-2d0c: the merge-base resolution over the real ladder.</summary>
    public static StructureMergeBase MergeBase() => new(
        new SandboxBaseLadder(new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
            NullLogger<SandboxBaseLadder>.Instance),
        new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance), new StructureGitRunner(),
        NullLogger<StructureMergeBase>.Instance);

    public static StructureGateContexts Contexts(ILogger<StructureGateContexts> logger) => new(
        StructureBaselineStubs.GitReads(), new StructureLimits(), StructureBaselineStubs.File, new StructureOwnership(),
        logger);

    /// <summary>2026-10-12-fbbda: the PR-time divergence judgment over the same gate.</summary>
    public static StructureDivergencePublisher Publisher() =>
        new(Gate(), NullLogger<StructureDivergencePublisher>.Instance);

    public static PipelineContext PipelineOf(RepoSandbox repo, params (string Name, string Workdir)[] contexts) =>
        PipelineOf(repo, [.. contexts.Select(c => new RemoteContextDiscovery(c.Name, c.Workdir, "csharp"))]);

    /// <summary>2026-10-13-50c7: the contexts as discovered, sources included.</summary>
    public static PipelineContext PipelineOf(RepoSandbox repo, IReadOnlyList<RemoteContextDiscovery> contexts)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = Key }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = repo });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [Key] = contexts,
            });
        return pipeline;
    }

    /// <summary>A repository whose 'api' context has Limits and an empty baseline at the base.</summary>
    public static RepoSandbox Repo(string baseline = "")
    {
        var repo = new RepoSandbox();
        repo.BaseFiles[".agentsmith/contexts/api/principles.md"] = Limits;
        repo.BaseFiles[".agentsmith/contexts/api/structure-baseline.tsv"] = baseline;
        return repo;
    }
}

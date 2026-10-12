using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>2026-10-06-8565d: the commit gate over real collaborators and one in-memory repository.</summary>
internal static class StructureGateFixture
{
    public const string Key = "repo";
    public const string Limits = "## Limits\n\n```yaml\nfunction_lines: 4\n```\n";

    public static StructureGate Gate(bool missingGrammar = false) => new(
        new SandboxTargets(),
        new DeliveryDiff(
            new SandboxBaseLadder(new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                NullLogger<SandboxBaseLadder>.Instance),
            new SandboxRunStartCommit(NullLogger<SandboxRunStartCommit>.Instance),
            NullLogger<DeliveryDiff>.Instance),
        new StructureGitReads(),
        new StructureGateContexts(new StructureGitReads(), new StructureLimits(), NullLogger<StructureGateContexts>.Instance),
        missingGrammar ? StructureEngineFixture.MissingGrammar() : StructureEngineFixture.Engine(),
        new StructureJudge(), new SandboxFileReaderFactory(), NullLogger<StructureGate>.Instance);

    public static PipelineContext PipelineOf(RepoSandbox repo, params (string Name, string Workdir)[] contexts)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = repo });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [Key] = [.. contexts.Select(c => new RemoteContextDiscovery(c.Name, c.Workdir, "csharp"))],
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

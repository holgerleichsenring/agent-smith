using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using Moq;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7b: several checked-out repositories, each with its own in-memory ticket branch
/// and its own last commit — what the series reader sees in a multi-repository run.
/// </summary>
internal sealed class SeriesRepoBranches
{
    internal const string Key = "azdo-4711";
    internal const string Base = "2026-10-12-0b0b";
    internal static readonly string T1 = "2026-10-12T09:00:00.0000000+00:00";
    internal static readonly string T2 = "2026-10-12T10:00:00.0000000+00:00";

    private readonly Dictionary<string, (Mock<ISandbox> Sandbox, SpecBranchFiles Files)> _repos = new(StringComparer.Ordinal);

    /// <summary>A repository that is checked out, with an empty branch.</summary>
    internal SpecBranchFiles Repo(string name)
    {
        if (_repos.TryGetValue(name, out var known)) return known.Files;
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, $"sha-{name}"));
        var files = new SpecBranchFiles { Key = Key };
        _repos[name] = (sandbox, files);
        return files;
    }

    /// <summary>Seeds a sub-series: its manifest and one spec per id (requires per id optional).</summary>
    internal void SubSeries(
        string repo, string at, IReadOnlyList<string>? members, IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, string>? requires = null, string? executed = null, string seriesBase = Base)
    {
        var repos = members is null ? string.Empty : $"series_repos: [{string.Join(", ", members)}]\n";
        var manifest = $"ticket: {Key}\n{repos}revisions:\n- number: 1\n  cause: initial derivation\n  at: '{at}'\n"
                       + $"specs:\n{string.Join("", ids.Select(id => $"- {id}\n"))}";
        Repo(repo).SeedSeries(seriesBase, manifest,
            ids.ToDictionary(id => $"{id}-label", id => Spec(id, requires?.GetValueOrDefault(id))), executed);
    }

    internal Task<SpecSetOnBranch> ReadAsync(string carrying, params string[] runRepos) =>
        ReadWithAsync(SeriesDoubles.Reader(Files()), carrying, runRepos);

    /// <summary>Reads through <paramref name="reader"/> — the composed one, in the harness.</summary>
    internal async Task<SpecSetOnBranch> ReadWithAsync(ISpecSetReader reader, string carrying, params string[] runRepos)
    {
        var pipeline = Pipeline();
        return await reader.ReadAsync(
            pipeline, new RepoConnection { Name = carrying },
            [.. runRepos.Select(r => new RepoConnection { Name = r })], new TicketKey(Key), default);
    }

    /// <summary>2026-10-12-f5f7d: a run over every checked-out repository, on the ticket branch.</summary>
    internal PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            _repos.ToDictionary(r => r.Key, r => r.Value.Sandbox.Object));
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            _repos.Keys.ToDictionary(k => k, k => k));
        pipeline.Set(ContextKeys.Repository,
            new AgentSmith.Domain.Entities.Repository(new AgentSmith.Domain.Models.BranchName("agent-smith/4711"), "https://example.invalid/r.git"));
        return pipeline;
    }

    /// <summary>Each checked-out repository's sandbox opens its own branch.</summary>
    internal ISandboxFileReaderFactory Files()
    {
        var readers = new Mock<ISandboxFileReaderFactory>();
        foreach (var (_, (sandbox, files)) in _repos)
            readers.Setup(f => f.Create(sandbox.Object)).Returns(files);
        return readers.Object;
    }

    internal static string Id(char letter) => $"{Base}{letter}";

    private static string Spec(string id, string? requires) =>
        $"spec: {id}\ngoal: g\n{(requires is null ? string.Empty : $"requires: [{requires}]\n")}done:\n  - d\n";
}

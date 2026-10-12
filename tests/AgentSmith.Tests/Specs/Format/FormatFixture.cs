using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Structure;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.Format;

/// <summary>2026-10-12-fbbdb: the format run and the spec commit over real collaborators.</summary>
internal static class FormatFixture
{
    public const string Key = "repo";
    public const string StartHead = "spec-start";

    public static SpecFormatRun Run() => new(
        new SandboxTargets(), new StructureGitReads(), new SpecFormatPlan(),
        new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance), NullLogger<SpecFormatRun>.Instance);

    public static CommitSpecWorkHandler Handler() => new(
        Run(), Checkpointer(), StructureGateFixture.Gate(),
        new StructureGateOutcome(TestSpecReview.Revert(), new Mock<ISpecProgressRecorder>().Object),
        NullLogger<CommitSpecWorkHandler>.Instance);

    public static RemoteContextDiscovery Context(string name, string workdir, string? command) =>
        new(name, workdir, "csharp", Format: command is null ? null : new ContextYamlFormat(command));

    public static PipelineContext Pipeline(ISandbox sandbox, params RemoteContextDiscovery[] contexts)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = Key }]);
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("spec/x"), "/work"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Key] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal) { [Key] = contexts });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SpecStartHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = StartHead });
        return pipeline;
    }

    private static RunWorkCheckpointer Checkpointer()
    {
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance,
            new StubSandboxFileReaderFactory(), new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        return new RunWorkCheckpointer(
            new RepoWorkPusher(gitOps, new SecretPatternScanner(), new SandboxTargets(), NullLogger<RepoWorkPusher>.Instance),
            NullLogger<RunWorkCheckpointer>.Instance);
    }
}

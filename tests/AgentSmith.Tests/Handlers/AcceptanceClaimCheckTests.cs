using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>2026-10-17-a196c: a met claim's citations resolve, in the loop, against what changed and what ran.</summary>
public sealed class AcceptanceClaimCheckTests
{
    private const string Absent = "no Sample bus remains";
    private const string Built = "the build exits 0";
    private static readonly IReadOnlyList<string> Criteria = [Absent, Built];

    private static CitationResolver Resolver() => new(
        CitedFileIndex.FromPaths(["src/Messaging/Installer.cs"]),
        ["Sample.Server: the agent ran 'grep -rn SampleBus src' exited 1 — no output"]);

    private static MasterVerification Verdict(params AcceptanceDisposition[] dispositions) =>
        new(VerificationStatus.Green, true, true, true, true, "s", AcceptanceDispositions: dispositions);

    private static AcceptanceDisposition Met(string criterion, params string[]? citations) =>
        new(criterion, AcceptanceStatus.Met, "evidence") { Citations = citations };

    [Fact]
    public void AcceptanceClaimCheck_MetCitingARanCommandOrDiffPath_Resolves() =>
        new AcceptanceClaimCheck().Unresolved(
                Verdict(Met(Absent, "grep -rn SampleBus src"), Met(Built, "src/Messaging/Installer.cs")), Criteria, Resolver())
            .Should().BeEmpty();

    [Fact]
    public void AcceptanceClaimCheck_MetCitingNothingThatRan_IsNamed() =>
        new AcceptanceClaimCheck().Unresolved(
                Verdict(Met(Absent, "UsingSampleBus|UseSampleBus"), Met(Built, "src/Messaging/Installer.cs")), Criteria, Resolver())
            .Should().ContainSingle().Which.Should().StartWith(Absent + ": claimed satisfied by 'UsingSampleBus|UseSampleBus'");

    [Fact]
    public void AcceptanceClaimCheck_VerdictWithoutAnyCitations_IsNotChecked() =>
        new AcceptanceClaimCheck().Unresolved(
                Verdict(new AcceptanceDisposition(Absent, AcceptanceStatus.Met, "Final source scan found nothing"),
                    new AcceptanceDisposition(Built, AcceptanceStatus.Met, "built")), Criteria, Resolver())
            .Should().BeEmpty("a skill before citations is not refused for a format it was never told");

    [Fact]
    public void AcceptanceClaimCheck_OneMetWithoutCitationsInACitingVerdict_IsNamed() =>
        new AcceptanceClaimCheck().Unresolved(
                Verdict(new AcceptanceDisposition(Absent, AcceptanceStatus.Met, "Final source scan found nothing"),
                    Met(Built, "src/Messaging/Installer.cs")), Criteria, Resolver())
            .Should().ContainSingle().Which.Should().Contain("cited nothing", "the key cannot be dropped from one claim");

    [Fact]
    public async Task AcceptanceClaimEvidence_DeliveryDiffFailed_SkipsTheCheck()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["server"] = new FailingDiffSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());

        var resolver = await new AcceptanceClaimEvidence(new SpecSandboxes(new SandboxTargets()), AgentSmith.Tests.TestHelpers.TestGit.Delivery,
                new AgentSmith.Application.Services.Sandbox.SandboxWorkingTreeReader(
                    NullLogger<AgentSmith.Application.Services.Sandbox.SandboxWorkingTreeReader>.Instance),
                NullLogger<AcceptanceClaimEvidence>.Instance)
            .GatherAsync(pipeline, CancellationToken.None);

        resolver.Should().BeNull("a partial index would refuse true claims");
    }

    /// <summary>Every git call fails, so no delivery diff can be taken.</summary>
    private sealed class FailingDiffSandbox : ISandbox
    {
        public string JobId => "failing";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 128, TimedOut: false,
                DurationSeconds: 0, ErrorMessage: "fatal", OutputContent: string.Empty));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

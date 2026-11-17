using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>2026-11-16-f3fbb: the boundary gate also holds a ticket whose newest attempt stopped on a precondition —
/// a retrigger is refused naming what did not hold, a person's start and a series start pass.</summary>
public sealed class PreconditionStopGateTests
{
    private static readonly EndDetail Image = new("Image node:22-bookworm (context default)", [],
        "Trusted image registries", "ghcr.io/, docker.io/library/");

    [Fact]
    public async Task Retrigger_TicketStoppedOnPrecondition_IsRefusedWithTheHeadline()
    {
        var refusal = await Gate(Held(RunEndKinds.ImageUntrusted, Image)).RefusalAsync(Request(), Configured, CancellationToken.None);

        refusal!.Rejection.Should().Be(ClaimRejectionReason.TicketStuckOnPrecondition);
        refusal.Error.Should().Be("This ticket stopped on a precondition: Image node:22-bookworm (context default) is not "
            + "trusted: set Trusted image registries to ghcr.io/, docker.io/library/. It starts by itself once that holds, or when you reply.");
    }

    [Fact]
    public async Task StartByName_TicketStoppedOnPrecondition_IsAdmitted()
    {
        var byName = Request() with { InitialContext = new() { [ContextKeys.RequestedByName] = true } };

        (await Gate(Held(RunEndKinds.Preflight, null)).RefusalAsync(byName, Configured, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SeriesAutoStart_TicketStoppedOnPrecondition_IsAdmitted()
    {
        var series = Request() with { InitialContext = new() { [ContextKeys.SeriesAutoStart] = true } };

        (await Gate(Held(RunEndKinds.BootstrapMissing, null)).RefusalAsync(series, Configured, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Retrigger_FailedWithoutPrecondition_IsClaimed() =>
        (await Gate(new PreviousAttempt("run-1", RunStatuses.Failed, DateTimeOffset.UtcNow, true) { EndKind = RunEndKinds.ReadinessUnknown })
            .RefusalAsync(Request(), Configured, CancellationToken.None)).Should().BeNull();

    [Fact]
    public async Task Retrigger_BoundaryStop_KeepsTheBoundarySentence()
    {
        var both = new PreviousAttempt("run-1", RunStatuses.Success, DateTimeOffset.UtcNow, true) { StoppedAtBoundary = true };

        var refusal = await Gate(both).RefusalAsync(Request(), Configured, CancellationToken.None);

        (refusal!.Rejection, refusal.Error).Should().Be((ClaimRejectionReason.TicketStoppedAtBoundary, BoundaryStopGate.Sentence));
    }

    [Theory]
    [InlineData(RunEndKinds.Preflight, null, "a precondition did not hold")]
    [InlineData(RunEndKinds.BootstrapMissing, null, "a repository has no agent-smith bootstrap")]
    [InlineData(RunEndKinds.ImageUntrusted, null, "a context names a container image outside the trusted registries")]
    public void PreconditionHeadline_WithoutDetail_UsesTheKindsWords(string kind, EndDetail? detail, string expected) =>
        PreconditionHeadline.Of(kind, detail).Should().Be(expected);

    [Fact]
    public void PreconditionHeadline_BootstrapAndConfig_NameTheReposAndTheSetting()
    {
        PreconditionHeadline.Of(RunEndKinds.BootstrapMissing, new EndDetail("bootstrap", ["api", "web"], "Initialise", "alpha"))
            .Should().Be("api, web have no agent-smith bootstrap: Initialise project alpha");
        PreconditionHeadline.Of(RunEndKinds.Preflight, new EndDetail("Agent claude", [], "agents.claude.type", "claude"))
            .Should().Be("Agent claude: set agents.claude.type to claude");
    }

    private static PreviousAttempt Held(string kind, EndDetail? detail) =>
        new("run-1", RunStatuses.Failed, DateTimeOffset.UtcNow, true)
        {
            StoppedOnPrecondition = true, EndKind = kind, EndDetail = detail,
        };

    private static BoundaryStopGate Gate(PreviousAttempt latest)
    {
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("my-project", "42", null, It.IsAny<CancellationToken>())).ReturnsAsync(latest);
        return new BoundaryStopGate(attempts.Object);
    }

    private static ClaimRequest Request() => new("GitHub", "my-project", new TicketId("42"), "code");

    // 2026-11-17-166aa: a project that configures failed_status, so only the precondition arm can hold.
    private static readonly AgentSmithConfig Configured = new()
    {
        Projects = new()
        {
            ["my-project"] = new ResolvedProject
            {
                Name = "my-project", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code", FailedStatus = "Failed" },
            },
        },
    };
}

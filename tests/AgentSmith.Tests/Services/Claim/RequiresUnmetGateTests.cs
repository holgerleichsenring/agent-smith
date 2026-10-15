using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-10-14-dbceh: a code claim of a ticket whose required ticket has not landed is refused at the one
/// door — a person's start and a series' automatic start included — and admitted once it landed, merged or by
/// hand. A ticket without requirement rows is never asked anything more; one with rows and no approval
/// record is an incomplete filing.
/// </summary>
public sealed class RequiresUnmetGateTests
{
    private const string Tracker = "gh";
    private readonly InMemoryTicketFacts _facts = new();
    private readonly InMemorySpecApprovalStore _approvals = new();
    private readonly Mock<IRedisClaimLock> _lock = new();

    [Fact]
    public async Task Claim_TicketWithoutRequirementRows_IsUnchanged()
    {
        var result = await ClaimAsync(Request());

        result.Rejection.Should().BeNull();
        LockTaken(Times.Once());
    }

    [Fact]
    public async Task Claim_RequirementNotLanded_IsRefusedNamingTheRequiredTicket()
    {
        await FiledAsync("42", "41");

        var result = await ClaimAsync(Request());

        result.Outcome.Should().Be(ClaimOutcome.Rejected);
        result.Rejection.Should().Be(ClaimRejectionReason.TicketRequiresUnmet);
        result.Error.Should().Be("This ticket waits for #41 to be merged first; merge them, or mark them done by hand.");
        LockTaken(Times.Never());
    }

    [Fact]
    public async Task Claim_TwoRequirementsOneLanded_SentenceNamesOnlyTheUnlandedOne()
    {
        await FiledAsync("42", "40", "41");
        Land("40", TicketLandingKinds.Merged);

        var result = await ClaimAsync(Request());

        result.Error.Should().Be("This ticket waits for #41 to be merged first; merge them, or mark them done by hand.");
    }

    [Fact]
    public async Task Claim_RequirementLandedByMerge_IsAdmitted()
    {
        await FiledAsync("42", "41");
        Land("41", TicketLandingKinds.Merged);

        (await ClaimAsync(Request())).Rejection.Should().BeNull();
        LockTaken(Times.Once());
    }

    [Fact]
    public async Task Claim_RequirementLandedByHand_IsAdmitted()
    {
        await FiledAsync("42", "41");
        Land("41", TicketLandingKinds.ByHand);

        (await ClaimAsync(Request())).Rejection.Should().BeNull();
    }

    [Fact]
    public async Task Claim_PersonsStartByName_IsRefusedToo()
    {
        await FiledAsync("42", "41");
        var byName = Request() with { InitialContext = new() { [ContextKeys.RequestedByName] = true } };

        (await ClaimAsync(byName)).Rejection.Should().Be(ClaimRejectionReason.TicketRequiresUnmet);
    }

    [Fact]
    public async Task Claim_SeriesAutoStart_IsRefusedToo()
    {
        await FiledAsync("42", "41");
        var series = Request() with
        {
            InitialContext = new() { [ContextKeys.RequestedByName] = true, [ContextKeys.SeriesAutoStart] = true },
        };

        (await ClaimAsync(series)).Rejection.Should().Be(ClaimRejectionReason.TicketRequiresUnmet);
    }

    [Fact]
    public async Task Claim_NonCodePipeline_IsNotGated()
    {
        await FiledAsync("42", "41");

        (await Gate().RefusalAsync(Request() with { PipelineName = "security-scan" }, Config(), default)).Should().BeNull();
    }

    [Fact]
    public async Task Claim_TicketWithEdgesButNoApprovalRecord_IsRefusedAsAnIncompleteFiling()
    {
        Edges("42", "41");

        var result = await ClaimAsync(Request());

        result.Rejection.Should().Be(ClaimRejectionReason.TicketRequiresUnmet);
        result.Error.Should().Be(RequiresUnmetGate.IncompleteFiling);
    }

    [Fact]
    public async Task Claim_TicketWithEdgesAllLandedButNoApprovalRecord_IsRefusedAsAnIncompleteFiling()
    {
        Edges("42", "41");
        Land("41", TicketLandingKinds.Merged);

        (await ClaimAsync(Request())).Error.Should().Be(RequiresUnmetGate.IncompleteFiling);
    }

    [Fact]
    public async Task Claim_TicketWithoutRowsOrApprovalRecord_IsUnchanged()
    {
        (await ClaimAsync(Request())).Rejection.Should().BeNull("an ordinary ticket is never asked for an approval record");
    }

    [Fact]
    public async Task Claim_JiraRequirementNotLanded_SentenceNamesDPG12WithoutAHash()
    {
        _facts.Edges.Add(new TicketRequirementEdge("jira-t", "jira-dpg-13", "DPG-13", "jira-project", "jira-dpg-12", "DPG-12",
            true, default, "s-1"));
        await _approvals.SaveAsync(ApprovedSets.Record("jira-dpg-13", DateTimeOffset.UnixEpoch, tracker: "jira-t"), default);
        var request = new ClaimRequest("Jira", "jira-project", new TicketId("DPG-13"), "code");

        var refusal = await Gate().RefusalAsync(request, Config(), default);

        refusal!.Error.Should().Be("This ticket waits for DPG-12 to be merged first; merge them, or mark them done by hand.");
    }

    private RequiresUnmetGate Gate() => new(TicketRequirementDoubles.Of(_facts, _approvals));

    private void Edges(string ticket, params string[] required) =>
        _facts.Edges.AddRange(required.Select(r =>
            new TicketRequirementEdge(Tracker, $"github-{ticket}", ticket, "my-project", $"github-{r}", r, true, default, "s-1")));

    private async Task FiledAsync(string ticket, params string[] required)
    {
        Edges(ticket, required);
        await _approvals.SaveAsync(ApprovedSets.Record($"github-{ticket}", DateTimeOffset.UnixEpoch, tracker: Tracker), default);
    }

    private void Land(string ticket, string kind) =>
        _facts.Landed.Add(new TicketLandingFact(Tracker, $"github-{ticket}", ticket, "my-project", kind, DateTimeOffset.UnixEpoch,
            null, null, null));

    private Task<ClaimResult> ClaimAsync(ClaimRequest request) =>
        new TicketClaimService(_lock.Object, new InMemoryUnmovedTicketStore(), new AgentSmith.Application.Services.Resume.NullPreviousAttemptReader(),
            Mock.Of<ITicketStatusTransitionerFactory>(), Mock.Of<IRedisJobQueue>(), Mock.Of<IActiveRunLease>(),
            new InMemoryTakenTicketStore(), NullLogger<TicketClaimService>.Instance, TicketRequirementDoubles.Of(_facts, _approvals))
            .ClaimAsync(request, Config(), CancellationToken.None);

    private void LockTaken(Times times) =>
        _lock.Verify(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), times);

    private static ClaimRequest Request() => new("GitHub", "my-project", new TicketId("42"), "code");

    private static AgentSmithConfig Config() => new()
    {
        Projects = new()
        {
            ["my-project"] = new ResolvedProject
            {
                Name = "my-project", Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code" },
            },
            ["jira-project"] = new ResolvedProject
            {
                Name = "jira-project", Tracker = new TrackerConnection { Name = "jira-t", Type = TrackerType.Jira },
                JiraTrigger = new JiraTriggerConfig { DefaultPipeline = "code" },
            },
        },
    };
}

using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.PreconditionStuck;

/// <summary>2026-11-16-f3fbb: a newest attempt that failed on a precondition reads Stuck by what ended it — also
/// when the 24-hour between-attempts check no longer says so (stuck: false is what the stale check yields).</summary>
public sealed class TicketBucketPreconditionTests
{
    private static readonly TicketLiveState Live = new(
        new Dictionary<string, int>(), new HashSet<string>(), new Dictionary<string, PendingQuestionInfo>());

    private static string Bucket(string status, string? kind)
    {
        var run = Run("r-1", status: status);
        run.EndKind = kind;
        return new TicketBucketRule().Of(run, Live, [], stuck: false).Bucket;
    }

    [Theory]
    [InlineData(RunEndKinds.Preflight)]
    [InlineData(RunEndKinds.ImageUntrusted)]
    [InlineData(RunEndKinds.BootstrapMissing)]
    public void TicketBucketRule_PreconditionStopAfter24Hours_IsStuck(string kind) =>
        Bucket(RunStatuses.Failed, kind).Should().Be(TicketBucket.Stuck);

    [Theory]
    [InlineData(RunEndKinds.ReadinessUnknown)]
    [InlineData(RunEndKinds.CriteriaUnmet)]
    [InlineData(null)]
    public void TicketBucketRule_OtherFailure_StaysFailed(string? kind) =>
        Bucket(RunStatuses.Failed, kind).Should().Be(TicketBucket.Failed);

    [Fact]
    public void TicketBucketRule_PreconditionKindOnACancelledAttempt_IsFailed() =>
        Bucket("cancelled", RunEndKinds.Preflight).Should().Be(TicketBucket.Failed, "2026-11-17-f9b0a: a cancelled head is failed");
}

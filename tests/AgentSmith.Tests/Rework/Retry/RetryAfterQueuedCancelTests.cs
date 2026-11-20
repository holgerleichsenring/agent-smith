using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Rework.Retry;

/// <summary>
/// 2026-11-20-9936a: a failed started attempt r-1, then r-2 a person cancelled while it was queued (reason operator, no
/// step) — the page's newest attempt is r-1, the rework worker reads r-2. A Retry of either is admitted on r-1's end
/// and recorded on r-2, the run the worker reads; the claim door still keeps a poll off the ticket.
/// </summary>
public sealed class RetryAfterQueuedCancelTests : IDisposable
{
    private const string Cancelled = "cancelled";

    private readonly RetryHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task SeedAsync(string headStatus = RunStatuses.Failed)
    {
        await _h.AttemptAsync("r-1", headStatus);
        await _h.AddAsync(TicketStoreHarness.Run("r-2", status: Cancelled, cancelReason: "operator"));
    }

    [Fact]
    public async Task Retry_ButtonOnTheStartedAttempt_RecordsOnTheQueuedCancel_WorkerLaunchesOnce()
    {
        await SeedAsync();

        var result = await _h.RetryAsync("r-1");

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        var request = (await _h.RequestsAsync()).Should().ContainSingle().Subject;
        request.RunId.Should().Be("r-2", "the worker reads r-2, so the request is a row of r-2");
        (await _h.HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
        var cause = await _h.CauseOfNextAttemptAsync("r-3");
        cause.Channel.Should().Be(AttemptChannels.Retry);
        cause.Actor.Should().Be(RetryHarness.Principal);
        cause.At.Should().Be(request.RequestedAt);
    }

    [Fact]
    public async Task Retry_ConversationWithTheReadersLatest_IsTheSameRetry()
    {
        await SeedAsync();
        var latest = await _h.Services.GetRequiredService<IPreviousAttemptReader>().LatestAsync("alpha", "42", null, default);
        latest!.RunId.Should().Be("r-2", "what ConversationAttemptStart and SeriesTicketActs pass");

        (await _h.ConversationRetryAsync(latest.RunId)).Kind.Should().Be(RunRetryResultKind.Requested);

        (await _h.RequestsAsync()).Should().ContainSingle().Which.RunId.Should().Be("r-2");
        await _h.HandleNextAsync();
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Retry_ConversationStartNextAttempt_StartsTheNextAttempt()
    {
        await SeedAsync();
        var port = await _h.StartPortAsync(new TurnPoster("ops-2", MayStartRuns: true));
        var tool = new StartNextAttemptToolHost(port!).GetTools(null, null).Single();

        var answer = (await tool.InvokeAsync(new AIFunctionArguments { ["reason"] = "go on" }))!.ToString()!.Trim('"');

        answer.Should().Be(ConversationAttemptStart.Requested);
        await _h.HandleNextAsync();
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Retry_SuccessHeadThenQueuedCancel_RefusedDeliveredForBothRuns()
    {
        await SeedAsync(RunStatuses.Success);

        var button = (await _h.RetryAsync("r-1")).Should().BeOfType<Conflict<RetryRefusalView>>().Which.Value!;
        var conversation = await _h.ConversationRetryAsync("r-2");

        button.Reason.Should().Be(RetryRefusals.Delivered);
        conversation.Refusal!.Reason.Should().Be(RetryRefusals.Delivered, "the head's end decides for the cancel after it");
        (await _h.RequestsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Retry_StartedAttemptAfterTheHead_StillNotLatest()
    {
        await SeedAsync();
        await _h.AttemptAsync("r-3", RunStatuses.Failed);

        var refusal = (await _h.RetryAsync("r-1")).Should().BeOfType<Conflict<RetryRefusalView>>().Which.Value!;

        refusal.Reason.Should().Be(RetryRefusals.NotLatest);
        refusal.Text.Should().Contain("r-3");
    }

    // The poll protection PreviousAttemptRows keeps: a person's queued cancel stays the reader's latest attempt.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClaimDoor_PersonsQueuedCancelIsLatest_PollRefused_StartByNamePasses(bool afterAStartedAttempt)
    {
        if (afterAStartedAttempt) await _h.AttemptAsync("r-1", RunStatuses.Success);
        await _h.AddAsync(TicketStoreHarness.Run("r-2", status: Cancelled, cancelReason: "operator"));
        var gate = new BoundaryStopGate(_h.Services.GetRequiredService<IPreviousAttemptReader>());
        var poll = new ClaimRequest("GitHub", "alpha", new TicketId("42"), "code");
        var byName = poll with { InitialContext = new() { [ContextKeys.RequestedByName] = true } };

        var refusal = await gate.RefusalAsync(poll, NoFailedStatus(), default);

        refusal!.Rejection.Should().Be(ClaimRejectionReason.TicketLeftInPlace);
        refusal.Error.Should().Contain("r-2");
        (await gate.RefusalAsync(byName, NoFailedStatus(), default)).Should().BeNull();
    }

    private AgentSmithConfig NoFailedStatus() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = new()
            {
                Name = "alpha", DefaultPipeline = "code", Tracker = _h.Project.Tracker,
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code", TriggerStatuses = ["open"], DoneStatus = "Done" },
            },
        },
    };
}

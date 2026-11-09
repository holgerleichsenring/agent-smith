using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-09-9a98: found live: the master wrote a proposal in a minute, the review ran for many more, and
/// cancelling discarded the draft that had been paid for. Through the REAL server composition: the
/// proposal reaches the page and the session row before its review, a cancel during that review leaves it
/// there as not reviewed, and the next turn is told its text.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogProposalUnderReviewTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-under-review";

    private const string SpecReply =
        """
        One spec.
        ```yaml
        spec: 2026-11-09-aaaa
        goal: "Widgets are stored and read back"
        steps:
          - id: store
            action: "Add the widget store"
        done:
          - "a widget is stored and read back"
        ```
        """;

    private readonly RecordingDialogHub _hub = new();
    private readonly CancellationTokenSource _cancel = new();
    private readonly List<SpecDialogProposalPush> _proposals = [];

    // Stands in for the person who cancels once they have seen the proposal.
    public SpecDialogProposalUnderReviewTests() => _hub.OnPush = (method, args) =>
    {
        if (method != "SpecDialogProposal" || args[0] is not SpecDialogProposalPush push) return;
        _proposals.Add(push);
        _cancel.Cancel();
    };

    [Fact]
    public async Task SpecDialogTurn_CancelledDuringReview_KeepsTheProposalAndTheNextTurnContinuesIt()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);
        harness.ChatClient.EnqueueText(SpecReply);

        // The pipeline turns a cancel into a failed result or lets it escape, depending on where it lands.
        var first = await TurnAsync(harness, state, _cancel.Token).ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : null, TaskScheduler.Default);

        _cancel.IsCancellationRequested.Should().BeTrue($"the person saw the proposal and cancelled (the turn ended on {first?.Reply})");
        _proposals.Should().ContainSingle().Which.UnderReview.Should().BeTrue();
        var view = await ViewAsync(harness);
        view.Session!.Proposal.Should().NotBeNull("what the master wrote survives the cancel");
        view.Session.Proposal!.UnderReview.Should().BeFalse("no turn is reviewing it any more");
        view.Session.Proposal.NotReviewed.Should().Equal("2026-11-09-aaaa");

        harness.ChatClient.EnqueueText("Continuing.");
        await TurnAsync(harness, state, CancellationToken.None);

        PromptOf(harness.ChatClient.CallMessages[^1]).Should().Contain("The proposal your last turn wrote")
            .And.Contain("Widgets are stored and read back");
    }

    private static string PromptOf(IReadOnlyList<ChatMessage>? messages) =>
        string.Join("\n", (messages ?? []).Select(m => m.Text));

    private static async Task<SpecDialogView> ViewAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SpecDialogViewReader>()
            .ReadAsync(Dialog, CancellationToken.None);
    }

    private static async Task<SpecDialogTurnResult> TurnAsync(RealCompositionHarness harness, ConversationState state, CancellationToken ct)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
        var current = await manager.GetOpenByThreadAsync(state.Platform, Dialog, ct) ?? state;
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(current with { Transcript = state.Transcript }, new TurnPoster("U1", false), ct);
    }

    private RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<IHubContext<JobsHub>>();
            services.AddSingleton<IHubContext<JobsHub>>(_hub);
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(new AliveSandboxHeartbeat());
        });

    private static async Task<ConversationState> OpenAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-review",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        // A proposal is allowed once the person has answered the partner, so the turn starts discussed.
        return opened
            .AppendTurn(new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow))
            .AppendTurn(new TranscriptTurn(TranscriptRole.Assistant, "Per tenant or global?", DateTimeOffset.UtcNow))
            .AppendTurn(new TranscriptTurn(TranscriptRole.User, "global; propose it", DateTimeOffset.UtcNow));
    }
}

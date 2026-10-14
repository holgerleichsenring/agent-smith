using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>
/// 2026-10-13-8762s: the turn's scope tracks the session row from its first append, while a ratification or
/// an approval answer is appended in a request scope of its own. The turn's later appends re-read the row
/// first, so neither is written over; the filing stamp reads through a fresh scope, so it sees the
/// ratification the tracked copy predates. Through a real container over the shipped schema.
/// </summary>
public sealed class TranscriptReloadTests : IDisposable
{
    private readonly RatificationRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task Filing_AfterRatificationInAnotherScope_KeepsTheRatificationTurn()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        using var turn = _rig.Provider.CreateScope();
        var state = await RatificationRig.AppendAsync(turn, TranscriptRole.Assistant, "proposal", SpecDialogTurnKind.SpecProposal);

        (await _rig.RatifyAsync(session, "spec-a", 1)).Status.Should().Be(200);
        var stamped = await turn.ServiceProvider.GetRequiredService<RatifiedMockStamp>()
            .StampAsync(state!, new SpecOutcome(Draft("spec-a")), CancellationToken.None);
        await RatificationRig.AppendAsync(turn, TranscriptRole.Assistant, "Filed.", SpecDialogTurnKind.Filing);

        ((SpecOutcome)stamped).Draft.RatifiedMock.Should().Be(1);
        (await _rig.TranscriptAsync()).Select(t => t.Kind).Should().Equal(
            SpecDialogTurnKind.SpecProposal, SpecDialogTurnKind.MockRatified, SpecDialogTurnKind.Filing);
    }

    [Fact]
    public async Task Filing_AfterApprovalAnswerInItsOwnScope_KeepsTheAnswerTurn()
    {
        await _rig.OpenAsync();
        using var turn = _rig.Provider.CreateScope();
        await RatificationRig.AppendAsync(turn, TranscriptRole.Assistant, "proposal", SpecDialogTurnKind.SpecProposal);

        using (var answer = _rig.Provider.CreateScope())
            await RatificationRig.AppendAsync(answer, TranscriptRole.User, "approve", decision: SpecDialogDecision.Approved);
        await RatificationRig.AppendAsync(turn, TranscriptRole.Assistant, "Filed.", SpecDialogTurnKind.Filing);

        var transcript = await _rig.TranscriptAsync();
        transcript.Select(t => t.Text).Should().Equal("proposal", "approve", "Filed.");
        transcript[1].Decision.Should().Be(SpecDialogDecision.Approved);
    }

    [Fact]
    public async Task MockRatify_DuringComputeOnAnotherScope_TurnSurvivesTheAssistantAppend()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        using var turn = _rig.Provider.CreateScope();
        await RatificationRig.AppendAsync(turn, TranscriptRole.User, "make the header blue");

        // The turn computes on another replica: this one's gate knows nothing of it, so the ratification is accepted.
        (await _rig.RatifyAsync(session, "spec-a", 1)).Status.Should().Be(200);
        await RatificationRig.AppendAsync(turn, TranscriptRole.Assistant, "Done.", SpecDialogTurnKind.Answer);

        (await _rig.TranscriptAsync()).Select(t => t.Text).Should().Equal(
            "make the header blue", "Ratified mock v1 of spec-a.", "Done.");
    }

    [Fact]
    public async Task SessionRepository_GetOpenByThread_TrackedRowChangedElsewhere_ReturnsCurrentValues()
    {
        await _rig.OpenAsync();
        using var first = _rig.Provider.CreateScope();
        var repository = first.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>();
        var tracked = await repository.GetOpenByThreadAsync(RatificationRig.Platform, RatificationRig.Thread, default);

        using (var other = _rig.Provider.CreateScope())
        {
            var elsewhere = other.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>();
            var row = await elsewhere.GetOpenByThreadAsync(RatificationRig.Platform, RatificationRig.Thread, default);
            row!.Subject = "changed elsewhere";
            await elsewhere.SaveAsync(default);
        }
        var again = await repository.GetOpenByThreadAsync(RatificationRig.Platform, RatificationRig.Thread, default);

        again.Should().BeSameAs(tracked, "the scope's tracked row is the one handed back");
        again!.Subject.Should().Be("changed elsewhere");
    }

    internal static SpecDraft Draft(string id, string? extra = null) =>
        new(id, "goal", $"spec: {id}\ngoal: goal\n{extra}".TrimEnd(), []);
}

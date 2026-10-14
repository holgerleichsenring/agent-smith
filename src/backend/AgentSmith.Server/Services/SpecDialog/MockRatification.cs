using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// A person ratifies one mock version of a spec in a design conversation. The ratification is a user
/// turn of the transcript until filing stamps it into the spec; the partner reads it like any other turn.
/// <para>
/// REFUSED ONLY WHILE THE TURN COMPUTES. The turn gate stays entered across the approval wait, which is
/// exactly when the proposal is on screen, so the gate is never taken here. Computing is known only on
/// the replica running the turn; a ratification accepted elsewhere is kept because the turn's later
/// appends re-read the row (<see cref="SpecDialogSessionRepository.GetOpenByThreadAsync"/>).
/// </para>
/// </summary>
public sealed class MockRatification(
    SpecDialogOwnership ownership, SpecDialogSessionRepository sessions, IConversationMockReader mocks,
    SpecDialogTurnGate gate, BoundSessionNudge bound, TimeProvider time)
{
    public async Task<MockRatificationResult> RatifyAsync(
        string sessionId, string specId, int version, string owner, CancellationToken ct)
    {
        if (!await ownership.MayReachSessionAsync(sessionId, owner, ct)) return MockRatificationResult.NotFound(null);
        var held = await mocks.ListAsync(sessionId, ct);
        if (!held.Any(m => m.SpecId == specId && m.Version == version))
            return MockRatificationResult.NotFound($"Mock v{version} of {specId} is not held in this conversation.");
        if (gate.Liveness(sessionId).Computing)
            return MockRatificationResult.Conflict("The design partner is computing a turn; ratify when it has answered.");

        var session = await sessions.GetBySessionOnPlatformAsync(DispatcherDefaults.PlatformDashboard, sessionId, ct);
        if (session is null) return MockRatificationResult.NotFound(null);
        var turn = new TranscriptTurn(TranscriptRole.User, $"Ratified mock v{version} of {specId}.", time.GetUtcNow(),
            SpecDialogTurnKind.MockRatified, Mock: new MockRatificationTurn(specId, version, owner));
        var transcript = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        session.TranscriptJson = SpecDialogSessionMapper.WriteTranscript([.. transcript, turn]);
        session.LastActivityAt = turn.At;
        await bound.SaveAsync(session, ct);
        return MockRatificationResult.Ratified(turn.Text);
    }
}

/// <summary>What a ratification answered: the HTTP status it maps to and the sentence it says.</summary>
public sealed record MockRatificationResult(int Status, string? Text)
{
    public static MockRatificationResult Ratified(string text) => new(StatusCodes.Status200OK, text);

    public static MockRatificationResult NotFound(string? text) => new(StatusCodes.Status404NotFound, text);

    public static MockRatificationResult Conflict(string text) => new(StatusCodes.Status409Conflict, text);
}

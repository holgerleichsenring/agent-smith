using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Turns;

/// <summary>2026-11-18-c8e2a: two mid-turn sources read as one — the operator's messages first, then the master's
/// sub-agent notices — so a master's tool result hands on both.</summary>
public sealed class CombinedMidTurnMessages(IMidTurnMessages first, IMidTurnMessages second) : IMidTurnMessages
{
    /// <summary>The sources there are as one; null when there is none.</summary>
    public static IMidTurnMessages? Of(IMidTurnMessages? first, IMidTurnMessages? second) =>
        first is null ? second : second is null ? first : new CombinedMidTurnMessages(first, second);

    public void Lead(bool stewarded) => first.Lead(stewarded);

    public async ValueTask<IReadOnlyList<MidTurnMessage>> DrainAsync(CancellationToken cancellationToken) =>
        [.. await first.DrainAsync(cancellationToken), .. await second.DrainAsync(cancellationToken)];
}

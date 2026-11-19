namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-12-3a36b: what the deriver does with a parsed reply after the contradiction
/// gate saw it.</summary>
public enum HandbackGateStep
{
    /// <summary>Not a contradiction — the cut goes on as before.</summary>
    Continue,

    /// <summary>The contradiction parks: cited, or uncited on the last attempt.</summary>
    Park,

    /// <summary>The contradiction cites no look the derivation took — asked again.</summary>
    ReAsk,
}

/// <param name="Step">What the deriver does next.</param>
/// <param name="Message">What the deriver is answered with on a re-ask.</param>
public sealed record HandbackGateOutcome(HandbackGateStep Step, string? Message = null)
{
    public static HandbackGateOutcome Continue { get; } = new(HandbackGateStep.Continue);

    public static HandbackGateOutcome Park { get; } = new(HandbackGateStep.Park);
}

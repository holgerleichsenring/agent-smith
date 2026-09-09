namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>What the ledger-complete brake asks of the governor on one iteration.</summary>
public enum LedgerBrakeAction
{
    /// <summary>Nothing — the checklist is open, or the allowance is not spent.</summary>
    None,
    /// <summary>Append the verdict demand to this iteration's messages, once per pass.</summary>
    Demand,
    /// <summary>End the pass without calling the model: the demand was spent on tool calls.</summary>
    Stop,
}

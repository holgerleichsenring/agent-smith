namespace AgentSmith.Server.Services.Derivation;

/// <summary>2026-10-26-bdd0e: the sentences a refused derivation is answered with.</summary>
public static class SpecDerivationTexts
{
    public const string NoRepos = "This project has no repositories to derive specs in.";
    public const string Held = "Another run is working on this ticket. Derive its specs once that run has ended.";
    public const string BeingStarted = "A run on this ticket is being started right now. Try again in a moment.";
    public const string Waiting =
        "The latest attempt is waiting for an answer. Answer it, or derive the specs once it has ended.";
    public const string Queued = "An attempt at this ticket is queued. Derive its specs once it has ended.";
    public const string QueuedSummary = "starting: spec derivation requested";
}

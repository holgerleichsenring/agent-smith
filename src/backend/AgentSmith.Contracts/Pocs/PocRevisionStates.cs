namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933db: the states of a PoC revision — admitted by the import, then validated or refused by the
/// poc-deploy run's intake, never evaluated twice.</summary>
public static class PocRevisionStates
{
    public const string Admitted = "admitted";
    public const string Validated = "validated";
    public const string Refused = "refused";
}

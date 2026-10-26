namespace AgentSmith.Server.Services.Derivation;

/// <summary>2026-10-26-bdd0e: how a request to derive a ticket's specs ended.</summary>
public enum SpecDerivationOutcome
{
    Started,
    Refused,
    UnknownProject,
    NoCapacity,
}

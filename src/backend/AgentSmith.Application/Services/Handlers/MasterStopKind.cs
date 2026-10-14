namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-10-13-3890a: why a master's open loop ended — recorded on the trail for every code run.</summary>
public enum MasterStopKind
{
    Cap,
    Budget,
    RedDrained,
    Blocker,
    Declined,
    Verdictless,
    Question,
    Idle,
    SelfRefilled,
    Satisfied,
}

namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-0ea8: the surface shape a verification requirement applies to — one
/// station of a request's path through the system. The lens keys the ingested standard
/// against these, so a question is asked where it can be answered by looking.
/// </summary>
public enum VerificationStation
{
    /// <summary>How a request is admitted: routing, transport, protocol, the shape and
    /// content of what is accepted.</summary>
    Admission,

    /// <summary>What the request carries as proof of who is making it: credentials,
    /// tokens, cookies, session identifiers.</summary>
    Evidence,

    /// <summary>How an identity is derived from that evidence and validated.</summary>
    Resolution,

    /// <summary>What the resolved identity is permitted to do.</summary>
    Authority,

    /// <summary>Which objects the resolved identity may reach.</summary>
    Scope,

    /// <summary>What the operation does and produces: state changes, output, logs,
    /// errors.</summary>
    Effect
}

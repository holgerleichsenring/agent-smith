namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>One tracker ticket created while filing a confirmed outcome.</summary>
public sealed record FiledTicket(string Reference, string Title);

/// <summary>
/// p0315c: exactly what a filing attempt did. Filed lists every ticket that
/// WAS created (in creation order) even when Error is set — a partial epic
/// must never silently lose children, so the report is honest about both
/// halves.
/// </summary>
public sealed record FilingReport(IReadOnlyList<FiledTicket> Filed, string? Error)
{
    public bool Succeeded => Error is null;
}

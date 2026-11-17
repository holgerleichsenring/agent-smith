namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: the mutable holder the server seeds under ContextKeys.FilingReport, because the pipeline context
/// never leaves the run; FileTickets fills it with what the filing did. Null when the run ended before filing.
/// </summary>
public sealed class FilingReportSlot
{
    public FilingReport? Report { get; set; }
}

namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-18-98fa: one spec master's report as the run detail serves it — markdown, rendered and never
/// parsed; SpecId is null where the view belongs to one spec already.</summary>
public sealed record MasterReportView(string Markdown, string? SpecId = null);

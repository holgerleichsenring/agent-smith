namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: one file of a design turn's draft — its path under <c>draft:</c> and its text.</summary>
public sealed record DraftFile(string Path, string Text);

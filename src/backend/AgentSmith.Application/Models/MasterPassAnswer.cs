namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-01-0e80: one pass of a scan master and the answer it gave, labelled by WHICH
/// pass it was — the unanchored first look, the coverage re-drive, or the reconciliation
/// against the scanners' output. The label is what lets a reader judge (or revert) the
/// change that moved the scanner list out of the first turn.
/// </summary>
public sealed record MasterPassAnswer(string Pass, string Answer);

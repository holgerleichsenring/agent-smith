namespace AgentSmith.Contracts.Verify;

/// <summary>
/// 2026-11-17-d6fbc: what one verify pass ran for one repository, in order, and whether it came out green.
/// </summary>
/// <param name="Repository">The sandbox key; "(default)" for the unnamed one.</param>
/// <param name="Result">passed | failed.</param>
public sealed record VerifyRepositoryPass(string Repository, string Result, IReadOnlyList<VerifyStageRun> Stages);

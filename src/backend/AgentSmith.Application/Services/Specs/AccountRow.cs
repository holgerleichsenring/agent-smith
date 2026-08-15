namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: one row exactly as the accounting instance wrote it, before its citation has
/// been resolved against the diff. Separate from CriterionAccount because a CLAIM and a
/// CHECKED claim are not the same thing, and the whole gate rests on the difference.
/// </summary>
public sealed record AccountRow(
    string Criterion, bool Satisfied, string? Citation = null, string? Note = null);

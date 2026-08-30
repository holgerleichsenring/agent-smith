namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-3c12: one entry group of the scanned system and every requirement its
/// stations were asked. A group beyond the run's declared cap carries no rows at all and
/// says so through <paramref name="Attempted"/> — a budget fact, never a verdict.
/// </summary>
public sealed record EntryGroupRequirements(
    string Group, bool Attempted, IReadOnlyList<RequirementRow> Rows)
{
    /// <summary>The rows the scan produced a verdict for.</summary>
    public IReadOnlyList<RequirementRow> Answered => [.. Rows.Where(r => r.Answered)];

    public IReadOnlyList<RequirementRow> Unmet =>
        [.. Rows.Where(r => r.Disposition == RequirementDisposition.Unmet)];

    public IReadOnlyList<RequirementRow> Undecidable =>
        [.. Rows.Where(r => r.Disposition == RequirementDisposition.CannotAnswer)];

    /// <summary>Whether this group enumerated any state-changing operation at all.</summary>
    public bool EnumeratesWrites => Rows.Any(r => r.Operation == RequirementOperation.Write);

    /// <summary>
    /// The write rows this group failed while passing the very same entry on its read path
    /// — the same resource scoped on read and unscoped on write, which is the asymmetry a
    /// reviewer following only the read path never reaches.
    /// </summary>
    public IReadOnlyList<RequirementRow> ReadWriteAsymmetries =>
    [
        .. Rows.Where(w => w.Operation == RequirementOperation.Write
                && w.Disposition == RequirementDisposition.Unmet)
            .Where(w => Rows.Any(r => r.Operation == RequirementOperation.Read
                && r.Disposition == RequirementDisposition.Met
                && r.Station == w.Station
                && string.Equals(r.RequirementId, w.RequirementId, StringComparison.Ordinal)))
    ];
}

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: the parsed measurement table — the rows plus the fixture content hash
/// the measurement was taken against, one per repository shape.
/// </summary>
public sealed record MeasuredCommandTable(
    IReadOnlyList<MeasuredCommand> Rows,
    IReadOnlyDictionary<string, string> FixtureHashes)
{
    public IEnumerable<string> Shapes => Rows.Select(r => r.Shape).Distinct();

    /// <summary>Every row of one (shape, command) pair, keyed by variant.</summary>
    public IReadOnlyDictionary<string, MeasuredCommand> Variants(string shape, string command) =>
        Rows.Where(r => r.Shape == shape && r.Command == command)
            .ToDictionary(r => r.Variant);

    public IEnumerable<(string Shape, string Command)> Pairs =>
        Rows.Select(r => (r.Shape, r.Command)).Distinct();
}

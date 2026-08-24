namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: the one part of the table that IS re-derivable offline — the
/// classification of a (shape, command) pair from its own exit codes.
/// <para>
/// A command that does not exist exits non-zero on every fixture, clean ones
/// included, so "it went red on the broken fixture" proves nothing alone:
/// red on the clean fixture of its own shape means <c>broken-on-clean</c>, not a
/// gate. Red only on the tab-indentation variant means <c>linter</c>, not a gate
/// for the defect class it was claimed to detect.
/// </para>
/// </summary>
public sealed class MeasuredCommandVerdict
{
    public const string Declarable = "declarable";
    public const string Linter = "linter";
    public const string BrokenOnClean = "broken-on-clean";
    public const string NoDefectDetected = "no-defect-detected";

    public static readonly string[] All = [Declarable, Linter, BrokenOnClean, NoDefectDetected];

    public string Classify(IReadOnlyDictionary<string, MeasuredCommand> variants)
    {
        if (!variants.TryGetValue(MeasuredCommand.CleanVariant, out var clean) || clean.IsRed)
            return BrokenOnClean;
        var reds = variants.Values
            .Where(r => r.IsRed && r.Variant != MeasuredCommand.CleanVariant)
            .Select(r => r.Variant)
            .ToHashSet(StringComparer.Ordinal);
        if (reds.Any(v => v != MeasuredCommand.SyntaxDefect)) return Declarable;
        return reds.Count > 0 ? Linter : NoDefectDetected;
    }
}

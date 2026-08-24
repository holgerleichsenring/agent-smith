using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0513: refuses a profile command this repository's measurement does not record
/// as declarable. p0505 measured candidate commands against clean and defective
/// fixtures and classified each; a command outside that set is a guess, and a
/// guess in a profile is a red build on somebody else's clean repository.
/// <para>
/// SCOPED TO PROFILES THAT HAVE A TABLE. There is exactly one measurement, for one
/// toolchain: read as "any declared command the table does not record", the rule
/// would refuse every command of every future profile. A profile this repository
/// offers no evidence about is not gated by evidence it was never offered.
/// </para>
/// <para>
/// It is a test rather than a refusal where the profile is read, because the table
/// is not in the deployed artefact — it and its readers live in this test project
/// behind a helper that throws without a checkout. Refusing at read time would
/// throw on every domain run in a pod.
/// </para>
/// </summary>
public sealed class MeasuredCommandGate
{
    /// <summary>The domains this repository ships a measured table for.</summary>
    public static readonly IReadOnlySet<string> DomainsWithATable =
        new HashSet<string>(StringComparer.Ordinal) { "dbt-databricks" };

    public bool Gates(DomainProfile profile) =>
        profile is not null && DomainsWithATable.Contains(profile.Name);

    /// <summary>
    /// The commands <paramref name="profile"/> declares that the table does not
    /// record as declarable ON ANY SHAPE. Empty for a profile with no table.
    /// </summary>
    public IReadOnlyList<string> Undeclarable(DomainProfile profile, MeasuredCommandTable table)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(table);
        if (!Gates(profile)) return [];
        var declarable = Declarable(table);
        return [.. profile.Verify.Select(c => c.Command).Where(c => !declarable.Contains(c))];
    }

    /// <summary>Every command the table records as declarable, on any shape.</summary>
    public IReadOnlySet<string> Declarable(MeasuredCommandTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Pairs
            .Where(p => table.Variants(p.Shape, p.Command).Values
                .Any(r => r.Verdict == MeasuredCommandVerdict.Declarable))
            .Select(p => p.Command)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Every command declarable on ONE shape — what a repository of that shape may run.</summary>
    public IReadOnlySet<string> DeclarableOn(MeasuredCommandTable table, string shape)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Pairs
            .Where(p => p.Shape == shape && table.Variants(p.Shape, p.Command).Values
                .Any(r => r.Verdict == MeasuredCommandVerdict.Declarable))
            .Select(p => p.Command)
            .ToHashSet(StringComparer.Ordinal);
    }
}

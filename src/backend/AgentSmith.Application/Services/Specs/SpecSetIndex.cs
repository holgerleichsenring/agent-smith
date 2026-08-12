using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: <c>set.yaml</c> — the machine-readable index beside the phase specs.
/// The phase yaml and its markdown companion are what a HUMAN reads; the order of
/// the sequence, the revision history, the accounting and which phases already
/// executed are what the next RUN reads, and putting them in the phase files would
/// mean editing a phase spec to record something that is not part of the phase.
/// <para>
/// Executed phase ids are recorded here because an executed phase is APPEND-ONLY:
/// editing one would rewrite the record of work that already happened and already
/// sits in the branch history.
/// </para>
/// </summary>
public sealed class SpecSetIndexDocument
{
    public string Key { get; set; } = string.Empty;
    public string Source { get; set; } = SpecSource.Derived.ToString();
    public bool TicketPinnedWhole { get; set; }
    public List<string> Phases { get; set; } = [];
    public List<string> ExecutedPhases { get; set; } = [];
    public List<SpecSetRevisionEntry> Revisions { get; set; } = [];
    public List<SpecSetCarriedEntry> Carried { get; set; } = [];
    public List<SpecSetDiscardedEntry> Discarded { get; set; } = [];
    public List<int> Unaccounted { get; set; } = [];
    public string? HandbackCase { get; set; }
    public string? HandbackReason { get; set; }
}

public sealed class SpecSetRevisionEntry
{
    public int Number { get; set; }
    public string Cause { get; set; } = string.Empty;
    public string At { get; set; } = string.Empty;
}

public sealed class SpecSetCarriedEntry
{
    public int Segment { get; set; }
    public string Phase { get; set; } = string.Empty;
}

public sealed class SpecSetDiscardedEntry
{
    public int Segment { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// p0393a: one YamlDotNet configuration shared by emit and consume, so a set this
/// system writes is always a set this system can read back — the p0193 one-builder
/// rule applied to the spec set.
/// </summary>
public sealed class SpecSetIndex
{
    /// <summary>File name of the index inside the spec-set directory.</summary>
    public const string FileName = "set.yaml";

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public string Serialize(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return _serializer.Serialize(new SpecSetIndexDocument
        {
            Key = set.Key,
            Source = set.Source.ToString(),
            TicketPinnedWhole = set.TicketPinnedWhole,
            Phases = [.. set.Phases.Select(p => p.FileStem)],
            ExecutedPhases = [.. set.Executed],
            Revisions = [.. set.Revisions.Select(r => new SpecSetRevisionEntry
            {
                Number = r.Number,
                Cause = r.Cause,
                At = r.At.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            })],
            Carried = [.. set.Accounting.Carried.Select(
                c => new SpecSetCarriedEntry { Segment = c.SegmentId, Phase = c.PhaseId })],
            Discarded = [.. set.Accounting.Discarded.Select(
                d => new SpecSetDiscardedEntry { Segment = d.SegmentId, Reason = d.Reason })],
            Unaccounted = [.. set.Accounting.Unaccounted],
            HandbackCase = set.Handback?.Case.ToString(),
            HandbackReason = set.Handback?.Reason,
        });
    }

    public SpecSetIndexDocument? Parse(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        try { return _deserializer.Deserialize<SpecSetIndexDocument>(yaml); }
        catch (YamlException) { return null; }
        catch (InvalidCastException) { return null; }
    }

    public SpecAccounting AccountingOf(SpecSetIndexDocument doc) => new(
        [.. doc.Carried.Select(c => new CarriedSegment(c.Segment, c.Phase))],
        [.. doc.Discarded.Select(d => new DiscardedSegment(d.Segment, d.Reason))],
        [.. doc.Unaccounted]);

    public IReadOnlyList<SpecRevision> RevisionsOf(SpecSetIndexDocument doc) =>
        doc.Revisions.Count == 0
            ? [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)]
            : [.. doc.Revisions.Select(r => new SpecRevision(
                r.Number, r.Cause,
                DateTimeOffset.TryParse(r.At, out var at) ? at : DateTimeOffset.UtcNow))];

    public SpecHandback? HandbackOf(SpecSetIndexDocument doc) =>
        Enum.TryParse<SpecHandbackCase>(doc.HandbackCase, ignoreCase: true, out var parsed)
        && parsed != SpecHandbackCase.None
            ? new SpecHandback(parsed, doc.HandbackReason ?? string.Empty)
            : null;
}

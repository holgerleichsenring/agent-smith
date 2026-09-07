using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: one YamlDotNet configuration shared by emit and consume, so a set this
/// system writes is always a set this system can read back — the p0193 one-builder
/// rule applied to the spec set. The document shape is <see cref="SpecSetIndexDocument"/>.
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
            HandbackReadings = [.. set.Handback?.Readings ?? []],
            HandbackTaken = set.Handback?.Taken ?? 0,
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
            ? new SpecHandback(
                parsed, doc.HandbackReason ?? string.Empty,
                Readings: doc.HandbackReadings, Taken: doc.HandbackTaken)
            : null;
}

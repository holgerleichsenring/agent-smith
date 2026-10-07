using System.Globalization;
using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7d: the series manifest's one YamlDotNet configuration, shared by emit and
/// consume, so a manifest this system writes is always one it reads back. Replaces the p0393a
/// <c>set.yaml</c> index; the document shape is <see cref="SeriesManifestDocument"/>.
/// </summary>
public sealed class SeriesManifest
{
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
        var doc = new SeriesManifestDocument
        {
            Ticket = set.Key,
            Goal = set.Goal,
            TicketPinnedWhole = set.TicketPinnedWhole,
            Specs = [.. set.Phases.Select(p => p.PhaseId)],
            Revisions = [.. set.Revisions.Select(r => new SeriesRevisionEntry
            {
                Number = r.Number, Cause = r.Cause, At = r.At.ToString("O", CultureInfo.InvariantCulture),
            })],
            TicketFingerprint = set.TicketFingerprint,
        };
        WriteAccounting(doc, set.Accounting);
        WriteHandbackAndApproval(doc, set);
        return _serializer.Serialize(doc);
    }

    public SeriesManifestDocument? Parse(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        try { return _deserializer.Deserialize<SeriesManifestDocument>(yaml); }
        catch (YamlException) { return null; }
        catch (InvalidCastException) { return null; }
    }

    /// <summary>2026-09-17-0e79a: the approval the published series came from, or null.</summary>
    public SpecApproval? ApprovalOf(SeriesManifestDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return DateTimeOffset.TryParse(
            doc.ApprovedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? new SpecApproval(at, doc.ApprovedInConversation ?? string.Empty, doc.ApprovedBy ?? string.Empty)
            : null;
    }

    /// <summary>The fingerprint the manifest carries, or null when it carries none.</summary>
    public string? FingerprintOf(SeriesManifestDocument doc) =>
        string.IsNullOrWhiteSpace(doc.TicketFingerprint) ? null : doc.TicketFingerprint.Trim();

    public SpecAccounting AccountingOf(SeriesManifestDocument doc) => new(
        [.. doc.Carried.Select(c => new CarriedSegment(c.Segment, c.Phase))],
        [.. doc.Discarded.Select(d => new DiscardedSegment(d.Segment, d.Reason))],
        [.. doc.Unaccounted],
        [.. doc.DiscardedContexts.Select(d => new DiscardedContext(d.Context, d.Reason))]);

    public IReadOnlyList<SpecRevision> RevisionsOf(SeriesManifestDocument doc) =>
        doc.Revisions.Count == 0
            ? [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)]
            : [.. doc.Revisions.Select(r => new SpecRevision(
                r.Number, r.Cause,
                DateTimeOffset.TryParse(r.At, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                    ? at : DateTimeOffset.UtcNow))];

    public SpecHandback? HandbackOf(SeriesManifestDocument doc) =>
        Enum.TryParse<SpecHandbackCase>(doc.HandbackCase, ignoreCase: true, out var parsed)
        && parsed != SpecHandbackCase.None
            ? new SpecHandback(
                parsed, doc.HandbackReason ?? string.Empty,
                Readings: doc.HandbackReadings, Taken: doc.HandbackTaken)
            : null;

    private static void WriteAccounting(SeriesManifestDocument doc, SpecAccounting accounting)
    {
        doc.Carried = [.. accounting.Carried.Select(
            c => new SeriesCarriedEntry { Segment = c.SegmentId, Phase = c.PhaseId })];
        doc.Discarded = [.. accounting.Discarded.Select(
            d => new SeriesDiscardedEntry { Segment = d.SegmentId, Reason = d.Reason })];
        doc.Unaccounted = [.. accounting.Unaccounted];
        doc.DiscardedContexts = [.. accounting.DiscardedContexts.Select(
            d => new SeriesDiscardedContextEntry { Context = d.Context, Reason = d.Reason })];
    }

    private static void WriteHandbackAndApproval(SeriesManifestDocument doc, SpecSet set)
    {
        doc.HandbackCase = set.Handback?.Case.ToString();
        doc.HandbackReason = set.Handback?.Reason;
        doc.HandbackReadings = [.. set.Handback?.Readings ?? []];
        doc.HandbackTaken = set.Handback?.Taken ?? 0;
        doc.ApprovedAt = set.Approval?.At.ToString("O", CultureInfo.InvariantCulture);
        doc.ApprovedInConversation = set.Approval?.Conversation;
        doc.ApprovedBy = set.Approval?.Principal;
    }
}

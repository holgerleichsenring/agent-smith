using System.Globalization;
using AgentSmith.Contracts.Specs;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7d: the series manifest's one YamlDotNet configuration. Replaces the p0393a
/// <c>set.yaml</c> index; the document shape is <see cref="SeriesManifestDocument"/>.
/// <para>2026-11-28-db88a: READ ONLY — no manifest is written any more; one on a branch is read once, to import the set
/// of a ticket in flight at that deploy (follow-up 2026-11-28-5e0b deletes it).</para>
/// </summary>
public sealed class SeriesManifest
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

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
        [.. doc.Carried.Select(c => new CarriedSegment(c.Segment, c.Spec))],
        [.. doc.Discarded.Select(d => new DiscardedSegment(d.Segment, d.Reason))],
        [.. doc.Unaccounted],
        [.. doc.DiscardedContexts.Select(d => new DiscardedContext(d.Context, d.Reason))]) { Shared = [.. doc.Shared] };

    public IReadOnlyList<SpecRevision> RevisionsOf(SeriesManifestDocument doc) =>
        doc.Revisions.Count == 0
            ? [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)]
            : [.. doc.Revisions.Select(r => new SpecRevision(
                r.Number, r.Cause,
                DateTimeOffset.TryParse(r.At, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                    ? at : DateTimeOffset.UtcNow))];

    /// <summary>2026-10-12-f5f7b: the current revision's time when it parses, null otherwise — a
    /// manifest whose time is unparsed is never the newest one.</summary>
    public DateTimeOffset? CurrentRevisionTimeOf(SeriesManifestDocument doc) =>
        doc.Revisions.Count > 0 && DateTimeOffset.TryParse(doc.Revisions[^1].At, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var at) ? at : null;

    public SpecHandback? HandbackOf(SeriesManifestDocument doc) =>
        Enum.TryParse<SpecHandbackCase>(doc.HandbackCase, ignoreCase: true, out var parsed)
        && parsed != SpecHandbackCase.None
            ? new SpecHandback(
                parsed, doc.HandbackReason ?? string.Empty,
                Readings: doc.HandbackReadings, Taken: doc.HandbackTaken)
            { QuestionKind = Enum.TryParse<SpecQuestionKind>(doc.HandbackQuestionKind, true, out var kind) ? kind : default }
            : null;
}

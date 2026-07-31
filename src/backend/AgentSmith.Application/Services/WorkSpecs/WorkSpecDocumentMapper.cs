using AgentSmith.Application.Services.WorkSpecs.Yaml;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: pure mapping from the mutable YamlDotNet shape to the immutable
/// contract record. Split out so the serializer stays a serializer.
/// </summary>
internal static class WorkSpecDocumentMapper
{
    public static WorkSpec ToSpec(WorkSpecDocument doc) => new(
        doc.Key ?? string.Empty,
        doc.Goal ?? string.Empty,
        doc.Requirements ?? [],
        [.. (doc.Constraints ?? []).Select(ToConstraint)],
        doc.Done ?? [],
        doc.DoneIsReadOnly,
        doc.Assumptions ?? [],
        ToRevisions(doc.Revisions),
        ToHandback(doc));

    private static WorkSpecConstraint ToConstraint(WorkSpecConstraintEntry entry) =>
        new(entry.Rule ?? string.Empty,
            string.IsNullOrWhiteSpace(entry.SampleAnchor) ? null : entry.SampleAnchor.Trim());

    // A spec with no revision header is malformed; it gets a synthetic first
    // revision rather than a crash on WorkSpec.Current, so a hand-edited file
    // that dropped the block is still readable input to the next revision.
    private static IReadOnlyList<WorkSpecRevision> ToRevisions(List<WorkSpecRevisionEntry>? entries)
    {
        if (entries is null || entries.Count == 0)
            return [new WorkSpecRevision(1, "recovered (revision header missing)", DateTimeOffset.UtcNow)];
        return [.. entries.Select(e => new WorkSpecRevision(
            e.Number, e.Cause ?? string.Empty, ParseTimestamp(e.At)))];
    }

    private static DateTimeOffset ParseTimestamp(string? raw) =>
        DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    private static WorkSpecHandback? ToHandback(WorkSpecDocument doc)
    {
        if (string.IsNullOrWhiteSpace(doc.HandbackCase)) return null;
        if (!Enum.TryParse<WorkSpecHandbackCase>(doc.HandbackCase, ignoreCase: true, out var parsed))
            return null;
        return parsed == WorkSpecHandbackCase.None
            ? null
            : new WorkSpecHandback(parsed, doc.HandbackReason ?? string.Empty);
    }
}

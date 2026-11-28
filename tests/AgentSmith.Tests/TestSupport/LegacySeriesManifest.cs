using System.Globalization;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-28-db88a: the series manifest exactly as the writer before 2026-11-28-db88a rendered it (moved here from
/// SeriesManifest.Serialize, which production no longer has). It arranges what a ticket in flight at that deploy carries on
/// its branch — the input of the one-time import — and nothing else.
/// </summary>
internal static class LegacySeriesManifest
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>The manifest the old writer put under series/ for <paramref name="set"/>.</summary>
    public static string Serialize(this SeriesManifest _, SpecSet set) => Write(set);

    public static string Write(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var doc = new SeriesManifestDocument
        {
            Ticket = set.Key,
            Goal = set.Goal,
            TicketPinnedWhole = set.TicketPinnedWhole,
            Specs = [.. set.Specs.Select(p => p.SpecId)], StandIns = set.StandIns.Count > 0 ? [.. set.StandIns] : null,
            SeriesRepos = set.SeriesRepos is { Count: > 0 } members ? [.. members] : null,
            Revisions = [.. set.Revisions.Select(r => new SeriesRevisionEntry
            {
                Number = r.Number, Cause = r.Cause, At = r.At.ToString("O", CultureInfo.InvariantCulture),
            })],
            TicketFingerprint = set.TicketFingerprint, Contract = SeriesContractYaml.Write(set.Contract),
        };
        doc.Carried = [.. set.Accounting.Carried.Select(c => new SeriesCarriedEntry { Segment = c.SegmentId, Spec = c.SpecId })];
        doc.Discarded = [.. set.Accounting.Discarded.Select(d => new SeriesDiscardedEntry { Segment = d.SegmentId, Reason = d.Reason })];
        (doc.Unaccounted, doc.Shared) = ([.. set.Accounting.Unaccounted], [.. set.Accounting.Shared]);
        doc.DiscardedContexts = [.. set.Accounting.DiscardedContexts.Select(
            d => new SeriesDiscardedContextEntry { Context = d.Context, Reason = d.Reason })];
        doc.HandbackCase = set.Handback?.Case.ToString();
        doc.HandbackReason = set.Handback?.Reason;
        doc.HandbackReadings = [.. set.Handback?.Readings ?? []];
        doc.HandbackTaken = set.Handback?.Taken ?? 0;
        doc.HandbackQuestionKind = set.Handback?.QuestionKind is { } kind and not SpecQuestionKind.Reading ? kind.ToString() : null;
        doc.ApprovedAt = set.Approval?.At.ToString("O", CultureInfo.InvariantCulture);
        doc.ApprovedInConversation = set.Approval?.Conversation;
        doc.ApprovedBy = set.Approval?.Principal;
        return Serializer.Serialize(doc);
    }
}

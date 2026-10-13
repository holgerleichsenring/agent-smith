using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59g: the conversation family — one <see cref="RunConversationEntry"/> per event that is a
/// turn of a ticket's conversation, written once per trail position. A fetched ticket keeps its description
/// and criteria (the title stays on the run row); every park on a question keeps that question, which the
/// single checkpoint row overwrites; a hand-back keeps its posted words and quote; every cut review keeps its
/// verdict. A fetch or a park without a ticket id writes nothing; a hand-back and a cut review are only ever
/// made over a ticket.
/// </summary>
public sealed class RunConversationProjection(ProjectedEventRecords records) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
    [
        typeof(TicketFetchedEvent), typeof(RunCheckpointedEvent),
        typeof(SpecHandedBackEvent), typeof(SpecCutReviewedEvent),
    ];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) =>
        EntryOf(runEvent) is { } entry ? AddOnceAsync(uow, entry, eventSeq, ct) : Task.CompletedTask;

    internal static RunConversationEntry? EntryOf(RunEvent runEvent) => runEvent switch
    {
        TicketFetchedEvent e when e.TicketId.Length > 0 => Entry(e, RunConversationKinds.TicketText,
            e.Description, e.AcceptanceCriteria is null ? null : RunStoryJson.Serialize(new { e.AcceptanceCriteria })),
        RunCheckpointedEvent e when e.TicketId.Length > 0 => Entry(e, RunConversationKinds.Question,
            QuestionText(e.QuestionJson), null),
        SpecHandedBackEvent e => Entry(e, RunConversationKinds.HandBack,
            e.Text, e.Quote is null ? null : RunStoryJson.Serialize(new { e.Quote })),
        SpecCutReviewedEvent e => Entry(e, RunConversationKinds.CutReview,
            null, RunStoryJson.Serialize(new { e.Attempt, e.Deliverable, e.Findings })),
        _ => null,
    };

    private async Task AddOnceAsync(IUnitOfWork uow, RunConversationEntry entry, long? eventSeq, CancellationToken ct)
    {
        if (await records.HoldsAsync<RunConversationEntry>(uow, entry.RunId, eventSeq, ct)) return;
        entry.EventSeq = eventSeq;
        uow.Add(entry);
        await uow.SaveChangesAsync(ct);
    }

    private static RunConversationEntry Entry(RunEvent e, string kind, string? text, string? detailJson) => new()
    {
        RunId = e.RunId, Kind = kind, At = e.Timestamp, Text = text, DetailJson = detailJson,
        QuestionId = (e as RunCheckpointedEvent)?.QuestionId,
    };

    private static string? QuestionText(string questionJson)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<DialogQuestion>(questionJson)?.Text;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }
}

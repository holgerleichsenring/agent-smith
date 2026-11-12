using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4ca: write_ticket — one ticket text into the turn's draft, added or replacing its key.</summary>
public sealed class TicketWriteToolHost(ProposalStore store, TicketDraftWrite write, IReadOnlyList<string> scope) : IToolHost
{
    public const string Name = "write_ticket";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteTicket, name: Name)];
    }

    [Description(
        "Write one ticket into this turn's draft: a ticket text the run it starts derives its own specs from. A key "
        + "already in the draft is replaced. Tickets file one ticket each, in the order their requires allow, and each "
        + "run drafts its own specs; keep a ticket small enough for one run to build, review and merge. English "
        + "whatever the conversation's language. The answer is the exact check of this ticket.")]
    private string WriteTicket(
        [Description("Lower-case letter, then lower-case letters, digits or '-'; unique in the draft.")] string key,
        [Description("One line.")] string title,
        [Description("The ticket's scope in prose, against the code as it stands once its requires are merged.")] string requirement,
        [Description("Outcomes true once it has merged, one line each (a scenario as WHEN … THEN …).")] string[] done,
        [Description("Keys of tickets already in this draft that must merge first, or ticket:<tracker id> of a filed ticket (ticket:41 for #41).")] string[]? requires = null,
        [Description("The configured repositories it changes.")] string[]? repos = null)
    {
        var ticket = new GraphTicket((key ?? string.Empty).Trim(), (title ?? string.Empty).Trim(),
            (requirement ?? string.Empty).Trim(), Trimmed(done), Trimmed(requires), Trimmed(repos));
        var refusal = store.Apply(draft => write.Write(draft, ticket, scope));
        return DraftWriteAnswer.Of(refusal, $"Stored ticket '{ticket.Key}'.", store);
    }

    private static IReadOnlyList<string> Trimmed(string[]? items) =>
        [.. (items ?? []).Select(item => (item ?? string.Empty).Trim())];
}

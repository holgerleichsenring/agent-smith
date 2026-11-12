using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4ca: write_comment — the comment a ticket's conversation proposes to post on it, replacing the draft's.</summary>
public sealed class CommentWriteToolHost(ProposalStore store) : IToolHost
{
    public const string Name = "write_comment";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteComment, name: Name)];
    }

    [Description("Write the comment this turn proposes for the bound ticket, when it should not be built as written. Approving it posts the comment and starts nothing.")]
    private string WriteComment([Description("The comment, for the ticket's readers, in the ticket's language.")] string comment)
    {
        var refusal = store.Apply(draft => CommentDraftWrite.Write(draft, comment));
        return DraftWriteAnswer.Of(refusal, "Stored the comment.", store);
    }
}

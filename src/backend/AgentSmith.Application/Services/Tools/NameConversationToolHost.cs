using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-04-96a0a: name_conversation — the design master names a conversation that has no subject yet, in the
/// language it answers in. The name lands on the turn's reply slot and is stored when the turn is kept; a refused
/// name comes back with the rule it broke, so the model can name it again. The last admitted call of a turn wins.
/// </summary>
public sealed class NameConversationToolHost(SpecDialogReplySlot slot) : IToolHost
{
    public const string Name = "name_conversation";

    internal const string Stored = "Stored. Write your reply; do not mention the name.";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(NameConversation, name: Name)];
    }

    [Description(
        "Name this conversation for the heading above it: one line naming what the conversation is about, in the "
        + "language you answer the operator in. Call it before writing the reply. It is not a source read.")]
    private string NameConversation(
        [Description("One line of plain prose, no markup, at most 120 characters.")] string subject)
    {
        var verdict = ConversationSubjectAdmission.Of(subject);
        if (verdict.Subject is null) return $"Not stored: {verdict.Refusal} Name it again as one line of plain prose.";
        slot.Subject = verdict.Subject;
        return Stored;
    }
}

using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-12-cd4ca: write_bug — the fix-bug ticket a small change files as, replacing the draft's bug.</summary>
public sealed class BugWriteToolHost(ProposalStore store) : IToolHost
{
    public const string Name = "write_bug";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(WriteBug, name: Name)];
    }

    [Description("Write the bug this turn proposes: a small fix filed as one fix-bug ticket the bug pipeline executes as it stands.")]
    private string WriteBug(
        [Description("One imperative line naming the fix.")] string title,
        [Description("What is wrong, where, and what correct behaviour looks like.")] string description,
        [Description("How the fix is verified, one line each.")] string[]? acceptance_criteria = null)
    {
        var refusal = store.Apply(draft => BugDraftWrite.Write(draft, title, description, acceptance_criteria ?? []));
        return DraftWriteAnswer.Of(refusal, "Stored the bug.", store);
    }
}

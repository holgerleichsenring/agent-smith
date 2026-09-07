using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: one derivation call — the model call itself, with the tools it may
/// look with, its scope in the run trail and its row in the cost ledger. Split from
/// <see cref="SpecSetDeriver"/> the way <see cref="SpecAccountCall"/> is split from the
/// accountant: what the call is offered is a different question from what its answer is
/// worth, and the deriver was at the file-length ceiling.
/// <para>
/// The iteration cap is the look allowance plus the turn that asks and the turn that
/// answers, as <see cref="AccountTools.MaxIterations"/> already does — so an exhausted
/// budget is answered in text by the tool and never trips the cap into an empty reply.
/// The whole response is returned, not its text: a retry has to see the looks the
/// previous attempt took, and those travel as tool-call and tool-result messages.
/// </para>
/// </summary>
public sealed class SpecDerivationCall(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext)
{
    public const string RoleName = "spec-derivation";

    /// <summary>Per request — every attempt gets it over again, while the look budget
    /// the tools draw on is one allowance for all of them.</summary>
    public const int MaxIterations = DerivationLookBudget.Allowance + 2;

    public async Task<ChatResponse> AskAsync(
        AgentConfig agentConfig, PipelineContext pipeline, IReadOnlyList<ChatMessage> messages,
        IList<AITool>? tools, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var chat = chatClientFactory.Create(agentConfig, TaskType.Planning, MaxIterations);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Planning);
        using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString());
        var response = await chat.GetResponseAsync(
            messages, new ChatOptions { MaxOutputTokens = maxTokens, Tools = tools }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        return response;
    }
}

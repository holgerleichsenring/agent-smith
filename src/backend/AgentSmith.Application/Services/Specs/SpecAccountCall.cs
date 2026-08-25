using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: one accounting call — the model call itself, with the bookkeeping every model
/// call in this system owes: its own role in the cost ledger and its own scope in the run
/// trail. Unattributed spend is invisible spend.
/// </summary>
public sealed class SpecAccountCall(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<SpecAccountCall> logger)
{
    public const string RoleName = "spec-accountant";

    public async Task<IReadOnlyList<AccountRow>?> AskAsync(
        IChatClient chat, string repoKey, IReadOnlyList<string> criteria, string diff,
        IReadOnlyList<string>? searchable,
        IReadOnlyList<string> commandResults, PipelineCostTracker costTracker, CancellationToken ct,
        string? correction = null, IList<AITool>? tools = null,
        CitedFileIndex? deliveryFiles = null, IReadOnlyList<string>? baseSearchable = null)
    {
        // p0474: a correction is appended, never substituted — the second answer is judged
        // against the same evidence as the first, so it needs the same prompt behind it.
        var prompt = SpecAccountPrompt.For(
                criteria, diff, commandResults, searchable, deliveryFiles, baseSearchable)
            + (correction is null ? string.Empty : "\n\n" + correction);
        try
        {
            // The account is a model call like any other: it belongs in the cost ledger
            // and in the run trail, under its own role, or it is spend nobody can see.
            using var _ = costTracker.BeginCall(
                RoleName, RoleName, SkillExecutionPhase.Verify, repoKey);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Verify.ToString(), repoKey);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = tools }, ct);
            costTracker.Track(response);
            return SpecAccountReader.Read(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The accounting call failed");
            return null;
        }
    }
}

using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-08-26-167c: persists a bootstrap round's Markdown summary into the pipeline
/// context — SkillOutputs, the per-(repo, context) BootstrapOutputs trail
/// WriteRunResult's init-mode fan-out reads, and the DiscussionLog.
/// <para>
/// Lifted out of BootstrapRoundHandler, which sat exactly on its file-length
/// baseline: recording where a round's prose goes is not the same responsibility as
/// running the round.
/// </para>
/// </summary>
public sealed class BootstrapOutputRecorder
{
    public void Record(
        BootstrapRoundContext context, RoleSkillDefinition role, string responseText)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(role);
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var outputs)
            || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[context.SkillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);

        AppendBootstrapOutput(pipeline, context.RepoName, context.ContextName, responseText);

        if (!pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussion)
            || discussion is null)
            discussion = [];
        discussion.Add(new DiscussionEntry(
            context.SkillName, role.DisplayName, role.Emoji, Round: 1, responseText));
        pipeline.Set(ContextKeys.DiscussionLog, discussion);
    }

    // p0161d: the (repo, context) → markdown trail used by WriteRunResultHandler's
    // init-mode fan-out. An empty contextName uses "default" so legacy single-context
    // runs land in a predictable slot.
    private static void AppendBootstrapOutput(
        PipelineContext pipeline, string repoName, string contextName, string output)
    {
        if (!pipeline.TryGet<Dictionary<string, Dictionary<string, string>>>(
                ContextKeys.BootstrapOutputs, out var byRepo) || byRepo is null)
            byRepo = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (!byRepo.TryGetValue(repoName, out var byContext))
        {
            byContext = new Dictionary<string, string>(StringComparer.Ordinal);
            byRepo[repoName] = byContext;
        }
        byContext[string.IsNullOrEmpty(contextName) ? "default" : contextName] = output;
        pipeline.Set(ContextKeys.BootstrapOutputs, byRepo);
    }
}

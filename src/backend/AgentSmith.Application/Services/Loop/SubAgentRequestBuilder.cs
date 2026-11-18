using System.Security.Cryptography;
using System.Text;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: builds a child's first loop request from its spec — who it is, where it works, the tool surface its master
/// granted and how far it may go. 2026-11-18-c8e2a: each granted function hands on what the master writes to it.
/// </summary>
public sealed class SubAgentRequestBuilder
{
    public AgenticLoopRequest Build(SubAgentMember member, SubAgentContext context)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(context);
        var spec = member.Spec;
        // The master grants the child tool surface: read-only for a scan master, read/write
        // for a coding master, never spawn_agents.
        var tools = context.ChildTools
            .Select(t => t is AIFunction f ? new MidTurnMessageAIFunction(f, member.Inbox) : t).ToList();
        var systemPrompt = SystemPrompt(spec) + SubAgentSituation.Render( // 2026-11-09-0620e
            context.Sandboxes.Keys, tools, context.ChildIterationCeiling ?? context.AgentConfig.MaxSubAgentLoopIterations);
        // The master's real AgentConfig, passed on the context: the coding-master path never
        // publishes it to the pipeline, so a lookup there would fail every child at Create.
        var agentConfig = context.AgentConfig;
        return new AgenticLoopRequest(
            AgentConfig: agentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: systemPrompt,
            UserPrompt: UserPrompt(spec),
            Tools: tools,
            InheritedContext: spec.InheritedContext,
            Name: spec.Name,
            Activity: spec.Activity,
            SubAgentId: member.Id,
            ParentSubAgentId: context.ParentSubAgentId,
            // p0341c: a child gets a REAL iteration budget and no governor hooks; its cost rolls up to the shared
            // tracker. 2026-09-22-5891: a master that declared a ceiling for ITS children is honoured over the config's.
            MaxIterations: context.ChildIterationCeiling ?? agentConfig.MaxSubAgentLoopIterations);
    }

    /// <summary>A short hash of what the child inherited, for the spawned event.</summary>
    public string HashInheritedContext(InheritedContext ctx)
    {
        var payload = $"{ctx.PipelineGoal}\n{ctx.PriorContextSlice}\n{ctx.OptionalSystemPromptBlock}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return "sha256:" + Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private static string SystemPrompt(SubAgentSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are {spec.Name}.");
        sb.AppendLine($"Activity: {spec.Activity}");
        if (!string.IsNullOrWhiteSpace(spec.InheritedContext.OptionalSystemPromptBlock))
            sb.AppendLine(spec.InheritedContext.OptionalSystemPromptBlock);
        return sb.ToString();
    }

    private static string UserPrompt(SubAgentSpec spec)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pipeline goal: {spec.InheritedContext.PipelineGoal}");
        sb.AppendLine();
        sb.AppendLine("Prior context:");
        sb.AppendLine(spec.InheritedContext.PriorContextSlice);
        sb.AppendLine();
        sb.AppendLine($"Your task: {spec.TaskDescription}");
        if (!string.IsNullOrWhiteSpace(spec.OutputHint))
        {
            sb.AppendLine();
            sb.AppendLine($"Output hint: {spec.OutputHint}");
        }
        return sb.ToString();
    }
}

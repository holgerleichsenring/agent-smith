using System.Text;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-6811c: the embedded master a refresh-base run hands a base merge to when the unions left paths unmerged or a
/// stage ran red over the merged tree — one loop shape for both, like the series check's (0207d): the server's own body,
/// FilesystemToolHost's tools on the primary sandbox, and a conclude tool. Its cost is the run's.
/// 2026-11-24-9dc0a: a pass opens a <see cref="BaseMergeSession"/> that a later pass continues — the same conversation and
/// the same file tools; a conflict pass's conclude asks <see cref="BaseMergeWholeness"/> before it records whole.
/// </summary>
public sealed class BaseMergeMaster(IAgenticLoopRunner loop, IPromptCatalog prompts, BaseMergeWholeness wholeness) : IBaseMergeMaster
{
    public const string MasterName = "base-merge-master";

    public async Task<BaseMergePass> RunAsync(BaseMergeSituation situation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(situation);
        var timeout = situation.Pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var seconds) && seconds > 0 ? seconds : (int?)null;
        var session = new BaseMergeSession(situation,
            new FilesystemToolHost(situation.Sandbox, Repository.SandboxWorkPath, runCommandTimeoutSeconds: timeout));
        var conclude = Conclude(session);
        var request = Request(situation, Prompt(situation), session.Tools(conclude), null);
        session.Conversation.Opened(request, await RunAsync(situation, request, ct));
        return session.Pass(conclude);
    }

    public async Task<BaseMergePass> ContinueAsync(BaseMergePass earlier, string note, bool concludeOnly, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        var session = earlier.Session ?? throw new ArgumentException("A pass no master ran cannot be continued.", nameof(earlier));
        var conclude = concludeOnly ? new BaseMergeConcludeTool() : Conclude(session);
        IList<AITool> tools = concludeOnly ? [conclude.GetTool()] : session.Tools(conclude);
        var request = Request(session.Situation, note, tools, session.Conversation.Thread());
        session.Conversation.Continued(note, await RunAsync(session.Situation, request, ct));
        return session.Pass(conclude);
    }

    private async Task<ChatResponse> RunAsync(BaseMergeSituation situation, AgenticLoopRequest request, CancellationToken ct)
    {
        var result = await loop.RunAsync(request, ct);
        PipelineCostTracker.GetOrCreate(situation.Pipeline).Track(result.Response);
        return result.Response;
    }

    private AgenticLoopRequest Request(BaseMergeSituation situation, string prompt, IList<AITool> tools, IReadOnlyList<ChatMessage>? prior) =>
        new(situation.Project.Agent, TaskType.Primary, prompts.Get(MasterName), prompt, tools, Name: MasterName,
            Compaction: situation.Project.Agent.Compaction, PriorMessages: prior);

    // A conflict pass checks the tree before whole is recorded; a repair pass is checked by the stages.
    private BaseMergeConcludeTool Conclude(BaseMergeSession session) =>
        session.Situation.Unmerged.Count == 0 ? new BaseMergeConcludeTool()
            : new BaseMergeConcludeTool(async c => await wholeness.CheckAsync(session.Situation.Sandbox, session.Situation.Unmerged,
                session.WrotePaths(), c) is { IsWhole: false } open ? open.Say() : null);

    /// <summary>The pass's input: the branch, the base, both sides' specs, and the unmerged paths or the red stage.</summary>
    public static string Prompt(BaseMergeSituation situation)
    {
        ArgumentNullException.ThrowIfNull(situation);
        var text = new StringBuilder($"Merging {situation.BaseRef} into {situation.Branch}.\n");
        text.Append($"Specs this branch added: {List(situation.OurSpecs)}\n");
        text.Append($"Specs that arrived on {situation.BaseRef}: {List(situation.TheirSpecs)}\n");
        if (situation.Unmerged.Count > 0)
            text.Append($"\nGit could not merge these paths; they carry conflict markers:\n- {string.Join("\n- ", situation.Unmerged)}\n");
        if (situation.Red is { } red)
            text.Append($"\nThe merge is committed, and the {red.Stage} stage '{red.Command}' ran red over it (exit {red.ExitCode}). "
                + $"Its output ends:\n{VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars)}\n");
        return text.ToString();
    }

    private static string List(IReadOnlyList<string> paths) => paths.Count == 0 ? "none" : string.Join(", ", paths);
}

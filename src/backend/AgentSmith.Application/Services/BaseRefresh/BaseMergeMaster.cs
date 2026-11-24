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
/// </summary>
public sealed class BaseMergeMaster(IAgenticLoopRunner loop, IPromptCatalog prompts) : IBaseMergeMaster
{
    public const string MasterName = "base-merge-master";

    public async Task<BaseMergePass> RunAsync(BaseMergeSituation situation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(situation);
        var timeout = situation.Pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var seconds) && seconds > 0 ? seconds : (int?)null;
        var fs = new FilesystemToolHost(situation.Sandbox, Repository.SandboxWorkPath, runCommandTimeoutSeconds: timeout);
        var conclude = new BaseMergeConcludeTool();
        IList<AITool> tools = [.. fs.GetTools(null, null).Select(BoundedResultTool.Wrap), conclude.GetTool()];
        var request = new AgenticLoopRequest(situation.Project.Agent, TaskType.Primary, prompts.Get(MasterName),
            Prompt(situation), tools, Name: MasterName, Compaction: situation.Project.Agent.Compaction);
        var result = await loop.RunAsync(request, ct);
        PipelineCostTracker.GetOrCreate(situation.Pipeline).Track(result.Response);
        return new BaseMergePass(conclude.Whole == true,
            conclude.Reason ?? "the master ended without saying whether the merge is whole", fs.GetChanges());
    }

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

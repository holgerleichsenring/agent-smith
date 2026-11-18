using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207d: what the verify master works with — the checked-out default branch to read, the
/// repository's own commands to run, and the criterion record.</summary>
public static class SeriesVerifySurface
{
    public static IList<AITool> Tools(PipelineContext pipeline, SeriesVerdictTool verdict)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var sandboxes = pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var repository = pipeline.Get<Repository>(ContextKeys.Repository);
        var fs = new FilesystemToolHost(sandboxes, sandboxes.Keys.First(), repository.LocalPath,
            keyToRepo: pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var map) ? map : null);
        return [.. fs.GetTools(null, null).Where(t => Reads(t.Name))
            .Select(BoundedResultTool.Wrap), verdict.GetTool()];
    }

    private static bool Reads(string tool) => tool is "read_file" or "grep_in_file" or "grep_in_tree" or "find_files"
        or "list_directory" or "directory_tree" or "run_command";
}

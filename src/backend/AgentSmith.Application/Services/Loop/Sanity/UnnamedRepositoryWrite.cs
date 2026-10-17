namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// 2026-10-17-a196a: a write that would land in a repository the current spec does not name — write_file,
/// edit or multi_edit on a path that routes to such a repository — judged BEFORE the call. A multi_edit dry
/// run writes nothing; an unprefixed path routes to the one repository of a single-repository run and is not
/// judged on a multi-repository one; a run-record path (<see cref="RunRecordPaths.IsRunRecordPath"/>) may be
/// written in any repository; a spec that names no repositories turns the check off. Until a196a this was an
/// annotation on a write that had already landed (2026-10-13-8762d).
/// </summary>
public sealed class UnnamedRepositoryWrite
{
    /// <summary>The refusal for this call, without the "Error: " head; null when the write may run.</summary>
    public string? Refusal(ToolResultSanityCall call, ToolResultSanityScope scope)
    {
        if (call.Tool is not ("write_file" or "edit" or "multi_edit")) return null;
        if (scope.NamedRepositories.Count == 0) return null;
        if (call.Tool == "multi_edit" && call.Flag("dry_run")) return null;

        var target = scope.Normalise(call.Argument("path"));
        var repository = target.Repository ?? scope.SingleRepository;
        if (repository is null || RunRecordPaths.IsRunRecordPath(target.Path)
            || scope.NamedRepositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
            return null;
        return $"{call.Tool} into {target.Path} of repository '{repository}' was refused: this spec is written into "
               + $"{string.Join(", ", scope.NamedRepositories)} alone, and '{repository}' belongs to another spec. "
               + "Nothing was written. If this spec cannot be done without that change, say so in your verdict.";
    }
}

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// A write that landed in a repository the current spec does not name: write_file, edit or
/// multi_edit succeeded (its result does not start with "Error") on a path that routes to such a
/// repository. A multi_edit dry run writes nothing; an unprefixed path routes to the one
/// repository of a single-repository run and is not judged on a multi-repository one; a spec that
/// names no repositories turns the rule off.
/// </summary>
public sealed class UnnamedRepositoryWriteRule : IToolResultSanityRule
{
    public string? Observe(ToolResultSanityCall call, ToolResultSanityScope scope)
    {
        if (call.Tool is not ("write_file" or "edit" or "multi_edit")) return null;
        if (scope.NamedRepositories.Count == 0 || ToolResultSanity.IsError(call.Result)) return null;
        if (call.Tool == "multi_edit" && call.Flag("dry_run")) return null;

        var target = scope.Normalise(call.Argument("path"));
        var repository = target.Repository ?? scope.SingleRepository;
        if (repository is null || scope.NamedRepositories.Contains(repository, StringComparer.OrdinalIgnoreCase))
            return null;
        return $"{call.Tool} wrote {target.Path} into repository '{repository}', which this spec does not name "
               + $"(it names {string.Join(", ", scope.NamedRepositories)}) — check that this change belongs to this "
               + "spec before building on it.";
    }
}

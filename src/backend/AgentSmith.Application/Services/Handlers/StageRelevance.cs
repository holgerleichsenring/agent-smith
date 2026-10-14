using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Which declared stages a sandbox runs in this verification.
/// <para>
/// A stage naming <c>when_changed</c> globs runs when its REPOSITORY's branch changed a
/// matching path — in any of the repository's sandboxes, because a stage guards what its
/// command reads, and that can be work another toolchain's sandbox delivered. A stage
/// without globs runs exactly when its own sandbox's branch carries source, as it always did.
/// </para>
/// <para>
/// The changed paths come from git, not from the delivery diff's text: that text is cut at
/// the stdout cap and lists paths in order, so a late path would vanish from a long series'
/// diff and its guarded stage would be skipped as untouched. The read is NUL-separated and
/// unquoted, so a non-ASCII path is matched as written; renames count under both names.
/// </para>
/// </summary>
public sealed class StageRelevance(SandboxTargets sandboxTargets, ILogger<StageRelevance> logger)
{
    private const int GitTimeoutSeconds = 120;

    public async Task<RepositoryChanges> ReadAsync(
        PipelineContext pipeline, IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, DeliveryDiff.DiffResult> diffs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);
        ArgumentNullException.ThrowIfNull(diffs);
        var repositoryOf = sandboxes.Keys.ToDictionary(
            key => key, key => RepositoryOf(pipeline, key), StringComparer.Ordinal);
        var paths = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        var undetermined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
        {
            var repository = repositoryOf[key];
            var names = diffs.TryGetValue(key, out var diff) && !diff.Failed
                ? await NamesAsync(sandbox, diff.BaseRef ?? "HEAD", ct)
                : null;
            if (names is null) undetermined.Add(repository);
            var known = paths.TryGetValue(repository, out var set) ? set : new HashSet<string>(StringComparer.Ordinal);
            paths[repository] = known.Union(names ?? [], StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        }
        return new RepositoryChanges(repositoryOf, paths, undetermined);
    }

    /// <summary>
    /// The declared contexts with only the stages that run here, or null when the sandbox is
    /// skipped. A sandbox that declares nothing keeps today's rule and is handed on as it is;
    /// a declared sandbox left with no stage is skipped, never handed to the analyzer.
    /// </summary>
    public IReadOnlyList<ContextVerifyStages>? Select(
        string key, IReadOnlyList<ContextVerifyStages> declared, RepositoryChanges changes,
        DeliveredWork delivered, VerifyResolutionNotes notes)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(delivered);
        ArgumentNullException.ThrowIfNull(notes);
        var own = delivered.HasSomethingToProve(key);
        var kept = new List<ContextVerifyStages>();
        foreach (var context in declared)
        {
            var runs = context.Stages.Where(stage => Runs(key, stage, own, changes, notes)).ToList();
            if (runs.Count > 0) kept.Add(context with { Stages = runs });
        }
        if (declared.Count == 0 ? !own : kept.Count == 0)
        {
            logger.LogInformation("{Key}: no stage applies to what this branch changed — skipping build/test", key);
            return null;
        }
        changes.Ran(key);
        return declared.Count == 0 ? declared : kept;
    }

    private static bool Runs(
        string key, ContextYamlVerifyStage stage, bool own, RepositoryChanges changes, VerifyResolutionNotes notes)
    {
        if (stage.WhenChanged is not { Count: > 0 } globs) return own;
        if (changes.Touches(key, globs)) return true;
        notes.NotTouched(key, stage.Label, globs);
        changes.Untouched(key, globs);
        return false;
    }

    private string RepositoryOf(PipelineContext pipeline, string key)
    {
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) || repos is not { Count: > 0 })
            return key;
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners);
        return repos.FirstOrDefault(repo =>
            sandboxTargets.KeyBelongsToRepo(key, repo.Name, repos.Count > 1, owners))?.Name ?? key;
    }

    // Null = undetermined: the read failed, or its output reached the capture cap and may be cut.
    private static async Task<IReadOnlyList<string>?> NamesAsync(ISandbox sandbox, string against, CancellationToken ct)
    {
        var result = await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
                Args: ["-c", "core.quotePath=false", "diff", "--name-only", "--no-renames", "-z", against],
                WorkingDirectory: "/work", TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);
        var output = result.OutputContent ?? string.Empty;
        if (result.ExitCode != 0 || output.Length >= SizeLimits.RunStepCapturedStdoutMaxChars) return null;
        // The capture ends every stdout line with a newline; -z output is one line.
        if (output.EndsWith('\n')) output = output[..^1];
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}

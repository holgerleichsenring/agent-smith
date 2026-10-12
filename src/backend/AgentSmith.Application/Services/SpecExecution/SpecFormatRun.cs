using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-12-fbbdb: before the spec commit pushes, every sandbox runs the formatter its contexts
/// declare over the files THIS spec changed — measured against where the spec started, so an
/// earlier spec's files and untouched code stay as they are. It never fails: a formatter that
/// exits non-zero, or is not installed, is logged and returned as a note, and the work is pushed
/// as written.
/// </summary>
public sealed class SpecFormatRun(
    SandboxTargets targets, StructureGitReads git, SpecFormatPlan plan, DeclaredStagePresence presence,
    VerifyCommandRunner runner, ILogger<SpecFormatRun> logger)
{
    public const string Stage = "format";

    /// <summary>What the step result should say about formatting, or null when nothing went wrong.</summary>
    public async Task<string?> RunAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) || repos is null)
            return null;
        var notes = new List<string>();
        foreach (var repo in repos)
            foreach (var (key, sandbox) in targets.SandboxesForRepo(pipeline, repo))
                notes.AddRange(await RunSandboxAsync(pipeline, repo.Name, key, sandbox, ct));
        return notes.Count == 0 ? null : string.Join('\n', notes);
    }

    private async Task<IReadOnlyList<string>> RunSandboxAsync(
        PipelineContext pipeline, string repoName, string key, ISandbox sandbox, CancellationToken ct)
    {
        var declaring = await DeclaringAsync(pipeline, key, sandbox, ct);
        if (declaring.Count == 0) return [];
        if (SpecStartHeads.For(pipeline, key) is not { } head
            || await git.ChangedPathsAsync(sandbox, head, ct) is not { } changed)
        {
            logger.LogInformation("{Key}: no spec start head or no change listing; nothing is formatted", key);
            return [];
        }
        var invocations = plan.Plan(declaring, StructureRepoContexts.Of(pipeline, repoName, [key]), changed);
        var notes = new List<string>();
        foreach (var invocation in invocations)
            if (await RunOneAsync(key, sandbox, invocation, ct) is { } note) notes.Add(note);
        return notes;
    }

    // The sandbox's contexts that declare a formatter whose when_present path the tree carries.
    private async Task<IReadOnlyList<RemoteContextDiscovery>> DeclaringAsync(
        PipelineContext pipeline, string key, ISandbox sandbox, CancellationToken ct)
    {
        pipeline.TryGet<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, out var discoveries);
        var declaring = new List<RemoteContextDiscovery>();
        foreach (var context in SandboxContextList.InOr(pipeline, key, discoveries?.GetValueOrDefault(key)))
            if (context.Format is { } format && await presence.IsSatisfiedAsync(
                    key, new ContextYamlVerifyStage(Stage, format.Command, format.WhenPresent), sandbox, ct))
                declaring.Add(context);
        return declaring;
    }

    private async Task<string?> RunOneAsync(
        string key, ISandbox sandbox, SpecFormatInvocation invocation, CancellationToken ct)
    {
        var exitCode = await ExitCodeAsync(key, sandbox, invocation, ct);
        if (exitCode == 0)
        {
            logger.LogInformation("{Key}: {Context} formatted {Count} file(s)", key, invocation.ContextName, invocation.FileCount);
            return null;
        }
        return $"format: the {invocation.ContextName} formatter exited {exitCode} in {key}; "
               + $"its {invocation.FileCount} file(s) are committed as written";
    }

    // The runner logs a non-zero exit with its output; a formatter that cannot even start is -1.
    private async Task<int> ExitCodeAsync(
        string key, ISandbox sandbox, SpecFormatInvocation invocation, CancellationToken ct)
    {
        try
        {
            var outcome = await runner.RunAsync(
                key, Stage, sandbox, Repository.SandboxWorkPath, invocation.CommandLine, ct);
            return outcome.ExitCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{Key}: the {Context} formatter could not run", key, invocation.ContextName);
            return -1;
        }
    }
}

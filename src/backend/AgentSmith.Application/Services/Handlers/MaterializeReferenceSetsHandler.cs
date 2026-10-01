using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283df: the step before PhaseSequence that hands the master the uploaded websites the
/// approval cites — written into the CARRYING repository's sandbox, outside the commit, and
/// published on <see cref="ContextKeys.ReferenceSets"/> for the prompt and render_reference. An
/// approval that cites none makes this step silent; one whose sets cannot be read fails the run.
/// </summary>
public sealed class MaterializeReferenceSetsHandler(
    ApprovedSpecSetResolver approvals,
    ReferenceSetCarrier carrier,
    ILogger<MaterializeReferenceSetsHandler> logger)
    : ICommandHandler<MaterializeReferenceSetsContext>
{
    internal const string NoneCited = "The approval cites no uploaded website";

    public async Task<CommandResult> ExecuteAsync(
        MaterializeReferenceSetsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var record = approvals.Published(context.Pipeline);
        if (record is null || record.CitedSets.Count == 0) return CommandResult.Ok(NoneCited);
        if (CarryingSandbox(context.Pipeline, record.CarryingRepo) is not { } target)
            return CommandResult.Fail(ReferenceSetCarrier.Refusal(record.CitedSets, "the run holds no sandbox to write them into"));
        var (repo, sandbox) = target;
        try
        {
            var carry = await carrier.CarryAsync(record, repo, sandbox, cancellationToken);
            if (carry.Refusal is not null) return CommandResult.Fail(carry.Refusal);
            context.Pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, carry.Sets);
            logger.LogInformation("Carried {Count} uploaded website set(s) into {Repo}", carry.Sets.Count, repo);
            return CommandResult.Ok($"Carried {carry.Sets.Count} uploaded website set(s) into {repo}: "
                + string.Join(", ", carry.Sets.Select(s => $"{s.Name} ({s.SetId}, {s.Files} files) at {s.Path}")));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CommandResult.Fail(ReferenceSetCarrier.Refusal(record.CitedSets, ex.Message));
        }
    }

    // The sandbox the carrying repository's name resolves to — the first of its sandboxes, which is
    // the one the master's path tools alias that name to. A record that named none takes the run's
    // first repository, as every other reader of the record does.
    private static (string Repo, ISandbox Sandbox)? CarryingSandbox(PipelineContext pipeline, string carrying)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes)
            || sandboxes is null || sandboxes.Count == 0) return null;
        var repos = pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var r) && r is not null
            ? r : new Dictionary<string, string>();
        foreach (var (key, sandbox) in sandboxes)
            if (string.IsNullOrEmpty(carrying) || string.Equals(repos.GetValueOrDefault(key), carrying, StringComparison.Ordinal))
                return (repos.GetValueOrDefault(key) ?? key, sandbox);
        var first = sandboxes.First();
        return (repos.GetValueOrDefault(first.Key) ?? first.Key, first.Value);
    }
}

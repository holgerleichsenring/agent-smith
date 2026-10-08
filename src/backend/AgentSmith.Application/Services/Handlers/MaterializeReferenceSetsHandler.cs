using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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
/// 2026-10-08-e8b9k: and the images it cites, written beside them and put among the run's
/// attachments within the picture ceiling — see <see cref="UploadImageAttachments"/>.
/// </summary>
public sealed class MaterializeReferenceSetsHandler(
    ApprovedSpecSetResolver approvals,
    ReferenceSetCarrier carrier,
    ReferenceImageCarrier images,
    UploadImageAttachments attachments,
    ILogger<MaterializeReferenceSetsHandler> logger)
    : ICommandHandler<MaterializeReferenceSetsContext>
{
    internal const string NoneCited = "The approval cites no uploaded material";

    public async Task<CommandResult> ExecuteAsync(
        MaterializeReferenceSetsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var record = approvals.Published(context.Pipeline);
        if (record is null || record.CitedSets.Count + record.CitedImages.Count == 0) return CommandResult.Ok(NoneCited);
        if (CarryingSandbox(context.Pipeline, record.CarryingRepo) is not { } target)
            return CommandResult.Fail(ReferenceSetCarrier.Refusal(record.CitedSets, "the run holds no sandbox to write them into"));
        var (repo, sandbox) = target;
        try
        {
            var carry = record.CitedSets.Count == 0
                ? new ReferenceCarry([], null) : await carrier.CarryAsync(record, repo, sandbox, cancellationToken);
            if (carry.Refusal is not null) return CommandResult.Fail(carry.Refusal);
            if (carry.Sets.Count > 0)
                context.Pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, carry.Sets);
            var shown = await CarryImagesAsync(context.Pipeline, record, sandbox, cancellationToken);
            logger.LogInformation("Carried {Count} uploaded set(s) and {Images} image(s) into {Repo}",
                carry.Sets.Count, shown.Count, repo);
            return CommandResult.Ok($"Carried {carry.Sets.Count} uploaded set(s) and {shown.Count} image(s) into {repo}: "
                + string.Join(", ", carry.Sets.Select(s => $"{s.Name} ({s.SetId}, {s.Files} files) at {s.Path}")
                    .Concat(shown.Select(i => i.Path ?? $"{i.SetId} (could not be read)"))));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CommandResult.Fail(ReferenceSetCarrier.Refusal(record.CitedSets, ex.Message));
        }
    }

    private async Task<IReadOnlyList<CarriedReferenceImage>> CarryImagesAsync(
        PipelineContext pipeline, SpecApprovalRecord record, ISandbox sandbox, CancellationToken ct)
    {
        var carried = await images.CarryAsync(record, sandbox, ct);
        if (carried.Count == 0) return [];
        var (merged, cited) = attachments.Merge(
            MasterPipelineFacts.ListFrom<TicketImageAttachment>(pipeline, ContextKeys.Attachments), carried);
        pipeline.Set(ContextKeys.Attachments, merged);
        pipeline.Set(ContextKeys.ReferenceImages, cited);
        return cited;
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

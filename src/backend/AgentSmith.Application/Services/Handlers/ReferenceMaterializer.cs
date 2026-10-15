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
/// 2026-10-01-283df: carries the uploaded material an approval cites into the CARRYING repository's sandbox, outside
/// the commit, published for the prompt and render_reference; a set that cannot be read fails the run. 2026-10-08-e8b9k:
/// the images too, among the attachments (<see cref="UploadImageAttachments"/>). 2026-10-12-f5f7e: the earliest spec's repo.
/// <para>2026-10-14-dbcel: a TICKET TEXT's derivation carries it FIRST (<see cref="CarryForDerivationAsync"/>) — one copy
/// into the carrying repository, else the first record repository with a sandbox; the master and the look read every
/// sandbox by prefix. The step carries again on every run, idempotently: a resume re-runs it in a fresh sandbox.</para>
/// </summary>
public sealed class ReferenceMaterializer(
    ApprovedSpecSetResolver approvals,
    ReferenceSetCarrier carrier,
    ReferenceImageCarrier images,
    UploadImageAttachments attachments,
    ILogger<ReferenceMaterializer> logger)
{
    internal const string NoneCited = "The approval cites no uploaded material";

    public async Task<CommandResult> CarryAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var record = approvals.Published(pipeline);
        if (record is null || record.CitedSets.Count + record.CitedImages.Count == 0) return CommandResult.Ok(NoneCited);
        var carrying = EarliestSpecRepo(pipeline) ?? record.CarryingRepo;
        if (Target(pipeline, record, carrying) is not { } target)
            return CommandResult.Fail(ReferenceSetCarrier.Refusal(record.CitedSets, string.IsNullOrEmpty(carrying)
                ? "no spec names the repository to write them into"
                : $"the run holds no sandbox of '{carrying}', the repository of the series' earliest spec"));
        return await CarryIntoAsync(pipeline, record, target, cancellationToken);
    }

    /// <summary>Carries a ticket text's cited material before its derivation; any other record carries nothing
    /// here. A refusal or a throw is a warning — the derivation runs without it and the later step fails the run.</summary>
    public async Task CarryForDerivationAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (approvals.Published(pipeline) is not { TicketText: true } record || record.CitedSets.Count + record.CitedImages.Count == 0) return;
        try
        {
            var result = Target(pipeline, record, record.CarryingRepo) is { } target
                ? await CarryIntoAsync(pipeline, record, target, cancellationToken)
                : CommandResult.Fail("no repository of the approval has a sandbox in this run");
            if (!result.IsSuccess) logger.LogWarning("The derivation runs without the cited material: {Reason}", result.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The derivation runs without the cited material");
        }
    }

    private async Task<CommandResult> CarryIntoAsync(
        PipelineContext pipeline, SpecApprovalRecord record, (string Repo, ISandbox Sandbox) target, CancellationToken ct)
    {
        try
        {
            var carry = record.CitedSets.Count == 0 ? new ReferenceCarry([], null) : await carrier.CarryAsync(record, target.Repo, target.Sandbox, ct);
            if (carry.Refusal is not null) return CommandResult.Fail(carry.Refusal);
            if (carry.Sets.Count > 0) pipeline.Set<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, carry.Sets);
            var carried = await images.CarryAsync(record, target.Sandbox, ct);
            var shown = carried.Count == 0 ? [] : Attach(pipeline, carried);
            logger.LogInformation("Carried {Count} uploaded set(s) and {Images} image(s) into {Repo}", carry.Sets.Count, shown.Count, target.Repo);
            return CommandResult.Ok($"Carried {carry.Sets.Count} uploaded set(s) and {shown.Count} image(s) into {target.Repo}: "
                + string.Join(", ", carry.Sets.Select(s => $"{s.Name} ({s.SetId}, {s.Files} files) at {s.Path}")
                    .Concat(shown.Select(i => i.Path ?? $"{i.SetId} (could not be read)"))));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CommandResult.Fail(ReferenceSetCarrier.Refusal(record.CitedSets, ex.Message));
        }
    }

    private IReadOnlyList<CarriedReferenceImage> Attach(PipelineContext pipeline, IReadOnlyList<CarriedUploadImage> carried)
    {
        var (merged, cited) = attachments.Merge(MasterPipelineFacts.ListFrom<TicketImageAttachment>(pipeline, ContextKeys.Attachments), carried);
        pipeline.Set(ContextKeys.Attachments, merged);
        pipeline.Set(ContextKeys.ReferenceImages, cited);
        return cited;
    }

    private static string? EarliestSpecRepo(PipelineContext pipeline) =>
        pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) && set is not null
            ? set.Specs.Select(p => p.Draft.Repos).FirstOrDefault(r => r.Count == 1)?[0] : null;

    // The carrying repository's first sandbox; none named takes the run's only repository, and with several a ticket
    // text takes its first record repository with a sandbox — anything else has no positional pick.
    private static (string Repo, ISandbox Sandbox)? Target(PipelineContext pipeline, SpecApprovalRecord record, string carrying)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes)
            || sandboxes is null || sandboxes.Count == 0) return null;
        var repos = pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var r) && r is not null
            ? r : new Dictionary<string, string>();
        var byRepo = sandboxes.Select(s => (Repo: repos.GetValueOrDefault(s.Key) ?? s.Key, Sandbox: s.Value)).ToList();
        (string Repo, ISandbox Sandbox)? Of(string name) =>
            byRepo.Where(s => string.Equals(s.Repo, name, StringComparison.OrdinalIgnoreCase)).Select(s => ((string, ISandbox)?)s).FirstOrDefault();
        if (!string.IsNullOrEmpty(carrying)) return Of(carrying);
        if (byRepo.Select(s => s.Repo).Distinct(StringComparer.Ordinal).Count() == 1) return byRepo[0];
        return record.TicketText ? record.Repositories.Select(Of).FirstOrDefault(t => t is not null) : null;
    }
}

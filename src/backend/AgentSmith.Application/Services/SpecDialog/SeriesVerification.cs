using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// Verifies a ticket's series as its next attempt will read it against the code: only the specs that have not run,
/// in read-only scopes opened at the ticket branch's revision per repository, because an unstarted spec may cite
/// what an executed one created there. The evidence check reads the unstarted specs whose predecessors all ran —
/// what describes the branch as it is; the cut reviewer reads every unstarted spec. Without a published revision
/// it reads the turn's default-branch repositories and skips the evidence check.
/// </summary>
public sealed class SeriesVerification(
    ISourceScopeSandboxFactory scopes, ProposalEvidenceReview evidence, ISpecCutReviewer reviewer,
    DerivationLookFactory looks, ILogger<SeriesVerification> logger)
{
    public const string DefaultBranch =
        "No attempt published a revision of the ticket branch, so verification reads the default branch and skips the evidence check.";

    /// <summary>The sentence the conversation is told: the findings per spec, or that there are none.</summary>
    public async Task<string> VerifyAsync(SeriesReading series, PipelineContext pipeline, AgentConfig agent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(pipeline);
        if (series.Failure is { } failure) return failure;
        var unstarted = series.Specs.Where(s => !s.Ran).Select(s => s.Draft).ToList();
        if (unstarted.Count == 0) return "Every spec of the series already ran; nothing is left to verify.";
        if (series.ShaByRepo.Count == 0
            || !pipeline.TryGet<ResolvedProject>(ContextKeys.SpecDialogProject, out var project) || project is null)
            return $"{DefaultBranch} {Say(await ReviewAsync(unstarted, looks.ForProposalReview(pipeline), pipeline, agent, ct))}";

        var opened = Open(project, series.ShaByRepo);
        try
        {
            var findings = new List<ProposalFinding>(await EvidenceAsync(Current(unstarted), opened, ct));
            findings.AddRange(await ReviewAsync(unstarted, looks.ForReview(opened), pipeline, agent, ct));
            return $"Verified {unstarted.Count} unstarted spec(s) at the ticket branch "
                + $"({string.Join(", ", series.ShaByRepo.Select(r => $"{r.Key}@{r.Value}"))}). {Say(findings)}";
        }
        finally
        {
            foreach (var scope in opened.Values) await scope.DisposeAsync();
        }
    }

    private Dictionary<string, ISandbox> Open(ResolvedProject project, IReadOnlyDictionary<string, string> shas) =>
        project.Repos.Where(r => shas.ContainsKey(r.Name))
            .ToDictionary(r => r.Name, r => (ISandbox)scopes.Create(project, r, shas[r.Name]), StringComparer.Ordinal);

    // An unstarted spec requiring another unstarted one describes what that one leaves, which is not built yet.
    private static IReadOnlyList<SpecDraft> Current(IReadOnlyList<SpecDraft> unstarted)
    {
        var ids = unstarted.Select(d => d.SpecId).ToHashSet(StringComparer.Ordinal);
        return [.. unstarted.Where(d => !d.Requires.Any(ids.Contains))];
    }

    private async Task<IReadOnlyList<ProposalFinding>> EvidenceAsync(
        IReadOnlyList<SpecDraft> drafts, IReadOnlyDictionary<string, ISandbox> repositories, CancellationToken ct)
    {
        try
        {
            return repositories.Count == 0 ? [] : await evidence.FindingsAsync(drafts, repositories, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The evidence of the series could not be checked at the ticket branch");
            return [];
        }
    }

    private async Task<IReadOnlyList<ProposalFinding>> ReviewAsync(
        IReadOnlyList<SpecDraft> drafts, DerivationLook? look, PipelineContext pipeline, AgentConfig agent, CancellationToken ct)
    {
        var key = pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var jobId) && jobId is not null ? jobId : "spec-dialog";
        try
        {
            var review = await reviewer.ReviewAsync(
                drafts, key, ticketText: null, look, agent, PipelineCostTracker.GetOrCreate(pipeline), ct);
            return SpecDialogProposalFindings.Of(review, look?.Evidence.Looks);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The review of the series could not be taken");
            return [new ProposalFinding("series", "review not taken", $"the cut review could not be taken: {ex.Message}")];
        }
    }

    private static string Say(IReadOnlyList<ProposalFinding> findings) => findings.Count == 0
        ? "No findings."
        : $"{findings.Count} finding(s): " + string.Join(" ", findings.Select(f =>
            $"[{f.SpecId}] {f.Problem}: {f.Why}{(f.Quote is null ? string.Empty : $" (\"{f.Quote}\")")}"));
}

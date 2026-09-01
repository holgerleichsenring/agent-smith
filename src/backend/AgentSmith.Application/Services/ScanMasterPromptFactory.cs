using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0278: builds the review user prompt for a scan master.
/// <para>
/// 2026-09-01-0e80: a REPOSITORY scan's first turn no longer carries the scanner list. The
/// list is an anchor — given a list and a codebase, the cheapest correct-looking behaviour
/// is to work the list, and nothing downstream can tell that apart from a search. It now
/// arrives in a second turn, for reconciliation, once the master has committed to what it
/// found on its own. An API scan's inputs ARE the scanner reports plus the OpenAPI
/// document, so it has nothing to look at first and its prompt is unchanged.
/// </para>
/// </summary>
public sealed class ScanMasterPromptFactory : IScanMasterPromptFactory
{
    public string Build(PipelineContext pipeline, Repository repository, IReadOnlyList<string> repoNames)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(repoNames);
        var repos = repoNames.Count > 1 ? $"**Repositories:** {string.Join(", ", repoNames)}\n" : string.Empty;
        var anchored = ScanFindingsSection.HasScannerReports(pipeline);
        return $"""
            You are running a SECURITY REVIEW, not a coding task. Do NOT modify any
            source, do NOT run a build, do NOT run tests. You have read-only tools.

            ## Working source
            **Path:** {repository.LocalPath}
            **Branch:** {repository.CurrentBranch}
            {repos}
            {BuildConversationSection(pipeline)}
            {(anchored ? ScanFindingsSection.Render(pipeline) : string.Empty)}
            {BuildSpecSection(pipeline)}
            {Surface.SurfaceDifferencePromptSection.Render(pipeline)}
            {(anchored ? AnchoredClosing : UnanchoredClosing)}
            """;
    }

    /// <summary>
    /// 2026-09-01-0e80: the scanners' output, presented AFTER the master has said what it
    /// found. Null when there is nothing to reconcile separately — an api scan, whose first
    /// turn already carried the reports, or a repository scan whose scanners found nothing.
    /// </summary>
    public string? BuildReconciliation(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (ScanFindingsSection.HasScannerReports(pipeline)) return null;
        if (ScanFindingsSection.RepoFindings(pipeline) is null) return null;
        return $"""
            {ScanFindingsSection.Render(pipeline)}
            These are the automated scanners' RAW output. You are seeing them now, after
            stating what your own review found. For each one, say which of three it is:
            already covered by a finding you reported, real and now added, or dismissed —
            and for a dismissal, name the code that makes it not exploitable.

            Then output ONLY your COMPLETE JSON observation array: everything you already
            reported plus every scanner fact you now judge real. Still read-only — do NOT
            modify code and do NOT run a build or tests.
            """;
    }

    public string BuildCoverageNudge(string originalUserPrompt) =>
        "Your review touched very little of the source — that is not a complete review. "
        + "Inventory the FULL surface (every endpoint and every area of responsibility) "
        + "and review EACH area: read the code that implements it, where its auth / "
        + "sessions are handled, its input boundaries, and the configuration that wires "
        + "CORS / headers / auth. Still read-only — do NOT modify code or run a build or "
        + "tests. When done, output ONLY your COMPLETE JSON observation array (everything "
        + "you found, including any earlier findings).\n\n"
        + originalUserPrompt;

    private const string AnchoredClosing =
        """
        Work your methodology over these scanner inputs and the source — read the
        implementing code to anchor each finding. When you are done, stop calling
        tools and output ONLY your final JSON observation array (an empty array
        `[]` if nothing survives your refutation step).
        """;

    private const string UnanchoredClosing =
        """
        Work your methodology over the SOURCE. Nobody has handed you a list of
        suspects: inventory the surface yourself, read the implementing code, and
        report what you find. When you are done, stop calling tools and output ONLY
        your final JSON observation array (an empty array `[]` if nothing survives
        your refutation step).
        """;

    // p0317: a goal-bearing ticket's conversation reaches the scan master too —
    // delimited + chronological, same untrusted-content contract as the coding path.
    private static string BuildConversationSection(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var comments)
            ? TicketConversationPromptSection.Render(comments)
            : string.Empty;

    /// <summary>
    /// p0429a: the key holds a <see cref="SwaggerSpec"/>, and this asked for a string — a
    /// type-checked TryGet answers false, so the master has never once seen the API surface
    /// it was triaging findings about. A finding's endpoint is now checked against that
    /// document, so the master has to be shown it.
    /// </summary>
    private static string BuildSpecSection(PipelineContext pipeline) =>
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec)
        && !string.IsNullOrWhiteSpace(spec?.RawJson)
            ? $"## OpenAPI spec (compressed)\n\n{spec.RawJson}\n"
            : string.Empty;
}

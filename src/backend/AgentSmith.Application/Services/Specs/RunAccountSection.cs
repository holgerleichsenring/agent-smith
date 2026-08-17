using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0429a: the run's account of itself, as the text a reader gets — in the ticket comment,
/// in the pull request body, and beside a scan's findings.
/// <para>
/// The account has been taken since p0420 and read by the gate since p0421, but the only
/// human who ever saw it was one opening a done-phase YAML. So a scan whose dependency
/// audit died read exactly like a scan that audited and found nothing: same finding list,
/// same silence. Nothing new is computed here — what the gate judges is simply shown.
/// </para>
/// </summary>
public static class RunAccountSection
{
    private const string ScanHeading = "## What this scan looked for";
    private const string RunHeading = "## What this run accounted for";

    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var accounts = RunAccountLedger.Current(pipeline).All;
        if (accounts.Count == 0) return string.Empty;
        return "\n\n" + SpecAccountRenderer.ToMarkdown(accounts, Heading(accounts)).TrimEnd();
    }

    /// <summary>A scan's account is keyed "scan" by its accountant; a phase's is a repo.</summary>
    private static string Heading(IReadOnlyList<SpecAccount> accounts) =>
        accounts.All(a => string.Equals(a.RepoKey, ScanCoverageAccountant.RepoKey, StringComparison.Ordinal))
            ? ScanHeading
            : RunHeading;
}

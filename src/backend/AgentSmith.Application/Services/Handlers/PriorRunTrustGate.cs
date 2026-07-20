namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0355: trust gate for prior-run records read back into a new run's context.
/// A run that aborted at bootstrap (scoped repo unexpectedly empty/new — e.g.
/// an operator rename the trigger didn't flag) recorded a CONFUSED result; a
/// later run ingesting that record as authoritative would inherit the
/// misframing. Classifies on the run's own record — the failed frontmatter the
/// finalizer writes plus the bootstrap gate's abort signature — so no extra
/// I/O and no contact with the record writer.
/// </summary>
public static class PriorRunTrustGate
{
    private const string FailedFrontmatter = "result: failed";
    private const string BootstrapAbortSignature = "missing bootstrap in repos";

    public static bool IsBootstrapAborted(string resultMarkdown) =>
        resultMarkdown.Contains(FailedFrontmatter, StringComparison.Ordinal)
        && resultMarkdown.Contains(BootstrapAbortSignature, StringComparison.OrdinalIgnoreCase);
}

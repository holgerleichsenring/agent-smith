using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: resolves a live-target finding's citation against the OpenAPI document the scan
/// was actually run against.
/// <para>
/// A DAST finding names an endpoint, not a file, so p0429's "read the file it cites" check
/// had nothing to resolve and would have passed every one of them through unchecked — the
/// blind spot the whole mechanism exists to close, reopened for the pipeline that needs it
/// most. An endpoint the specification does not declare is the invented location.
/// </para>
/// <para>
/// The evidence handed on is the declaration plus the REAL request and response, because a
/// plausible copy of an exchange is not evidence. When the scanners kept no exchange the
/// declaration stands alone and the refuter is told so, so it cannot refute a claim by
/// quoting something nobody sent.
/// </para>
/// </summary>
public sealed class EndpointCitationResolver : ICandidateResolver
{
    public bool CanAnswer(SkillObservation finding, ScanEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(evidence);
        return !evidence.Endpoints.IsEmpty && Citation(finding) is not null;
    }

    public Task<CandidateResolution> ResolveAsync(
        SkillObservation finding, ScanEvidence evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(evidence);
        var citation = Citation(finding)!;
        var declarations = evidence.Endpoints.Declarations(citation);
        return Task.FromResult(declarations.Count == 0
            ? CandidateResolution.Invented
            : CandidateResolution.Refutable(new CandidateFinding(
                finding, finding.DisplayLocation,
                Describe(declarations, evidence.Exchanges.For(citation)),
                EvidenceSurface.LiveTarget)));
    }

    /// <summary>The endpoint or schema the finding points at, or null when it points at
    /// neither and this resolver has nothing to say about it.</summary>
    private static string? Citation(SkillObservation finding) =>
        !string.IsNullOrWhiteSpace(finding.ApiPath) ? finding.ApiPath
        : !string.IsNullOrWhiteSpace(finding.SchemaName) ? finding.SchemaName
        : null;

    private static string Describe(IReadOnlyList<string> declarations, HttpExchange? exchange)
    {
        var text = new StringBuilder("the specification declares:\n");
        foreach (var declaration in declarations) text.Append("  ").AppendLine(declaration);
        if (exchange is null)
            return text.AppendLine(
                "no request/response was recorded for this endpoint — you are reading the "
                + "specification only, and cannot refute a claim about what the live system did")
                .ToString();
        text.AppendLine().Append("request: ").Append(exchange.Method).Append(' ').AppendLine(exchange.Url);
        Section(text, "attack", exchange.Attack);
        Section(text, "matched evidence", exchange.Evidence);
        Section(text, "request sent", exchange.Request);
        Section(text, "response received", exchange.Response);
        return text.ToString();
    }

    private static void Section(StringBuilder text, string label, string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        text.AppendLine().Append(label).AppendLine(":").AppendLine(body.TrimEnd());
    }
}

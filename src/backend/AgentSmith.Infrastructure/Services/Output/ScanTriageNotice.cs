using System.Text.Json.Nodes;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// 2026-08-30-03e4: the ONE wording that tells the reader of a DELIVERED artefact that the
/// findings in front of them were never triaged, in each form delivery needs it.
/// <para>
/// The run record already knows — the coverage account refuses the triage criterion and the
/// delivery gate records the run as not delivered. But an artefact travels on its own, and
/// an untriaged scan delivers MORE findings than a triaged one, so it reads as the thorough
/// result: three identical runs delivered 25, 26 and 37 findings and the untriaged one
/// looked like the best. One wording here, so the mark cannot say different things in
/// different formats, and every form is EMPTY on a healthy run — the artefact a scan that
/// triaged normally produces is byte-identical to the one it produced before this existed.
/// </para>
/// </summary>
public static class ScanTriageNotice
{
    /// <summary>The headline every format shows verbatim; each format frames it in its own
    /// syntax (blockquote, banner line, SARIF notification) and adds nothing else.</summary>
    public const string Headline =
        "TRIAGE DEGRADED — these findings were NOT triaged by the scan master";

    /// <summary>The mark for this run, or null when the run triaged normally.</summary>
    public static string? For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.ScanTriageDegraded, out var reason)
            || string.IsNullOrWhiteSpace(reason))
            return null;
        return $"{Headline}: {reason}. What follows is raw scanner output — expect "
            + "duplicates, false positives and an inflated count.";
    }

    /// <summary>A Markdown blockquote to prefix a report with, or the empty string.</summary>
    public static string Markdown(PipelineContext pipeline) =>
        For(pipeline) is { } notice
            ? $"> **{notice}**{Environment.NewLine}{Environment.NewLine}"
            : "";

    /// <summary>A plain-text block for a stdout banner, or the empty string.</summary>
    public static string Banner(PipelineContext pipeline) =>
        For(pipeline) is { } notice
            ? notice + Environment.NewLine + Environment.NewLine
            : "";

    /// <summary>The SARIF form: a fact about the RUN, not about any one finding, so it
    /// belongs in the run's invocation where a consumer reads run health.</summary>
    public static JsonArray SarifInvocations(string notice) =>
    [
        new JsonObject
        {
            ["executionSuccessful"] = false,
            ["toolExecutionNotifications"] = new JsonArray
            {
                new JsonObject
                {
                    ["level"] = "error",
                    ["message"] = new JsonObject { ["text"] = notice }
                }
            }
        }
    ];
}

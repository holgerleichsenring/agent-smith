using System.Net;
using System.Text.RegularExpressions;
using AgentSmith.Application.Services.SpecDialog;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: inverts the p0315c PhaseTicketRenderer — parses the single fenced
/// ```yaml block out of a phase ticket body into a schema-validated
/// <see cref="Contracts.Models.PhaseDraft"/>. Two wire shapes exist: the
/// markdown body the renderer produced (GitHub/GitLab/Jira round-trip it
/// verbatim), and the Azure DevOps variant where System.Description stores
/// the markdown→HTML conversion, so the fence comes back as an HTML-encoded
/// <c>&lt;pre&gt;&lt;code class="language-yaml"&gt;</c> block. Validation is
/// the production SpecDraftValidator — the same schema gate the draft passed
/// before filing.
/// </summary>
public sealed partial class PhaseSpecFromTicket(
    ISpecDraftValidator validator,
    PhaseDraftReader draftReader)
    : IPhaseSpecFromTicket
{
    [GeneratedRegex("<pre><code class=\"language-yaml\">(.*?)</code></pre>", RegexOptions.Singleline)]
    private static partial Regex HtmlYamlBlockRegex();

    public PhaseSpecExtraction Extract(string ticketBody)
    {
        if (string.IsNullOrWhiteSpace(ticketBody))
            return new PhaseSpecInvalid("the phase ticket body is empty — no spec to execute");

        var outcome = ticketBody.Contains("```yaml", StringComparison.Ordinal)
            ? validator.Validate(ticketBody)
            : ValidateHtmlVariant(ticketBody);

        return outcome switch
        {
            SpecDraftValid valid => new PhaseSpecExtracted(draftReader.Read(valid.Yaml)),
            SpecDraftInvalid invalid => new PhaseSpecInvalid(invalid.Error),
            _ => new PhaseSpecInvalid(
                "the phase ticket body contains no fenced ```yaml block — "
                + "a phase ticket must carry its spec verbatim (p0315c contract)"),
        };
    }

    // Azure DevOps renders System.Description as HTML: the create path converts
    // the markdown body with Markdig, so the fence reads back as one
    // <pre><code class="language-yaml"> block with HTML-encoded content.
    private SpecDraftOutcome ValidateHtmlVariant(string body)
    {
        var blocks = HtmlYamlBlockRegex().Matches(body);
        if (blocks.Count == 0) return new SpecDraftAbsent();
        if (blocks.Count > 1)
            return new SpecDraftInvalid(
                $"the ticket body contains {blocks.Count} HTML yaml blocks — a phase ticket carries exactly one spec");
        return validator.ValidateYaml(WebUtility.HtmlDecode(blocks[0].Groups[1].Value));
    }
}

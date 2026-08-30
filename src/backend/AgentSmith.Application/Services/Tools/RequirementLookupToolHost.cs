using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-03e1: the published standard, answering a LOOKUP.
/// <para>
/// Its predecessor handed a station its entries and expected an answer per entry, which
/// turned the standard into an agenda the scan walked: six scans of one repository under
/// that shape found none of the five issues a reviewer found, while the base prompt grew by
/// three quarters. The catalogue is a reference, so it is consulted when the scan already
/// has something to report and wants the clause that names it.
/// </para>
/// <para>
/// The answer is the WHOLE floor set for the station. The twelve-entry bound was a budget
/// device for the hand-out and died with it; kept here it would only refuse a real finding
/// against the thirteenth entry of a station that classifies seventy-nine.
/// </para>
/// </summary>
public sealed class RequirementLookupToolHost(IVerificationLens lens, PipelineContext run) : IToolHost
{
    public const string ToolName = "look_up_requirements";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(LookUpRequirements, name: ToolName)];
    }

    [Description(
        "Looks up the entries of the published verification standard that apply at ONE "
        + "station of a request. Call it when you have already found something and want the "
        + "clause it breaks, so you can cite it with record_cited_finding. It is a "
        + "reference, not an agenda: nothing here is a list to work through, and a finding "
        + "no entry covers belongs in your observation array like any other.")]
    public string LookUpRequirements(
        [Description("One of: admission, evidence, resolution, authority, scope, effect.")]
        string station)
    {
        if (!Enum.TryParse<VerificationStation>(station, ignoreCase: true, out var parsed))
            return $"Error: unknown station '{station}'. Use one of: "
                + string.Join(", ", Enum.GetNames<VerificationStation>()).ToLowerInvariant() + ".";
        var selection = lens.For(run, parsed);
        return selection.Requirements.Count == 0
            ? $"The standard has no entry classified for the {parsed} station in this release."
            : Render(parsed, selection);
    }

    private static string Render(VerificationStation station, VerificationSelection selection)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{selection.Requirements.Count} entries of {selection.CatalogueVersion} "
            + $"apply at the {station} station. Cite the one your finding breaks with "
            + $"{CitedFindingToolHost.ToolName}. If none of them names what you found, "
            + "report it in your observation array — it reaches the reader unchanged.");
        foreach (var entry in selection.Requirements)
            sb.AppendLine($"- {entry.Id} [L{entry.Level}] {entry.Text}");
        sb.AppendLine(selection.Attribution);
        return sb.ToString();
    }
}

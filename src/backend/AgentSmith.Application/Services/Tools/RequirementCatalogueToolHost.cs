using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: hands a worker the entries of the published standard that apply to one
/// station of a request.
/// <para>
/// HANDED, not recalled. A model asked "which requirements apply here" answers from memory
/// and the denominator becomes whatever it happened to remember; the lens table and the
/// station map both sit outside the model, so what a station is asked is the same question
/// on every run of every repository. The selection is bounded by the lens, and the licence
/// line the ingested text carries rides along with it.
/// </para>
/// </summary>
public sealed class RequirementCatalogueToolHost(IVerificationLens lens, PipelineContext run) : IToolHost
{
    public const string ToolName = "list_station_requirements";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ListStationRequirements, name: ToolName)];
    }

    [Description(
        "Lists the entries of the published verification standard that this run asks at ONE "
        + "station of a request. Call it once per station, then answer every entry it lists "
        + "with record_requirement_answer. The list is the run's denominator: it is not "
        + "yours to shorten, and an entry it does not list is not asked.")]
    public string ListStationRequirements(
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
            + $"apply to the {station} station. Answer each one with "
            + $"{RequirementAnswerToolHost.ToolName}, for reads and for writes separately. "
            + $"This run answers the entry groups you stated first, at most "
            + $"{RequirementAnswerLog.MaxEntryGroups} of them — one worker per group.");
        foreach (var entry in selection.Requirements)
            sb.AppendLine($"- {entry.Id} [L{entry.Level}] {entry.Text}");
        sb.AppendLine(selection.Attribution);
        return sb.ToString();
    }
}

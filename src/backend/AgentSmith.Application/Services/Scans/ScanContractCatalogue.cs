using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the scan's contract, derived from the steps its preset really declares.
/// <para>
/// A coding run states its criteria in a phase spec. A scan had none at all, so a
/// dependency audit that failed to restore left the run reporting "clean" — a MISS was
/// indistinguishable from a NON-GOAL. Reading the criteria off the preset costs no
/// tokens, cannot drift from what the run will actually do, and shrinks automatically
/// when a step is removed.
/// </para>
/// </summary>
public sealed class ScanContractCatalogue : IScanContractCatalogue
{
    private static readonly IReadOnlyDictionary<string, string> ByStep =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CommandNames.StaticPatternScan] =
                "Secrets and unsafe patterns in the working source are identified",
            [CommandNames.GitHistoryScan] =
                "Secrets committed to the repository's history are identified",
            [CommandNames.DependencyAudit] =
                "Known-vulnerable dependencies are identified",
            [CommandNames.SecurityTrend] =
                "The finding set is compared against the previous snapshot",
            [CommandNames.LoadSwagger] =
                "The API surface under test is enumerated from its specification",
            [CommandNames.SpawnNuclei] =
                "Known vulnerability templates are executed against the live API",
            [CommandNames.SpawnSpectral] =
                "The API specification is linted for contract defects",
            [CommandNames.SpawnZap] =
                "The live API is exercised dynamically",
            [CommandNames.AgenticMaster] =
                "Every candidate finding is triaged by the scan master",
            [CommandNames.SubstantiateFindings] =
                "Every delivered finding is substantiated against the scanned source",
            [CommandNames.DeliverFindings] =
                "The surviving findings are delivered in the requested formats",
        };

    public ScanContract For(string? pipelineName)
    {
        var steps = string.IsNullOrWhiteSpace(pipelineName)
            ? null
            : PipelinePresets.TryResolve(pipelineName);
        if (steps is null) return ScanContract.Empty;

        var criteria = steps
            .Where(ByStep.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .Select(step => new ScanCriterion(ByStep[step], step))
            .ToList();
        return criteria.Count == 0 ? ScanContract.Empty : new ScanContract(criteria);
    }
}

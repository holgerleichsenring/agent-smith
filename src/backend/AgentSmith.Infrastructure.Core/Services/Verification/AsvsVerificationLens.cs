using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Core.Services.Verification;

/// <summary>
/// 2026-08-30-0ea8: selects the entries of the ingested standard that apply to one
/// station of a request, at or under the level floor, up to the bound.
/// <para>
/// THE BOUND IS THE POINT. The release carries 345 requirements; a station times an entry
/// group times the rest is thousands of questions, and a question nobody can answer is
/// worse than one nobody asked. Twelve per station keeps a whole group inside one
/// sub-agent's reach.
/// </para>
/// <para>
/// THE LENS MUST CLASSIFY EVERYTHING. A hand-kept mapping table rots silently; keyed
/// against a checked-in catalogue, rot can only arrive on a deliberate version bump — so
/// an id the table does not name refuses the lens outright, and the build checks the row
/// count against the declared one before any of this runs.
/// </para>
/// </summary>
internal sealed class AsvsVerificationLens : IVerificationLens
{
    /// <summary>How many entries a single station may be asked.</summary>
    public const int MaxEntriesPerStation = 12;

    internal const string ResourceName = "AgentSmith.VerificationLens.tsv";

    /// <summary>The levels a station is asked unless a caller states otherwise: the
    /// standard's own floor for an application that is not high-assurance.</summary>
    private static readonly string[] LevelFloor = ["1", "2"];

    private const int NamedInFailure = 10;

    private readonly IVerificationCatalogue _catalogue;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<VerificationStation>> _stations;

    public AsvsVerificationLens(IVerificationCatalogue catalogue, VerificationLensTableParser parser)
    {
        _catalogue = catalogue;
        using var table = new StreamReader(Open());
        _stations = parser.Parse(table);
        RefuseUnclassifiedRequirements();
    }

    public VerificationSelection For(PipelineContext run, VerificationStation station)
    {
        run.Set(ContextKeys.VerificationCatalogueVersion, _catalogue.Version);
        var entries = _catalogue.Requirements
            .Where(requirement => LevelFloor.Contains(requirement.Level))
            .Where(requirement => Applies(requirement.Id, station))
            .OrderBy(requirement => requirement.Level, StringComparer.Ordinal)
            .Take(MaxEntriesPerStation)
            .ToArray();
        return new VerificationSelection(_catalogue.Version, AsvsRelease.Attribution, entries);
    }

    private bool Applies(string id, VerificationStation station) =>
        _stations.TryGetValue(id, out var stations) && stations.Contains(station);

    private void RefuseUnclassifiedRequirements()
    {
        var unclassified = _catalogue.Requirements
            .Select(requirement => requirement.Id)
            .Where(id => !_stations.ContainsKey(id))
            .ToList();
        if (unclassified.Count == 0) return;
        throw new InvalidOperationException(
            $"The lens table classifies no station for {unclassified.Count} requirement(s) of "
            + $"catalogue {_catalogue.Version}: {string.Join(", ", unclassified.Take(NamedInFailure))}"
            + $"{(unclassified.Count > NamedInFailure ? ", ..." : string.Empty)}. "
            + "Classify every id the checked-in export carries.");
    }

    private static Stream Open() =>
        typeof(AsvsVerificationLens).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded lens table resource '{ResourceName}' not found in "
                + "AgentSmith.Infrastructure.Core — the checked-in table is not embedded.");
}

using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// What the commit gate found — the units over their Limits and not tolerated by a baseline, or
/// why it could not judge at all (<see cref="Unavailable"/>: a grammar that would not load).
/// 2026-10-12-fbbda: only an excess fails it; a divergence is inside the band and is named in the
/// pull request instead. 2026-10-13-2d0c: the split is computed once per set of findings, and the
/// verdict says how much was judged and why the rest was not — a pass over nothing is no pass.
/// </summary>
public sealed record StructureGateVerdict
{
    private readonly IReadOnlyList<StructureFinding> _findings = [];
    private readonly IReadOnlyList<StructureFinding> _excess = [];
    private readonly IReadOnlyList<StructureFinding> _divergences = [];

    public StructureGateVerdict(IReadOnlyList<StructureFinding> findings, string? unavailable = null)
    {
        Findings = findings;
        Unavailable = unavailable;
    }

    public static StructureGateVerdict Pass { get; } = new([]);

    /// <summary>Nothing was judged, and why.</summary>
    public static StructureGateVerdict NotJudged(string reason) => new([]) { Skipped = [reason] };

    public IReadOnlyList<StructureFinding> Findings
    {
        get => _findings;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _findings = value;
            _excess = [.. value.Where(f => f.IsExcess)];
            _divergences = [.. value.Where(f => !f.IsExcess)];
        }
    }

    public string? Unavailable { get; init; }

    /// <summary>Files measured against a context's terms.</summary>
    public int JudgedFiles { get; init; }

    /// <summary>Contexts whose Limits and baseline were read at the base.</summary>
    public int JudgedContexts { get; init; }

    /// <summary>Why a repository or a context was not judged.</summary>
    public IReadOnlyList<string> Skipped { get; init; } = [];

    public IReadOnlyList<StructureFinding> Excess => _excess;

    public IReadOnlyList<StructureFinding> Divergences => _divergences;

    public bool Passed => Unavailable is null && _excess.Count == 0;

    /// <summary>Both verdicts' findings, counts and reasons, as one repository after another.</summary>
    public StructureGateVerdict Plus(StructureGateVerdict other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new StructureGateVerdict([.. Findings, .. other.Findings], Unavailable ?? other.Unavailable)
        {
            JudgedFiles = JudgedFiles + other.JudgedFiles,
            JudgedContexts = JudgedContexts + other.JudgedContexts,
            Skipped = [.. Skipped, .. other.Skipped],
        };
    }
}

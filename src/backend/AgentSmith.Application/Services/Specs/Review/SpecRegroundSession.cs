using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: one entered spec's re-ground — its repositories, the preceding build notes, the
/// approval instant, what was found, and the read budget: <paramref name="maxPairs"/> distinct (repository, path)
/// pairs, each candidate file read counted as one more (spec_review.reground_read_budget, 2026-11-17-f9b0b).
/// </summary>
internal sealed class SpecRegroundSession(
    IReadOnlyDictionary<string, ISandbox> repositories, IReadOnlyList<PrecedingNote> notes,
    string specId, DateTimeOffset? approvedAt, int maxPairs)
{
    private readonly HashSet<(string Repository, string Path)> _pairs = [];
    private int _spent;

    public IReadOnlyDictionary<string, ISandbox> Repositories => repositories;
    public IReadOnlyList<PrecedingNote> Notes => notes;
    public string SpecId => specId;
    public DateTimeOffset? ApprovedAt => approvedAt;
    public List<SpecCitationMove> Moves { get; } = [];
    public List<string> Stale { get; } = [];
    public int Unchecked { get; private set; }

    /// <summary>True when the cited pair is already counted or still fits; past the bound it is counted unchecked.</summary>
    public bool Admit(string repository, string path)
    {
        if (_pairs.Contains((repository, path))) return true;
        if (_spent >= maxPairs) return Refuse();
        _pairs.Add((repository, path));
        _spent++;
        return true;
    }

    /// <summary>True when <paramref name="reads"/> more candidate reads fit; past the bound the citation is unchecked.</summary>
    public bool Spend(int reads)
    {
        if (_spent + reads > maxPairs) return Refuse();
        _spent += reads;
        return true;
    }

    private bool Refuse()
    {
        Unchecked++;
        return false;
    }
}

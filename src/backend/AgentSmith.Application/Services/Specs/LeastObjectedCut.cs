using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0447: the best cut the deriver saw across its attempts — fewest standing findings.
/// <para>
/// When the attempts run out this is what the run gets. A cut with an objection against
/// one phase is strictly better than what it replaced: one phase carrying the whole
/// ticket, with no boundary, no per-phase verdict and no repair pass. A cut that never
/// PARSED never reaches here, so the fail-safe below it is untouched.
/// </para>
/// </summary>
internal sealed class LeastObjectedCut
{
    private int _fewest = int.MaxValue;

    internal SpecDerivation? Best { get; private set; }

    internal void Offer(SpecDerivation cut, SpecCutReview review)
    {
        if (review.Findings.Count >= _fewest) return;
        Best = cut;
        _fewest = review.Findings.Count;
    }
}

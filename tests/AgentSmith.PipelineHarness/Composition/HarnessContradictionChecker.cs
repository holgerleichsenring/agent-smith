using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-10-12-3a36b: the contradiction check is a framework call like the premise check and gets
/// the same treatment — letting it draw from the deriver's script would shift the whole sequence
/// by one. It lets every contradiction stand unless a case says it refutes; the real checker is
/// exercised by ContradictionCheckerTests over a real look.
/// </summary>
internal sealed class HarnessContradictionChecker : IContradictionChecker
{
    private ContradictionCheck? _refutation;

    /// <summary>What the check was asked about, in call order: the claim and the cited lines.</summary>
    internal List<(string Reason, IReadOnlyList<string> Cited)> Asked { get; } = [];

    /// <summary>Every later check refutes with this.</summary>
    internal HarnessContradictionChecker Refutes(string why, string evidence)
    {
        _refutation = new ContradictionCheck(true, why, evidence);
        return this;
    }

    public Task<ContradictionCheck> CheckAsync(
        SpecHandback handback, IReadOnlyList<EvidenceLook> cited, DerivationLook look, string key,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handback);
        ArgumentNullException.ThrowIfNull(cited);
        lock (Asked) Asked.Add((handback.Reason, [.. cited.Select(l => l.Line)]));
        return Task.FromResult(_refutation ?? ContradictionCheck.Stands("the harness lets it stand"));
    }
}

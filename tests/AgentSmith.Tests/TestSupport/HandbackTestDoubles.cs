using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-12-3a36b: the contradiction gate, the foreign-commit probe and the heads
/// reader as the product composes them, for tests whose subject is something else.</summary>
internal static class HandbackTestDoubles
{
    public static ForeignCommitProbe Probe() =>
        new(new SandboxTargets(),
            new SandboxBaseLadder(
                new SandboxBaseBranch(NullLogger<SandboxBaseBranch>.Instance),
                NullLogger<SandboxBaseLadder>.Instance),
            NullLogger<ForeignCommitProbe>.Instance);

    public static HandbackHeadsReader Heads() =>
        new(new SandboxTargets(), NullLogger<HandbackHeadsReader>.Instance);

    /// <summary>A gate whose checker lets every cited contradiction stand.</summary>
    public static ContradictionHandbackGate Gate(IContradictionChecker? checker = null) =>
        new(checker ?? new StandingChecker(), Specs.DerivationTestLooks.Factory(),
            NullLogger<ContradictionHandbackGate>.Instance);

    /// <summary>Lets every contradiction stand; records what it was asked.</summary>
    public sealed class StandingChecker(ContradictionCheck? answer = null) : IContradictionChecker
    {
        public List<(SpecHandback Handback, IReadOnlyList<EvidenceLook> Cited)> Asked { get; } = [];

        public Task<ContradictionCheck> CheckAsync(
            SpecHandback handback, IReadOnlyList<EvidenceLook> cited, DerivationLook look, string key,
            AgentConfig agent, AgentSmith.Application.Services.PipelineCostTracker costTracker,
            CancellationToken cancellationToken)
        {
            Asked.Add((handback, cited));
            return Task.FromResult(answer ?? ContradictionCheck.Stands("the claim holds"));
        }
    }
}

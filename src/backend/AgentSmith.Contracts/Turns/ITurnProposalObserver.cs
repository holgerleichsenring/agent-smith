using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Turns;

/// <summary>
/// 2026-11-09-9a98: told what a design turn's proposal looks like while its review is running, so
/// what the master wrote is readable before the review ends and survives if it never does.
/// <para>An implementation must not throw: the review awaits it, and a shown state that failed
/// would cost the review it describes.</para>
/// </summary>
public interface ITurnProposalObserver
{
    Task PublishAsync(OutcomeProposal proposal, CancellationToken cancellationToken);
}

/// <summary>
/// The observer carried ambiently for the turn that set it, as <see cref="ITurnActivityObserverAccessor"/>
/// is. The default is no observer, so a turn nothing is listening to publishes nothing.
/// </summary>
public interface ITurnProposalObserverAccessor
{
    ITurnProposalObserver? Current { get; }

    /// <summary>Sets <paramref name="observer"/> until the returned handle is disposed.</summary>
    IDisposable Observe(ITurnProposalObserver observer);
}

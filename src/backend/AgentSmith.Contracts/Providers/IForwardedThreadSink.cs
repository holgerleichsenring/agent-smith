using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-12-92e9a: hears each thread a <see cref="ForwardedThreadCapture"/> records, as the turn grows, with whether
/// the ticket steward leads the loop. Offering never blocks the call that recorded it and never throws.
/// </summary>
public interface IForwardedThreadSink
{
    void Offer(IReadOnlyList<ChatMessage> messages, bool stewarded);
}

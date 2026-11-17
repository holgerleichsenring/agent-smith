namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-11-17-e061a: the class of a failed close — <see cref="TicketCloseReasons.Refused"/> when the host answered and
/// declined, else a read class. Never the host's text. The server implements it, because the hosts' exception types
/// live there; the 'close' run reads it.
/// </summary>
public interface ITicketCloseFailureClassifier
{
    string Of(Exception exception);
}

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// 2026-08-27-729e: the database's state as a fact rather than as a verdict. The
/// connectivity probe answers "can this be used"; a report of what an installation is
/// running needs the count behind that answer, and both must come from one read.
/// Never throws — a failure is a state, not an exception.
/// </summary>
public interface IPersistenceStateReader
{
    Task<PersistenceState> ReadPersistenceStateAsync(CancellationToken cancellationToken);
}

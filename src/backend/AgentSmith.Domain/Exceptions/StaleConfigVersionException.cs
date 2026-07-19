namespace AgentSmith.Domain.Exceptions;

/// <summary>
/// p0349: thrown when a config entity write carries an expected version that no
/// longer matches the stored version — a concurrent edit moved it on. The API
/// surfaces this as HTTP 409 Conflict, never a silent last-write-wins.
/// </summary>
public sealed class StaleConfigVersionException : AgentSmithException
{
    public StaleConfigVersionException(string entityType, string entityId, int expected, int actual)
        : base($"Stale write to config {entityType}/{entityId}: expected version {expected}, " +
               $"but the stored version is {actual}. Reload and retry.")
    {
    }
}

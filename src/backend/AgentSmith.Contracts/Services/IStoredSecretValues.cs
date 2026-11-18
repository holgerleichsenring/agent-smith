namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-17-f3a8d: the secret values stored in the admin area, opened for one configuration load. Keyed by the
/// secret's name in lower case. A value that cannot be opened is said as a finding. 2026-11-18-78efb: and is listed as
/// unreadable, so a secret whose value exists but did not open is told apart from one that has none.
/// </summary>
public interface IStoredSecretValues
{
    StoredSecretLoad Load();
}

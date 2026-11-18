namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-17-f3a8d: the secret values stored in the admin area, opened for one configuration load. Keyed by the
/// secret's name in lower case. A value that cannot be opened is left out and said as a finding.
/// </summary>
public interface IStoredSecretValues
{
    IReadOnlyDictionary<string, string> Load();
}

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-01-7f7aa: the credential values the configuration holds NOW, looked up at the
/// moment of use. A resolved record carries a secret's NAME and asks here for the value, so
/// nothing that caches a record ever holds a token; the masker asks here for every value, so
/// a secret added after startup is masked like one known at boot.
/// </summary>
public interface ISecretValues
{
    /// <summary>The value of the named secret, or null when it is unknown or empty.</summary>
    string? Resolve(string name);

    /// <summary>
    /// 2026-11-18-78efb: why the named secret's stored value cannot be opened, naming no value; null when it opened,
    /// when the environment holds a value, or when nothing is stored.
    /// </summary>
    string? Unreadable(string name);

    /// <summary>
    /// Every credential value currently configured — secrets and registry tokens. The same
    /// list instance is returned until the values change, so a caller can cache work on it.
    /// </summary>
    IReadOnlyList<string> All();
}

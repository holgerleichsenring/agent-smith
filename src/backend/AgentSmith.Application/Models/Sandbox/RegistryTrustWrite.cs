namespace AgentSmith.Application.Models.Sandbox;

/// <summary>
/// 2026-11-16-f3fbd: what became of a run's request to trust a registry prefix. <see cref="Trusted"/> is
/// true once the stored list holds the prefix; <see cref="Message"/> says what was done, or why not.
/// </summary>
public sealed record RegistryTrustWrite(bool Trusted, string Message)
{
    public static RegistryTrustWrite Written(string message) => new(true, message);

    public static RegistryTrustWrite Refused(string message) => new(false, message);
}

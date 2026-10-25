using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7h: what an in-process step inherits from the host. It removes every declared
/// secret name (the agent bindings, each agent's api_key_secret, <see cref="ServerSecretEnvNames"/>)
/// and every variable whose value is a known secret value. Values shorter than SecretMasker's
/// floor are never treated as secrets, so CI=1 or DOTNET_NOLOGO=true stay. This hides the values
/// from what env, a build or a test prints; an in-process step still runs as the server's user.
/// </summary>
public sealed class InProcessStepEnvironment
{
    /// <summary>SecretMasker's floor: shorter values are not worth treating as secrets.</summary>
    public const int MinimumSecretLength = 6;

    private readonly HashSet<string> _names;
    private readonly HashSet<string> _values;

    public InProcessStepEnvironment(IEnumerable<string> names, IEnumerable<string> secretValues)
    {
        _names = new HashSet<string>(names, StringComparer.Ordinal);
        _values = new HashSet<string>(secretValues.Where(v => v.Length >= MinimumSecretLength), StringComparer.Ordinal);
    }

    /// <summary>The whole host environment, unchanged: for tests that are not about secrets.</summary>
    public static InProcessStepEnvironment Inherit { get; } = new([], []);

    /// <summary>The declared names for a configuration (or for none) and the given known values.</summary>
    public static InProcessStepEnvironment For(AgentSmithConfig? config, IEnumerable<string> secretValues) =>
        new(DeclaredNames(config), secretValues);

    /// <summary>Removes every declared name and every variable carrying a known secret value.</summary>
    public void Scrub(IDictionary<string, string?> environment)
    {
        foreach (var (name, value) in environment.ToList())
            if (_names.Contains(name) || (value is not null && _values.Contains(value)))
                environment.Remove(name);
    }

    // api_key_secret is read directly, not through AgentSecretNames.For, whose Kubernetes-key
    // check would turn an unusual name into a failure of every step.
    private static IEnumerable<string> DeclaredNames(AgentSmithConfig? config) =>
        AgentSecretBinding.All.Select(b => b.EnvVar)
            .Concat(ServerSecretEnvNames.All)
            .Concat((config?.Agents.Values ?? Enumerable.Empty<AgentConfig>())
                .Select(a => a.ApiKeySecret).OfType<string>().Where(n => n.Length > 0));
}

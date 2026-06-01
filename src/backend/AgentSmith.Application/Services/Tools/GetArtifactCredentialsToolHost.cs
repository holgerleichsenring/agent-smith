using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0191: exposes <c>get_artifact_credentials</c> so the agent can fetch
/// private-package-feed credentials on demand when a toolchain command
/// fails with NU1301 / EAUTH / 401. Returns JSON list of
/// {host, username, token}. Pull-on-demand keeps tokens out of the
/// initial LLM context. <see cref="ISensitiveToolHost"/> marker triggers
/// the history-scrub layer to redact prior-turn results.
/// </summary>
public sealed class GetArtifactCredentialsToolHost : IToolHost, ISensitiveToolHost
{
    private readonly IReadOnlyList<RegistryConfig> _registries;

    public GetArtifactCredentialsToolHost(IReadOnlyList<RegistryConfig> registries)
    {
        _registries = registries;
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(GetArtifactCredentials, name: SensitiveToolNames.GetArtifactCredentials)];
    }

    [Description(
        "Retrieve credentials for a private package feed. Call on package-manager " +
        "auth errors (NU1301 / EAUTH / 401). Returns matching {host, username, token} " +
        "entries. host_filter is required when more than one registry is configured.")]
    public Task<string> GetArtifactCredentials(
        [Description("Hostname or full URL of the failing feed (e.g. 'pkgs.dev.azure.com' " +
                     "or 'https://pkgs.dev.azure.com/Org/.../v3/index.json'). Optional " +
                     "only when exactly one registry is configured.")]
        string? host_filter = null,
        CancellationToken ct = default)
    {
        _ = ct;
        if (_registries.Count == 0)
            return Task.FromResult(SerializeMatches(Array.Empty<RegistryConfig>()));

        if (string.IsNullOrWhiteSpace(host_filter))
            return Task.FromResult(NoFilterPolicy());

        var normalised = ReduceToHostname(host_filter);
        var matches = MatchByDotBoundary(normalised);
        return Task.FromResult(SerializeMatches(matches));
    }

    private string NoFilterPolicy()
    {
        if (_registries.Count == 1)
            return SerializeMatches(_registries);
        return JsonSerializer.Serialize(new
        {
            error = "host_filter required when multiple registries are configured.",
            registries_configured = _registries.Count,
            hint = "Pass the host from the failing URL, e.g. 'pkgs.dev.azure.com'.",
        });
    }

    private List<RegistryConfig> MatchByDotBoundary(string normalised)
    {
        var matches = new List<RegistryConfig>();
        foreach (var reg in _registries)
        {
            if (IsDotBoundaryMatch(reg.Host, normalised))
                matches.Add(reg);
        }
        return matches;
    }

    private static bool IsDotBoundaryMatch(string host, string filter)
    {
        if (string.Equals(host, filter, StringComparison.OrdinalIgnoreCase)) return true;
        return host.EndsWith("." + filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReduceToHostname(string input)
    {
        var trimmed = input.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            return uri.Host;
        return trimmed;
    }

    private static string SerializeMatches(IReadOnlyList<RegistryConfig> matches) =>
        JsonSerializer.Serialize(matches.Select(r => new
        {
            host = r.Host,
            username = r.Username,
            token = r.Token,
        }));
}

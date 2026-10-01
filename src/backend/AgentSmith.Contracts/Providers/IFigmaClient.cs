using AgentSmith.Contracts.Models.Design;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-01-7f7ab: reads Figma's REST API with the token held by the secret
/// <c>secretName</c> names. The token is looked up per request and travels only in the
/// request header to the API host — never in a result, a failure detail or a log line.
/// </summary>
public interface IFigmaClient
{
    /// <summary>GET /v1/files/:key/nodes for one node, <paramref name="depth"/> levels deep — of
    /// <paramref name="version"/> when one is named (2026-10-01-7f7ae), the current one otherwise.</summary>
    Task<FigmaReadResult> GetNodesAsync(
        string secretName, string fileKey, string nodeId, int depth, string? version, CancellationToken cancellationToken);

    /// <summary>GET /v1/files/:key/variables/local.</summary>
    Task<FigmaReadResult> GetLocalVariablesAsync(
        string secretName, string fileKey, CancellationToken cancellationToken);
}

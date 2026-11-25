namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-25-d0e2: whether Azure DevOps takes a token as Bearer. A Microsoft Entra access token is a JWT — three base64url
/// parts joined by dots, the header a JSON object (<c>eyJ…</c>) — and goes as <c>Authorization: Bearer</c>; a personal access
/// token has no dot and goes as Basic, as it always did. The token says it itself, so every client, process and replica
/// decides alike, and an Entra token a service principal minted or an operator configured is sent the same way.
/// </summary>
public static class AzureDevOpsBearer
{
    public static bool Is(string? token) =>
        token is { Length: > 0 } t && t.StartsWith("eyJ", StringComparison.Ordinal) && t.Count(c => c == '.') == 2
        && !t.EndsWith('.');
}

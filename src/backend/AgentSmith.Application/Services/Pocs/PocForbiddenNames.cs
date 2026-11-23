namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: names a PoC archive may not carry anywhere in its tree — a repository of its own (.git), secrets
/// (.env, .env.*, private keys and certificates) and compose files, which would describe a second way to run the app
/// beside its Dockerfile. A reference upload leaves such files out; a PoC is refused, so the person removes them.
/// </summary>
public sealed class PocForbiddenNames
{
    private static readonly string[] Exact = [".git", ".env", "id_rsa", "id_ed25519"];
    private static readonly string[] Extensions = [".pem", ".p12", ".pfx"];
    private static readonly string[] ComposePrefixes = ["docker-compose", "compose"];
    private static readonly string[] ComposeExtensions = [".yml", ".yaml"];

    internal PocRuleRefusal? RefusalOf(IReadOnlyList<PocArchiveItem> items) =>
        items.Select(i => (i.Path, Name: i.Path.Split('/').FirstOrDefault(IsForbidden)))
            .FirstOrDefault(f => f.Name is not null) is { Name: { } name } found
            ? new PocRuleRefusal(PocArchiveChecks.Forbidden,
                $"'{found.Path}' carries '{name}', which a PoC may not hold (.git, .env and .env.*, compose files, "
                + "id_rsa, id_ed25519, *.pem, *.p12, *.pfx); remove it, because a PoC's source is committed to a shared "
                + "repository and runs from its Dockerfile alone.")
            : null;

    // Whether one path segment is a forbidden name, in any case.
    private static bool IsForbidden(string segment)
    {
        var name = segment.ToLowerInvariant();
        return Exact.Contains(name) || name.StartsWith(".env.", StringComparison.Ordinal)
            || Extensions.Any(e => name.EndsWith(e, StringComparison.Ordinal))
            || (ComposePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal))
                && ComposeExtensions.Any(e => name.EndsWith(e, StringComparison.Ordinal)));
    }
}

using AgentSmith.Tests.Architecture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: which routes are anonymous and which are permissioned, read from p0503a's golden
/// rather than retyped. A copy would go stale the moment a route changed sides, which is
/// the one event these assertions exist for.
/// </summary>
internal static class AnonymousRoutes
{
    private const string GoldenFile = "route-permission-baseline.tsv";
    private const string Anonymous = "anonymous";

    public static IReadOnlyList<(string Method, string Path)> FromGolden() =>
        [.. Golden().Where(r => r.Declaration == Anonymous).Select(r => (r.Method, r.Path))];

    /// <summary>The permissioned GETs a test can call without inventing an id.</summary>
    public static IReadOnlyList<string> PermissionedParameterlessGets() =>
        [.. Golden()
            .Where(r => r.Declaration != Anonymous && r.Method == "GET" && !r.Path.Contains('{'))
            .Select(r => r.Path)];

    private static IEnumerable<(string Method, string Path, string Declaration)> Golden() =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Server", GoldenFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(parts => (parts[0], parts[1], parts[2]));
}

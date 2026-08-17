using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: the API surface the scan really loaded, and whether a claimed endpoint lands
/// in it — <see cref="Specs.CitedFileIndex"/>'s job for a target that has no files.
/// <para>
/// A DAST finding cites an ApiPath or a schema name, so reading "the file it names" checks
/// nothing. The specification the scan was run against is the evidence: an endpoint the
/// document does not declare is the invented location, exactly as an unreadable file is.
/// </para>
/// <para>
/// An EMPTY index means no specification was loaded, not that every citation is invented.
/// It answers nothing, so nothing is dropped — the check is silent where it has no
/// evidence rather than refusing a healthy run.
/// </para>
/// </summary>
public sealed class CitedEndpointIndex
{
    private readonly List<(string Declaration, string[] Segments)> endpoints = [];
    private readonly HashSet<string> schemas = new(StringComparer.OrdinalIgnoreCase);

    private CitedEndpointIndex(SwaggerSpec? spec)
    {
        if (spec is null) return;
        foreach (var endpoint in spec.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Path)) continue;
            endpoints.Add((
                $"{endpoint.Method.ToUpperInvariant()} {endpoint.Path}"
                + (endpoint.RequiresAuth ? " (requires authentication)" : " (no security requirement)"),
                ApiPathTokens.Segments(endpoint.Path)));
            foreach (var schema in new[] { endpoint.RequestBodySchema, endpoint.ResponseSchema })
                if (!string.IsNullOrWhiteSpace(schema)) schemas.Add(schema);
        }
    }

    public static CitedEndpointIndex FromSpec(SwaggerSpec? spec) => new(spec);

    public static CitedEndpointIndex Empty { get; } = new(null);

    public bool IsEmpty => endpoints.Count == 0 && schemas.Count == 0;

    /// <summary>Does this citation name something the loaded specification declares?</summary>
    public bool Contains(string? citation) => Declarations(citation).Count > 0;

    /// <summary>
    /// What the specification says about the cited endpoint — the DAST equivalent of the
    /// lines around a cited one, so a refuter reading the exchange also reads what the
    /// document promised.
    /// </summary>
    public IReadOnlyList<string> Declarations(string? citation)
    {
        var matched = new List<string>();
        if (!string.IsNullOrWhiteSpace(citation) && schemas.Contains(citation.Trim()))
            matched.Add($"schema {citation.Trim()}");
        foreach (var cited in ApiPathTokens.PathsIn(citation).Select(ApiPathTokens.Segments))
            foreach (var endpoint in endpoints)
                if (ApiPathTokens.Matches(endpoint.Segments, cited)
                    && !matched.Contains(endpoint.Declaration, StringComparer.Ordinal))
                    matched.Add(endpoint.Declaration);
        return matched;
    }
}

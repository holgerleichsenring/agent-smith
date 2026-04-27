using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security.Code;

/// <summary>
/// Maps swagger endpoints to their handler locations by scanning source files
/// for framework-specific route declarations and matching method + path.
/// </summary>
public sealed class RouteMapper(ILogger<RouteMapper> logger) : IRouteMapper
{
    public IReadOnlyList<RouteHandlerLocation> MapRoutes(
        IReadOnlyList<ApiEndpoint> endpoints, string repoPath)
    {
        var declared = ScanDeclarations(repoPath);
        logger.LogDebug("RouteMapper: scanned {Count} route declarations in {Path}", declared.Count, repoPath);
        return MatchToEndpoints(declared, endpoints);
    }

    private static List<RouteDeclaration> ScanDeclarations(string repoPath)
    {
        var results = new List<RouteDeclaration>();
        foreach (var file in SourceFileEnumerator.EnumerateSourceFiles(repoPath))
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            foreach (var pat in FrameworkRoutePatterns.All)
                if (pat.AppliesTo(file))
                    AccumulateMatches(file, text, pat, results);
        }
        return results;
    }

    private static void AccumulateMatches(
        string file, string text, FrameworkRoutePattern pat, List<RouteDeclaration> sink)
    {
        foreach (Match match in pat.Pattern.Matches(text))
        {
            var verb = match.Groups[1].Value.ToUpperInvariant();
            var path = match.Groups["path"].Value;
            var line = SourceSnippetReader.LineNumberFromOffset(text, match.Index);
            sink.Add(new RouteDeclaration(verb, path, file, line, pat.Framework));
        }
    }

    private static List<RouteHandlerLocation> MatchToEndpoints(
        List<RouteDeclaration> declared, IReadOnlyList<ApiEndpoint> endpoints)
    {
        var matched = new List<RouteHandlerLocation>();
        foreach (var d in declared)
        {
            var endpoint = endpoints.FirstOrDefault(e => PathsMatch(e.Path, d.Path));
            if (endpoint is null) continue;
            var confidence = endpoint.Method.Equals(d.Verb, StringComparison.OrdinalIgnoreCase)
                ? 1.0
                : 0.5;
            var (start, end, snippet) = SourceSnippetReader.Read(
                d.File, d.Line, SourceSnippetReader.DefaultHandlerLines);
            matched.Add(new RouteHandlerLocation(
                endpoint.Method, endpoint.Path, d.File, start, end,
                snippet, d.Framework, confidence));
        }
        return matched;
    }

    private static bool PathsMatch(string swaggerPath, string sourcePath) =>
        Canonicalize(swaggerPath).Equals(Canonicalize(sourcePath), StringComparison.OrdinalIgnoreCase);

    private static string Canonicalize(string path)
    {
        var normalized = Regex.Replace(path.TrimEnd('/'), @":[A-Za-z_][A-Za-z0-9_]*", "{}");
        normalized = Regex.Replace(normalized, @"\{[^}/]+\}", "{}");
        return normalized.StartsWith('/') ? normalized : "/" + normalized;
    }

    private sealed record RouteDeclaration(string Verb, string Path, string File, int Line, string Framework);
}

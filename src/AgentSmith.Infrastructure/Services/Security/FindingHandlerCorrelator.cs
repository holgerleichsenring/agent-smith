using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Compiles each route template (e.g. /users/{id}) into a regex
/// (^/users/[^/]+/?$) and matches finding URLs against the set.
/// Confidence threshold and HTTP method gate route admission.
/// </summary>
public sealed class FindingHandlerCorrelator : IFindingHandlerCorrelator
{
    private const double MinRouteConfidence = 0.5;
    private static readonly Regex PathParam = new(@"\{[^/}]+\}", RegexOptions.Compiled);

    public IReadOnlyList<FindingHandlerCorrelation> Correlate(
        NucleiResult? nuclei, ZapResult? zap, ApiCodeContext? codeContext)
    {
        var routes = BuildRouteIndex(codeContext);
        var result = new List<FindingHandlerCorrelation>();

        if (nuclei is not null)
            foreach (var f in nuclei.Findings)
                result.Add(BuildCorrelation("nuclei", f.TemplateId, f.Severity, ExtractMethod(f), f.MatchedUrl, routes));

        if (zap is not null)
            foreach (var f in zap.Findings)
                result.Add(BuildCorrelation("zap", f.AlertRef, f.RiskDescription, ExtractMethod(f), f.Url, routes));

        return result;
    }

    private static List<CompiledRoute> BuildRouteIndex(ApiCodeContext? code)
    {
        if (code is null) return [];
        return code.RoutesToHandlers
            .Where(r => r.Confidence >= MinRouteConfidence)
            .Select(r => new CompiledRoute(r, BuildRegex(r.Path)))
            .ToList();
    }

    private static Regex BuildRegex(string template)
    {
        // Swap {param} slots for an inert placeholder before Regex.Escape, since Escape
        // mangles { and }. Then Escape, then swap the placeholder for the URL-segment match.
        const string placeholder = "PARAM";
        var withPlaceholder = PathParam.Replace(template, placeholder);
        var escaped = Regex.Escape(withPlaceholder);
        var withParams = escaped.Replace(placeholder, "[^/]+");
        return new Regex($"^{withParams}/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static FindingHandlerCorrelation BuildCorrelation(
        string source, string id, string severity, string method, string url,
        List<CompiledRoute> routes)
    {
        var path = StripQueryAndNormalize(url);
        var handler = routes
            .FirstOrDefault(r =>
                (string.IsNullOrEmpty(method) || string.Equals(r.Route.Method, method, StringComparison.OrdinalIgnoreCase))
                && r.Pattern.IsMatch(path));
        return new FindingHandlerCorrelation(source, id, severity, method, url, handler?.Route);
    }

    private static string StripQueryAndNormalize(string url)
    {
        var path = url;
        var queryIdx = path.IndexOf('?');
        if (queryIdx >= 0) path = path[..queryIdx];
        if (Uri.TryCreate(path, UriKind.Absolute, out var abs)) path = abs.AbsolutePath;
        return path.Length > 1 && path.EndsWith('/') ? path.TrimEnd('/') : path;
    }

    private static string ExtractMethod(NucleiFinding f) =>
        TryExtractMethodFromTemplate(f.TemplateId);

    private static string ExtractMethod(ZapFinding _) => "";

    private static string TryExtractMethodFromTemplate(string templateId)
    {
        // Nuclei templates often hint at method via id, e.g. "post-bola-test".
        var lower = templateId.ToLowerInvariant();
        foreach (var m in new[] { "get", "post", "put", "delete", "patch" })
            if (lower.Contains($"{m}-") || lower.StartsWith($"{m}-")) return m.ToUpperInvariant();
        return "";
    }

    private sealed record CompiledRoute(RouteHandlerLocation Route, Regex Pattern);
}

using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Services.Security.Code;

/// <summary>
/// Extracts ASP.NET controller routes by combining class-level <c>[Route("api/X")]</c>
/// with method-level <c>[HttpGet("Y")]</c> or <c>[Route("Y")] [HttpGet]</c> pairings.
/// Also resolves the <c>[controller]</c> token to the controller name (minus
/// the "Controller" suffix). The single-regex approach in FrameworkRoutePatterns
/// only catches inline paths on HTTP attributes, so most real-world controllers
/// (those with a class-level Route prefix) were unmatched and downstream
/// findings could not be correlated to source.
/// </summary>
internal static partial class DotNetRouteExtractor
{
    // Class-level [Route("path")] immediately preceding a controller class declaration.
    // Intervening attributes ([ApiController], [Authorize], comments) are tolerated by
    // the lazy [\s\S]*? gap. Anchored against the next `public ... class ...Controller`
    // to avoid capturing a method-level [Route] as if it were class-level.
    [GeneratedRegex(
        """\[Route\(\s*"(?<prefix>[^"]+)"\s*\)\][\s\S]*?\bpublic\s+(?:partial\s+|sealed\s+|abstract\s+|static\s+)*class\s+(?<class>\w+Controller)\b""",
        RegexOptions.Compiled)]
    private static partial Regex ClassRouteRegex();

    // Method-level HTTP attribute with optional inline path.
    // - With path:    [HttpGet("user/{id}")]
    // - Without path: [HttpGet]
    // Captures the verb and an optional path; the brace style varies (() vs none).
    [GeneratedRegex(
        """\[Http(?<verb>Get|Post|Put|Delete|Patch|Head|Options)(?:\s*\(\s*"(?<path>[^"]*)"\s*(?:,[^)]*)?\))?\s*\]""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex HttpAttrRegex();

    // Method-level [Route("subpath")] — only relevant when paired with a verb-attribute
    // that has no inline path. Same shape as the class-level Route but matched per-method.
    [GeneratedRegex(
        """\[Route\(\s*"(?<path>[^"]+)"\s*\)\]""",
        RegexOptions.Compiled)]
    private static partial Regex AnyRouteRegex();

    public static IEnumerable<ExtractedRoute> Extract(string filePath, string text)
    {
        var classes = FindClassRoutes(text).ToList();
        var classRoutePositions = ClassRouteRegex().Matches(text).Cast<Match>()
            .Select(m => m.Index).ToHashSet();

        foreach (var verbMatch in HttpAttrRegex().Matches(text).Cast<Match>())
        {
            var owner = FindOwningClass(classes, verbMatch.Index);
            var prefix = owner?.Prefix ?? "";

            var verb = verbMatch.Groups["verb"].Value.ToUpperInvariant();
            var inlinePath = verbMatch.Groups["path"].Success ? verbMatch.Groups["path"].Value : null;
            // Verb attribute without an inline path is paired with a nearby [Route("...")] —
            // these can appear on either side of the HTTP verb (real-world ASP.NET allows both).
            // The pairing window is bounded so a distant Route on a different method can't
            // be misattributed.
            var methodPath = inlinePath ?? FindAdjacentMethodRoute(text, verbMatch.Index, classRoutePositions);

            var combined = Combine(prefix, methodPath);
            yield return new ExtractedRoute(verb, combined, filePath, LineFromOffset(text, verbMatch.Index));
        }
    }

    private static IEnumerable<ClassRoute> FindClassRoutes(string text)
    {
        foreach (var m in ClassRouteRegex().Matches(text).Cast<Match>())
        {
            var rawPrefix = m.Groups["prefix"].Value;
            var className = m.Groups["class"].Value;
            var resolved = ResolveControllerToken(rawPrefix, className);
            yield return new ClassRoute(resolved, m.Index + m.Length);
        }
    }

    private static ClassRoute? FindOwningClass(IReadOnlyList<ClassRoute> classes, int offset)
    {
        // Largest declaration offset that's still <= verbMatch position.
        ClassRoute? owner = null;
        foreach (var c in classes)
            if (c.DeclarationOffset <= offset && (owner is null || c.DeclarationOffset > owner.DeclarationOffset))
                owner = c;
        return owner;
    }

    // Bidirectional scan around the verb attribute for a method-level [Route("path")].
    // Pairings exist on either side in real ASP.NET ([Route] above OR below [HttpGet]).
    // Window is bounded to a few hundred chars to avoid pairing with unrelated routes
    // from neighboring methods. Skips Route matches that we already identified as
    // class-level (their offsets are in classRoutePositions).
    private static string? FindAdjacentMethodRoute(string text, int verbOffset, ISet<int> classRoutePositions)
    {
        const int Window = 200;
        var start = Math.Max(0, verbOffset - Window);
        var end = Math.Min(text.Length, verbOffset + Window);
        var slice = text[start..end];

        Match? closest = null;
        var closestDistance = int.MaxValue;
        foreach (var m in AnyRouteRegex().Matches(slice).Cast<Match>())
        {
            var absoluteOffset = start + m.Index;
            if (classRoutePositions.Contains(absoluteOffset)) continue;
            var distance = Math.Abs(absoluteOffset - verbOffset);
            if (distance < closestDistance)
            {
                closest = m;
                closestDistance = distance;
            }
        }
        return closest?.Groups["path"].Value;
    }

    // [Route("api/[controller]")] → "api/auth" (when class is AuthController). Token
    // replacement is case-insensitive; "Controller" suffix is stripped before
    // substitution to match ASP.NET's default conventional binding.
    private static string ResolveControllerToken(string template, string className)
    {
        var controllerName = className.EndsWith("Controller", StringComparison.Ordinal)
            ? className[..^"Controller".Length]
            : className;
        return Regex.Replace(template, @"\[controller\]", controllerName, RegexOptions.IgnoreCase);
    }

    private static string Combine(string prefix, string? subpath)
    {
        var head = (prefix ?? "").Trim('/');
        var tail = (subpath ?? "").Trim('/');
        if (head.Length == 0 && tail.Length == 0) return "/";
        if (head.Length == 0) return "/" + tail;
        if (tail.Length == 0) return "/" + head;
        return "/" + head + "/" + tail;
    }

    private static int LineFromOffset(string text, int offset)
    {
        var line = 1;
        for (var i = 0; i < offset && i < text.Length; i++)
            if (text[i] == '\n') line++;
        return line;
    }

    private sealed record ClassRoute(string Prefix, int DeclarationOffset);
}

internal sealed record ExtractedRoute(string Verb, string Path, string File, int Line);

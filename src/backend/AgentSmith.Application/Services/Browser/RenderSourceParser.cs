using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: reads the model's <c>source</c> — <c>reference:&lt;name&gt;</c>, optionally
/// followed by <c>/&lt;page&gt;</c> inside the set, or an absolute URL. An address is looked up in
/// the turn's own sandbox map, so only a set the conversation holds can be named; a URL is only
/// parsed here and judged by <see cref="RenderUrlGuard"/>.
/// </summary>
public sealed class RenderSourceParser
{
    /// <summary>The source, or why the text names none.</summary>
    public (RenderSource? Source, string? Refusal) Parse(
        string source, IReadOnlyDictionary<string, ISandbox> sandboxes)
    {
        var text = (source ?? string.Empty).Trim();
        if (text.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            return OfAddress(text, sandboxes);
        if (Uri.TryCreate(text, UriKind.Absolute, out var url))
            return (RenderSource.OfUrl(url), null);
        return (null, $"'{text}' is neither a {ReferenceScopeName.Prefix} address nor an absolute http(s) URL");
    }

    private static (RenderSource?, string?) OfAddress(string text, IReadOnlyDictionary<string, ISandbox> sandboxes)
    {
        var slash = text.IndexOf('/');
        var address = slash < 0 ? text : text[..slash];
        var page = slash < 0 ? string.Empty : text[(slash + 1)..];
        if (page.Contains("..", StringComparison.Ordinal) || page.Contains('\\'))
            return (null, $"'{page}' is not a page inside the set");
        if (!sandboxes.TryGetValue(address, out var scope) || scope is not ReferenceSetSandbox set)
            return (null, $"'{address}' is not an uploaded website of this conversation");
        return (RenderSource.OfSet(set.SetId, page), null);
    }
}

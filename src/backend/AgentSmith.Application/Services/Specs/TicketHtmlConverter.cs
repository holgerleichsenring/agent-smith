using System.Net;
using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0399: an Azure DevOps ticket body arrives as HTML. Segments cut from it raw carry
/// the bytes of the ENCODING, not of the content — a phase companion promising "copied
/// byte for byte out of the ticket" would hand the implementing agent
/// entity-corrupted commands. Converted ONCE, at segment ingestion, so every
/// downstream consumer (derivation prompt, phase companions, accounting, run viewer)
/// reads the ticket's TEXT. A body already written as plain text or markdown passes
/// through unchanged.
/// </summary>
public static partial class TicketHtmlConverter
{
    private const char Marker = '\x01';

    public static string ToText(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body ?? string.Empty;
        if (HtmlTag().IsMatch(body)) return Convert(body);
        // Entities without a single tag still mean the transport encoded the text:
        // nobody types `&quot;` into a ticket by hand.
        return HtmlEntity().IsMatch(body) ? Decode(body) : body;
    }

    private static string Convert(string html)
    {
        var text = html.Replace("\r\n", "\n", StringComparison.Ordinal);
        text = ScriptOrStyleBlock().Replace(text, string.Empty);
        var fences = new List<string>();
        text = PreBlock().Replace(text, m => Park(fences, m.Groups["code"].Value));
        text = BlockBoundaryTag().Replace(text, "\n");
        text = Decode(StripTags(text));
        text = ExcessBlankLines().Replace(text, "\n\n");
        return Restore(text, fences).Trim() + "\n";
    }

    // A fenced code block is ONE segment to the segmenter no matter how many blank
    // lines it holds — parking the <pre> content and restoring it inside a fence
    // keeps that property through the conversion.
    private static string Park(List<string> fences, string rawCode)
    {
        var code = Decode(StripTags(LineBreakTag().Replace(rawCode, "\n"))).Trim('\n');
        fences.Add(code);
        return $"\n{Marker}{fences.Count - 1}{Marker}\n";
    }

    private static string Restore(string text, List<string> fences)
    {
        for (var i = 0; i < fences.Count; i++)
            text = text.Replace(
                $"{Marker}{i}{Marker}", $"```\n{fences[i]}\n```", StringComparison.Ordinal);
        return text;
    }

    // Decoding runs AFTER tag stripping, so `&lt;Domain&gt;ServiceClient` decodes to
    // literal angle brackets instead of being stripped as markup.
    private static string StripTags(string text) => AnyTag().Replace(text, string.Empty);

    // &nbsp; decodes to U+00A0, which reads as a space but corrupts a pasted command.
    private static string Decode(string text) =>
        WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');

    [GeneratedRegex(
        @"</?(p|div|br|li|ul|ol|h[1-6]|pre|code|span|table|thead|tbody|tr|td|th|b|i|u"
        + @"|strong|em|a|img|blockquote|hr)(\s[^>]*)?/?>",
        RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"&(quot|amp|lt|gt|nbsp|apos|#\d+|#x[0-9a-fA-F]+);")]
    private static partial Regex HtmlEntity();

    [GeneratedRegex(@"<(script|style)[^>]*>.*?</\1>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyleBlock();

    [GeneratedRegex(@"<pre[^>]*>(?<code>.*?)</pre>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex PreBlock();

    [GeneratedRegex(@"<br[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTag();

    [GeneratedRegex(
        @"</?(p|div|li|ul|ol|h[1-6]|tr|table|blockquote)[^>]*>|<(br|hr)[^>]*/?>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryTag();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLines();
}

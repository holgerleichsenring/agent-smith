using System.ComponentModel;
using System.Net.Http.Headers;
using AgentSmith.Application.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0154: in-process web_fetch tool, mirroring the MCP fetch-server shape.
/// HTML responses are converted to markdown via HtmlAgilityPack + ReverseMarkdown;
/// non-HTML bodies are returned as-is. <c>raw=true</c> bypasses conversion.
/// Pagination via <c>max_length</c> (default 100k chars) and <c>start_index</c>.
/// Lives in the C# process rather than the sandbox — the URLs are public docs
/// (no repo access) and the agent already reaches the open internet for LLM
/// calls, so a sandbox round-trip adds latency with no security benefit.
/// </summary>
public sealed class WebToolHost : IToolHost
{
    private const int DefaultMaxLength = 100_000;
    private const int DefaultTimeoutSeconds = 30;
    private const string UserAgent = "agent-smith-web-fetch/1.0";

    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public WebToolHost(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout == TimeSpan.Zero)
            _httpClient.Timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("agent-smith-web-fetch", "1.0"));
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
    [
        AIFunctionFactory.Create(WebFetch, name: "web_fetch")
    ];

    [Description("Fetches an HTTP(S) URL and returns its content as markdown (HTML→md conversion by default) or raw text. Use for public docs / changelogs / vendor advisories the agent has no other access to. Truncates at max_length characters from start_index; the response prefix indicates truncation so the caller can request the next page.")]
    public async Task<string> WebFetch(
        [Description("Absolute URL to fetch (http or https only).")] string url,
        [Description("Maximum number of characters to return (default 100000). Use a smaller value when paginating.")] int? max_length = null,
        [Description("Character offset into the converted/raw body. Default 0. Use with max_length for pagination.")] int? start_index = null,
        [Description("Return raw response body without HTML→markdown conversion. Default false.")] bool? raw = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Error: url is required.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            return $"Error: '{url}' is not an http or https URL.";

        var limit = max_length is > 0 ? max_length.Value : DefaultMaxLength;
        var offset = start_index is > 0 ? start_index.Value : 0;
        var rawMode = raw is true;

        _logger?.LogInformation("tool_call: web_fetch url={Url} max_length={Max} start_index={Start} raw={Raw}",
            url, limit, offset, rawMode);

        try
        {
            using var response = await _httpClient.GetAsync(parsed, ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            var body = await response.Content.ReadAsStringAsync(ct);

            var converted = rawMode || !IsHtml(contentType)
                ? body
                : ConvertHtmlToMarkdown(body);

            return Paginate(converted, offset, limit, response.StatusCode, contentType);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"Error: web_fetch timed out after {_httpClient.Timeout.TotalSeconds:F0}s ({url}).";
        }
        catch (HttpRequestException ex)
        {
            return $"Error: web_fetch failed for {url}: {ex.Message}";
        }
    }

    private static bool IsHtml(string contentType) =>
        contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("xhtml", StringComparison.OrdinalIgnoreCase);

    private static string ConvertHtmlToMarkdown(string html)
    {
        var doc = new HtmlDocument { OptionAutoCloseOnEnd = true };
        doc.LoadHtml(html);
        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        foreach (var node in body.SelectNodes("//script|//style|//noscript")?.ToList() ?? new List<HtmlNode>())
            node.Remove();

        var converter = new Converter(new Config
        {
            UnknownTags = Config.UnknownTagsOption.PassThrough,
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true
        });
        return converter.Convert(body.InnerHtml).Trim();
    }

    private static string Paginate(string content, int offset, int limit, System.Net.HttpStatusCode status, string contentType)
    {
        var header = $"HTTP {(int)status} ({contentType}) | length={content.Length} chars";
        if (offset >= content.Length)
        {
            return $"{header} | start_index={offset} is at or beyond end of content; nothing to return.";
        }

        var slice = content.AsSpan(offset, Math.Min(limit, content.Length - offset)).ToString();
        var truncated = offset + slice.Length < content.Length;
        var tail = truncated
            ? $"\n\n(truncated at {offset + slice.Length}/{content.Length}; resume with start_index={offset + slice.Length})"
            : string.Empty;
        return $"{header} | start_index={offset} | returned={slice.Length} chars{(truncated ? " (truncated)" : "")}\n\n{slice}{tail}";
    }
}

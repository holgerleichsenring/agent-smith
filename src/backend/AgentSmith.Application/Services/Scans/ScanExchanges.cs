using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: the request/response pairs the scanners really made, looked up by the endpoint
/// a finding cites.
/// <para>
/// The scanners report per URL and the master writes its finding against a path template,
/// so the two are joined by the same segment rule the endpoint index uses. A citation with
/// no exchange behind it is normal — Nuclei emits the pair only when asked and the ZAP
/// report may carry none — and it means the finding is never put to a refuter rather than
/// put to one with evidence nobody has.
/// </para>
/// </summary>
public sealed class ScanExchanges
{
    private readonly List<(string[] Segments, HttpExchange Exchange)> exchanges = [];

    private ScanExchanges(IEnumerable<HttpExchange> captured)
    {
        foreach (var exchange in captured)
        {
            var path = ApiPathTokens.PathsIn(exchange.Url).FirstOrDefault();
            if (path is null) continue;
            exchanges.Add((ApiPathTokens.Segments(path), exchange));
        }
    }

    public static ScanExchanges From(IEnumerable<HttpExchange?>? captured) =>
        new((captured ?? []).OfType<HttpExchange>());

    public static ScanExchanges Empty { get; } = From(null);

    /// <summary>
    /// The exchange behind this citation, or null when the scanners kept none. The
    /// citation is the TEMPLATE and the exchange's URL is a CONCRETE call, so the
    /// template's segments are matched against the recorded ones.
    /// </summary>
    public HttpExchange? For(string? citation)
    {
        foreach (var cited in ApiPathTokens.PathsIn(citation).Select(ApiPathTokens.Segments))
        {
            var hit = exchanges.FirstOrDefault(e => ApiPathTokens.Matches(cited, e.Segments));
            if (hit.Exchange is not null) return hit.Exchange;
        }
        return null;
    }
}

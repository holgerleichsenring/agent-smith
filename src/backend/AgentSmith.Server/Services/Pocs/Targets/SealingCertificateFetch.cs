using AgentSmith.Contracts.Pocs;
using AgentSmith.Server.Services.Pocs.Import;

namespace AgentSmith.Server.Services.Pocs.Targets;

/// <summary>
/// 2026-11-23-933dg: reads a Sealed Secrets controller's public certificate for the settings form — from a pasted PEM,
/// or fetched from an https URL (redirects off, the body read up to <see cref="MaxBytes"/>). A run-free read: nothing is
/// saved until the form saves; no credential is sent. A body that is no certificate is refused, never echoed.
/// </summary>
internal sealed class SealingCertificateFetch(IHttpClientFactory clients, ILogger<SealingCertificateFetch> logger)
{
    public const string ClientName = "sealed-secrets-cert";
    public const int MaxBytes = 64 * 1024; // not a limit: a read bound of a one-off fetch — a certificate is about 2 KB

    public async Task<(SealingCertificateAnswer? Answer, PocRefusal? Refusal)> ReadAsync(SealingCertificateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrWhiteSpace(request.Pem)) return Parsed(request.Pem.Trim());
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
            return (null, PocRefusal.BadRequest("Name an https URL to fetch the certificate from, or paste the PEM: the "
                + "certificate the PoCs are sealed with must not be read over a channel anyone can change."));
        try
        {
            using var response = await clients.CreateClient(ClientName).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return (null, BadGateway($"{url.Host} answered {(int)response.StatusCode}"));
            return await BodyAsync(response, ct) is { } pem ? Parsed(pem) : (null, BadGateway($"the answer is larger than {MaxBytes / 1024} KB"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "The Sealed Secrets certificate could not be fetched from {Host}", url.Host);
            return (null, BadGateway($"{url.Host} could not be reached ({ex.GetType().Name})"));
        }
    }

    private static async Task<string?> BodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxBytes + 1];
        var (read, last) = (0, -1);
        while (read < buffer.Length && (last = await body.ReadAsync(buffer.AsMemory(read), ct)) > 0) read += last;
        return read > MaxBytes ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, read).Trim();
    }

    private static (SealingCertificateAnswer?, PocRefusal?) Parsed(string pem)
    {
        try
        {
            var certificate = SealingCertificate.Parse(pem);
            return (new SealingCertificateAnswer(pem, certificate.Fingerprint, certificate.NotBefore, certificate.NotAfter), null);
        }
        catch (FormatException ex)
        {
            return (null, PocRefusal.BadRequest($"The certificate {ex.Message}."));
        }
    }

    private static PocRefusal BadGateway(string why) =>
        new(StatusCodes.Status502BadGateway, $"The certificate was not fetched: {why}.");
}

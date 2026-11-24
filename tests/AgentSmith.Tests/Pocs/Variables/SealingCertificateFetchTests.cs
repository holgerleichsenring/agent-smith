using System.Net;
using AgentSmith.Server.Services.Pocs.Targets;
using AgentSmith.Tests.Pocs.Sealing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: the settings form's certificate read — a pasted PEM answers fingerprint and validity, a
/// plain-http URL is refused, a fetched body that is no certificate is refused and never echoed, an oversized one too.</summary>
public sealed class SealingCertificateFetchTests
{
    [Fact]
    public async Task Read_APastedPem_AnswersFingerprintAndValidity()
    {
        var (answer, refusal) = await Fetch(HttpStatusCode.OK, "").ReadAsync(new SealingCertificateRequest(null, KubeSealerTests.Fixture("kubeseal-cert.pem")), default);

        refusal.Should().BeNull();
        answer!.Fingerprint.Should().StartWith("sha256:");
        answer.NotAfter.Year.Should().Be(2036);
    }

    [Fact]
    public async Task Read_APlainHttpUrl_IsRefused()
    {
        var (_, refusal) = await Fetch(HttpStatusCode.OK, "").ReadAsync(new SealingCertificateRequest("http://controller/v1/cert.pem", null), default);

        (refusal!.Status, refusal.Reason.Contains("https")).Should().Be((400, true));
    }

    [Fact]
    public async Task Read_AFetchedCertificate_IsAnswered_ABodyThatIsNoneIsRefusedUnechoed()
    {
        var pem = KubeSealerTests.Fixture("kubeseal-cert.pem");
        (await Fetch(HttpStatusCode.OK, pem).ReadAsync(new SealingCertificateRequest("https://seal.example.internal/v1/cert.pem", null), default))
            .Answer!.Pem.Should().Be(pem);

        var (_, refusal) = await Fetch(HttpStatusCode.OK, "<html>login page</html>")
            .ReadAsync(new SealingCertificateRequest("https://seal.example.internal/v1/cert.pem", null), default);
        refusal!.Reason.Should().Contain("does not parse").And.NotContain("login page");
        (await Fetch(HttpStatusCode.OK, new string('x', SealingCertificateFetch.MaxBytes + 1))
            .ReadAsync(new SealingCertificateRequest("https://seal.example.internal/v1/cert.pem", null), default)).Refusal!.Status.Should().Be(502);
    }

    private static SealingCertificateFetch Fetch(HttpStatusCode status, string body)
    {
        var clients = new Mock<IHttpClientFactory>();
        clients.Setup(c => c.CreateClient(SealingCertificateFetch.ClientName)).Returns(() => new HttpClient(new Answer(status, body)));
        return new SealingCertificateFetch(clients.Object, NullLogger<SealingCertificateFetch>.Instance);
    }

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}

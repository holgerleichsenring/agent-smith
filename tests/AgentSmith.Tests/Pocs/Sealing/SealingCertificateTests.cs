using System.Security.Cryptography;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Sealing;

/// <summary>2026-11-23-933dg: the controller's certificate read from PEM, and the target field's rule.</summary>
public sealed class SealingCertificateTests
{
    [Fact]
    public void SealingCertificate_Parse_FingerprintAndNotAfter()
    {
        var pem = KubeSealerTests.Fixture("kubeseal-cert.pem");
        var certificate = SealingCertificate.Parse(pem);

        certificate.Fingerprint.Should().MatchRegex("^sha256:[0-9a-f]{64}$");
        certificate.NotBefore.Should().Be(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        certificate.NotAfter.Should().Be(new DateTimeOffset(2036, 11, 1, 0, 0, 0, TimeSpan.Zero));
        certificate.PublicKey.Exponent.Should().NotBeEmpty();
    }

    [Fact]
    public void SealingCertificate_NotPem_RefusedNamingField()
    {
        FluentActions.Invoking(() => SealingCertificate.Parse("not a certificate")).Should().Throw<FormatException>();
        new PocSecretsConfig { SealedSecretsCert = "not a certificate" }.Problems("main").Should().ContainSingle()
            .Which.Should().StartWith("pocs.targets[].secrets.sealed_secrets_cert of target 'main' does not parse");
    }

    [Fact]
    public void Problems_AnotherProviderOrAPlainHttpUrl_AreRefusedNamingTheKeys()
    {
        new PocSecretsConfig { Provider = "vault", SealedSecretsCertUrl = "http://x/cert" }.Problems("main").Should().HaveCount(2)
            .And.Contain(p => p.Contains("secrets.provider of target 'main' is 'vault'"))
            .And.Contain(p => p.Contains("sealed_secrets_cert_url of target 'main'"));
        new PocSecretsConfig { SealedSecretsCert = KubeSealerTests.Fixture("kubeseal-cert.pem") }.Problems("main").Should().BeEmpty();
    }

    [Fact]
    public void SealingCertificate_AnEcKey_IsRefused()
    {
        using var ec = ECDsa.Create();
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=ec", ec, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        SealingCertificate.Problem(cert.ExportCertificatePem()).Should().Contain("no RSA public key");
    }
}

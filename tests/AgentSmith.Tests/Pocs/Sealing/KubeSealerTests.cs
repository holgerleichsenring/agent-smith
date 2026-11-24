using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Services.Pocs.Sealing;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Sealing;

/// <summary>2026-11-23-933dg: KubeSealer is kubeseal's HybridEncrypt — pinned by the ported controller decryptor and by a
/// value kubeseal's own library sealed (sealed-secrets v0.27.1, Fixtures/kubeseal-*).</summary>
public sealed class KubeSealerTests
{
    private static readonly KubeSealer Sealer = new(new EmeOaep(), new RsaPublicOperation());
    private static readonly byte[] Label = KubeSealer.Label("poc-demo", PocSecretNames.Secrets);

    [Fact]
    public void Seal_OpensWithMatchingPrivateKeyAndLabel()
    {
        using var rsa = RSA.Create(2048);
        var sealedBytes = Sealer.Seal(rsa.ExportParameters(false), Encoding.UTF8.GetBytes("sk-live-value-933dg"), Label);

        Encoding.UTF8.GetString(KubeOpener.Open(rsa.ExportParameters(true), sealedBytes, Label)).Should().Be("sk-live-value-933dg");
        Encoding.ASCII.GetString(Label).Should().Be("poc-demo/poc-secrets");
    }

    [Fact]
    public void Seal_WrongNamespace_DoesNotOpen()
    {
        using var rsa = RSA.Create(2048);
        var sealedBytes = Sealer.Seal(rsa.ExportParameters(false), "value-933dg"u8.ToArray(), Label);

        FluentActions.Invoking(() => KubeOpener.Open(rsa.ExportParameters(true), sealedBytes, KubeSealer.Label("poc-other", PocSecretNames.Secrets)))
            .Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Seal_TwoSeals_Differ()
    {
        using var rsa = RSA.Create(2048);
        var key = rsa.ExportParameters(false);

        Sealer.Seal(key, "same-value"u8.ToArray(), Label).Should().NotEqual(Sealer.Seal(key, "same-value"u8.ToArray(), Label));
    }

    [Fact]
    public void Seal_KubesealFixture_OpensWithPortedDecryptor()
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(Fixture("kubeseal-key.der.b64")), out _);
        var sealedBytes = Convert.FromBase64String(Fixture("kubeseal-sealed.b64"));

        Encoding.UTF8.GetString(KubeOpener.Open(rsa.ExportParameters(true), sealedBytes, Label)).Should().Be("fixture-openai-key-933dg");
        SealingCertificate.Parse(Fixture("kubeseal-cert.pem")).PublicKey.Modulus.Should().Equal(rsa.ExportParameters(false).Modulus);
    }

    [Fact]
    public void Seal_EmptyLabelCiphertext_DoesNotOpen()
    {
        // Guards against RSA.Encrypt with RSAEncryptionPadding.OaepSHA256, which can only seal under an empty label.
        using var rsa = RSA.Create(2048);
        var wrapped = rsa.Encrypt(new byte[32], RSAEncryptionPadding.OaepSHA256);
        var sealedBytes = new byte[2 + wrapped.Length + 16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(sealedBytes, (ushort)wrapped.Length);
        wrapped.CopyTo(sealedBytes, 2);

        FluentActions.Invoking(() => KubeOpener.Open(rsa.ExportParameters(true), sealedBytes, Label))
            .Should().Throw<CryptographicException>().WithMessage("label mismatch");
    }

    internal static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Pocs", "Sealing", "Fixtures", name)).Trim();
}

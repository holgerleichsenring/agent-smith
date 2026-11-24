using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: the seal fingerprint is a keyed digest of every input — equal inputs agree, a re-wrap
/// (another key id, the same key) leaves it, another master key or a rotated token changes it, and it is not SHA-256.</summary>
public sealed class PocSealFingerprintTests
{
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Of_EqualInputs_EqualOutput_AndAReWrapLeavesIt()
    {
        var first = Fingerprint(Key).Of(Inputs("token-value-1"));

        first.Should().MatchRegex("^hmac-sha256:[0-9a-f]{64}$").And.HaveLength(76);
        Fingerprint(Key).Of(Inputs("token-value-1")).Should().Be(first, "set times and key ids are no inputs, so a re-wrap changes nothing");
    }

    [Fact]
    public void Of_AnotherMasterKeyOrARotatedToken_ChangesIt()
    {
        var first = Fingerprint(Key).Of(Inputs("token-value-1"));

        Fingerprint(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))).Of(Inputs("token-value-1")).Should().NotBe(first);
        Fingerprint(Key).Of(Inputs("token-value-2")).Should().NotBe(first);
    }

    [Fact]
    public void Of_TheDigest_DiffersFromSha256OfTheInputs()
    {
        var digest = Fingerprint(Key).Of(Inputs("token-value-1"))["hmac-sha256:".Length..];

        digest.Should().NotBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("token-value-1"))));
        new PocSealFingerprint(new MasterKeyRing(_ => null)).IsAvailable.Should().BeFalse();
    }

    private static PocSealFingerprint Fingerprint(string key) =>
        new(new MasterKeyRing(name => name == MasterKeyRing.Variable ? key : null));

    private static PocSealInputs Inputs(string token) => new("sealed_secrets", "sha256:cert", "poc-todo",
        new Dictionary<string, SecretInput> { ["OPENAI_API_KEY"] = new("sk-value-123456") }, "pull_secret",
        "pocregistry.azurecr.io", "poc-pull", new SecretInput(token));
}

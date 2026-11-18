using System.Security.Cryptography;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using FluentAssertions;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-17-f3a8d: envelope encryption — never deterministic, bound to its name and key, re-wrappable.</summary>
public sealed class SecretEnvelopeTests
{
    private static readonly string KeyA = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string KeyB = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly SecretEnvelope _envelope = new();

    [Fact]
    public void Seal_TheSameValueTwice_StoresDifferentBytesThatOpenToIt()
    {
        var key = Ring(KeyA).Current!;

        var first = _envelope.Seal("gh", "tok-123456", key);
        var second = _envelope.Seal("gh", "tok-123456", key);

        first.Ciphertext.Should().NotEqual(second.Ciphertext);
        first.Nonce.Should().NotEqual(second.Nonce);
        first.WrappedKey.Should().NotEqual(second.WrappedKey);
        _envelope.Open("gh", first, key).Should().Be("tok-123456");
        System.Text.Encoding.UTF8.GetString(first.Ciphertext).Should().NotContain("tok-123456");
    }

    [Fact]
    public void Open_UnderAnotherNameOrKey_Fails()
    {
        var sealedSecret = _envelope.Seal("gh", "tok-123456", Ring(KeyA).Current!);

        FluentActions.Invoking(() => _envelope.Open("jira", sealedSecret, Ring(KeyA).Current!)).Should().Throw<CryptographicException>();
        FluentActions.Invoking(() => _envelope.Open("gh", sealedSecret with { KeyId = Ring(KeyB).Current!.Id }, Ring(KeyB).Current!))
            .Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Rewrap_ToTheCurrentKey_OpensUnderItAndKeepsTheCiphertext()
    {
        var ring = Ring(KeyB, previous: KeyA);
        var sealedSecret = _envelope.Seal("gh", "tok-123456", ring.Previous!);

        var moved = _envelope.Rewrap("gh", sealedSecret, ring.Find(sealedSecret.KeyId)!, ring.Current!);

        moved.KeyId.Should().Be(ring.Current!.Id);
        moved.Ciphertext.Should().Equal(sealedSecret.Ciphertext);
        _envelope.Open("gh", moved, ring.Current!).Should().Be("tok-123456");
    }

    [Fact]
    public void Ring_KeyIdsRevealNoKey_AndAMalformedKeyIsSaid()
    {
        var ring = Ring(KeyA);

        ring.Current!.Id.Should().StartWith("env:").And.NotContain(KeyA);
        Ring("not-base64!").Problem.Should().Contain(MasterKeyRing.Variable);
        Ring(KeyA, previous: KeyA).Previous.Should().BeNull("the same key twice is one key");
    }

    private static MasterKeyRing Ring(string current, string? previous = null) => new(name => name switch
    {
        MasterKeyRing.Variable => current,
        MasterKeyRing.Variable + "_PREVIOUS" => previous,
        _ => null,
    });
}

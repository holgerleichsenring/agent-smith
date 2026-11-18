using System.Security.Cryptography;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using FluentAssertions;

namespace AgentSmith.Tests.Admin;

/// <summary>
/// 2026-11-17-f3a8e: a master key delivered wrapped by the public half of an Azure Key Vault RSA key, unwrapped once by
/// the vault. The stand-in vault is a local RSA key doing what unwrapKey does (RSA-OAEP-256), and the key is wrapped
/// the way the docs tell an operator to — with the public half only.
/// </summary>
public sealed class MasterKeyVaultTests
{
    private const string KeyUrl = "https://kv-test.vault.azure.net/keys/agentsmith-master/0123456789abcdef0123456789abcdef";
    private static readonly byte[] Raw = RandomNumberGenerator.GetBytes(32);
    private readonly LocalVault _vault = new();

    [Fact]
    public void AWrappedKey_UnwrapsToTheSameIdAsTheRawOne_SoStoredRowsStillOpen()
    {
        var raw = new MasterKeyRing(Env((MasterKeyRing.Variable, Convert.ToBase64String(Raw))));
        var sealedSecret = new SecretEnvelope().Seal("gh", "tok-123456", raw.Current!);

        var wrapped = new MasterKeyRing(Env(
            (MasterKeyRing.WrappedVariable, _vault.WrapWithPublicKey(Raw)), (MasterKeyRing.VaultKeyVariable, KeyUrl)), _vault);

        wrapped.Problem.Should().BeNull();
        wrapped.Current!.Id.Should().Be(raw.Current!.Id);
        wrapped.Source.Should().Contain("Azure Key Vault key").And.Contain(KeyUrl).And.NotContain(Convert.ToBase64String(Raw));
        _vault.AskedFor.Should().Equal(new Uri(KeyUrl));
        new SecretEnvelope().Open("gh", sealedSecret, wrapped.Find(sealedSecret.KeyId)!).Should().Be("tok-123456");
    }

    [Fact]
    public void ThePreviousSlot_UnwrapsWithTheCurrentVaultKey_WhenNoPreviousOneIsNamed()
    {
        var ring = new MasterKeyRing(Env(
            (MasterKeyRing.Variable, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            (MasterKeyRing.WrappedVariable + "_PREVIOUS", _vault.WrapWithPublicKey(Raw)),
            (MasterKeyRing.VaultKeyVariable, KeyUrl)), _vault);

        ring.Previous.Should().NotBeNull();
        ring.Source.Should().Contain("environment");
    }

    [Fact]
    public void Problems_NameTheVariable_AndLeaveNoKey()
    {
        Ring((MasterKeyRing.Variable, Convert.ToBase64String(Raw)), (MasterKeyRing.WrappedVariable, _vault.WrapWithPublicKey(Raw)))
            .Problem.Should().Contain("set one");
        Ring((MasterKeyRing.WrappedVariable, _vault.WrapWithPublicKey(Raw)))
            .Problem.Should().Contain(MasterKeyRing.VaultKeyVariable);
        Ring((MasterKeyRing.WrappedVariable, _vault.WrapWithPublicKey(RandomNumberGenerator.GetBytes(16))), (MasterKeyRing.VaultKeyVariable, KeyUrl))
            .Problem.Should().Contain("32-byte");
        var unreachable = new MasterKeyRing(Env((MasterKeyRing.WrappedVariable, _vault.WrapWithPublicKey(Raw)),
            (MasterKeyRing.VaultKeyVariable, KeyUrl)), new FailingVault());
        unreachable.Current.Should().BeNull();
        unreachable.Problem.Should().Contain(KeyUrl).And.Contain("did not unwrap");
    }

    [Fact]
    public void AzureKeyVaultUnwrap_UnwrapsWithRsaOaep256AgainstTheNamedKey()
    {
        var client = new StandInCryptographyClient(_vault);
        var unwrap = new AzureKeyVaultUnwrap(keyId => client.For(keyId));

        unwrap.Unwrap(new Uri(KeyUrl), Convert.FromBase64String(_vault.WrapWithPublicKey(Raw))).Should().Equal(Raw);
        client.Algorithm.Should().Be(KeyWrapAlgorithm.RsaOaep256);
        client.KeyId.Should().Be(new Uri(KeyUrl));
        AzureKeyVaultUnwrap.Options().Retry.MaxRetries.Should().Be(2);
        AzureKeyVaultUnwrap.CredentialOptions().ExcludeAzureCliCredential.Should().BeTrue();
        AzureKeyVaultUnwrap.CredentialOptions().ExcludeManagedIdentityCredential.Should().BeFalse();
    }

    private MasterKeyRing Ring(params (string Name, string Value)[] variables) => new(Env(variables), _vault);

    private static Func<string, string?> Env(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(v => v.Name == name).Value;

    /// <summary>What unwrapKey does with an RSA key: RSA-OAEP with SHA-256 under the private half.</summary>
    internal sealed class LocalVault : IKeyVaultUnwrap
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public List<Uri> AskedFor { get; } = [];

        public string WrapWithPublicKey(byte[] key)
        {
            using var publicHalf = RSA.Create();
            publicHalf.ImportSubjectPublicKeyInfo(_rsa.ExportSubjectPublicKeyInfo(), out _);
            return Convert.ToBase64String(publicHalf.Encrypt(key, RSAEncryptionPadding.OaepSHA256));
        }

        public byte[] Unwrap(Uri keyId, byte[] wrapped)
        {
            AskedFor.Add(keyId);
            return _rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256);
        }
    }

    private sealed class FailingVault : IKeyVaultUnwrap
    {
        public byte[] Unwrap(Uri keyId, byte[] wrapped) => throw new Azure.RequestFailedException(403, "Forbidden\nheaders");
    }

    /// <summary>The SDK's client with its service call replaced — the methods are virtual for exactly this.</summary>
    private sealed class StandInCryptographyClient(LocalVault vault) : CryptographyClient
    {
        public Uri? KeyId { get; private set; }

        public KeyWrapAlgorithm? Algorithm { get; private set; }

        public StandInCryptographyClient For(Uri keyId)
        {
            KeyId = keyId;
            return this;
        }

        public override UnwrapResult UnwrapKey(KeyWrapAlgorithm algorithm, byte[] encryptedKey, CancellationToken cancellationToken = default)
        {
            Algorithm = algorithm;
            return CryptographyModelFactory.UnwrapResult(KeyId!.ToString(), vault.Unwrap(KeyId!, encryptedKey), algorithm);
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Admin;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-11-17-f3a8e, end to end: a server whose only master key is the wrapped one, unwrapped by a stand-in vault.
/// A secret run stores a value under it, the configuration resolves it, and the state names the vault key.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SecretStoreVaultTests(SecretStoreVaultTests.VaultFixture fixture) : IClassFixture<SecretStoreVaultTests.VaultFixture>
{
    private const string Value = "f3a8e-value-0123456789";

    [Fact]
    public async Task OnlyAWrappedKey_StoresAndResolvesValues_AndTheStateNamesTheVaultKey()
    {
        var set = await SendAsync(HttpMethod.Put, "/api/config/secret-values/f3a8e_one", new { value = Value });
        var state = await (await SendAsync(HttpMethod.Get, "/api/config/secret-values")).Content.ReadAsStringAsync();

        set.StatusCode.Should().Be(HttpStatusCode.OK, await set.Content.ReadAsStringAsync());
        await using var scope = fixture.Server.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Set<StoredSecret>().SingleAsync(s => s.Name == "f3a8e_one");
        row.KeyId.Should().Be(fixture.Server.Services.GetRequiredService<MasterKeyRing>().Current!.Id);
        fixture.Server.Services.GetRequiredService<ISecretValues>().Resolve("f3a8e_one").Should().Be(Value);
        state.Should().Contain("Azure Key Vault key").And.Contain(VaultFixture.KeyUrl).And.NotContain(Value);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            fixture.Issuer.Token(AuthorityFixture.Audience, extra: [new Claim("roles", BuiltInRoles.Admin)]));
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Server.Client.SendAsync(request);
    }

    public sealed class VaultFixture : AuthorityFixture
    {
        internal const string KeyUrl = "https://kv-test.vault.azure.net/keys/agentsmith-master/0123456789abcdef0123456789abcdef";

        protected override bool MigratesStore => true;

        protected override Action<IServiceCollection>? Services => services =>
        {
            var vault = new MasterKeyVaultTests.LocalVault();
            var wrapped = vault.WrapWithPublicKey(RandomNumberGenerator.GetBytes(32));
            services.RemoveAll<MasterKeyRing>();
            services.AddSingleton(new MasterKeyRing(name => name switch
            {
                MasterKeyRing.WrappedVariable => wrapped,
                MasterKeyRing.VaultKeyVariable => KeyUrl,
                _ => null,
            }, vault));
        };

        protected override string AuthYaml(string authority) => $"""
            auth:
              authority: {authority}
              audience: {Audience}
              enforce: true
              role_claim: roles
              name_claim: sub
            """;
    }
}

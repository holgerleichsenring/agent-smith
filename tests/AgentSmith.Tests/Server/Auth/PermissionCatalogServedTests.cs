using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-1806: the permission catalog is CLOSED, so a role bundle is built by choosing
/// from it. The studio's forms render from the capabilities descriptor, so that is where
/// the catalog is served — a second endpoint would be a second copy of a closed list.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class PermissionCatalogServedTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    [Fact]
    public async Task Capabilities_TheDescriptor_CarriesTheClosedPermissionCatalog()
    {
        var capabilities = await Capabilities();

        capabilities.Permissions.Should().BeEquivalentTo(Permissions.All,
            "the form offers the catalog the server enforces, not a copy of it");
        capabilities.BuiltInRoles.Should().BeEquivalentTo(["admin", "operator", "reader"]);
    }

    private async Task<ConfigCapabilities> Capabilities()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/config/capabilities");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(AuthorityFixture.Audience, ["config.read"]));
        var response = await fixture.Server.Client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<ConfigCapabilities>())!;
    }
}

/// <summary>
/// 2026-08-25-1806: the store read behind role resolution. It is asked per request, and a
/// store that cannot answer must hand back nothing rather than throw into the authorization
/// path — the caller then falls back to the mapping the installation booted with.
/// </summary>
public sealed class StoredRoleMappingTests
{
    [Fact]
    public void Read_TheStoreAnswers_ReturnsTheStoredMapping()
    {
        var mapping = new AgentSmith.Contracts.Models.Configuration.RoleMappingConfig
        {
            RoleClaim = "app_roles",
        };
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSetting("role_mapping")).Returns(mapping);

        Reader(store.Object).Read().Should().BeSameAs(mapping);
    }

    [Fact]
    public void Read_TheStoreThrows_ReturnsNothingInsteadOfFailingTheRequest()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSetting(It.IsAny<string>())).Throws(new InvalidOperationException("db down"));

        Reader(store.Object).Read().Should().BeNull();
    }

    private static StoredRoleMapping Reader(IConfigStore store) =>
        new(store, NullLogger<StoredRoleMapping>.Instance);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Archive;

/// <summary>
/// 2026-08-28-3793: an archive is not a configuration read, and this is where that stops
/// being an assertion in a comment.
/// <para>
/// The archive carries ticket text, prompts, artifacts and the config store's secrets in
/// clear, so it states <c>archive.export</c> / <c>archive.import</c> of its own. The
/// load-bearing case is therefore a caller who holds the ENTIRE configuration grant —
/// read, export and the secrets that go with it — and is still refused, because that
/// bundle is what a route reusing the config permission would have let through.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ArchivePermissionTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string ExportRoute = "/api/archive/export";
    private const string PreviewRoute = "/api/archive/preview";
    private const string ImportRoute = "/api/archive/import";

    [Fact]
    public async Task Export_WithoutThePermission_IsRefused()
    {
        var response = await SendAsync(
            HttpMethod.Get, ExportRoute,
            Permissions.ConfigRead, Permissions.ConfigExport, Permissions.SecretsRead);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Missing(response)).Should().Equal(Permissions.ArchiveExport);
    }

    [Fact]
    public async Task Export_WithNoTokenAtAll_IsRefused() =>
        (await fixture.Server.Client.GetAsync(ExportRoute))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Preview_WithoutThePermission_IsRefused()
    {
        var response = await SendAsync(HttpMethod.Get, PreviewRoute, Permissions.RunsRead);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Missing(response)).Should().Equal(Permissions.ArchiveExport);
    }

    [Fact]
    public async Task Import_WithTheExportPermissionOnly_IsRefused()
    {
        // Taking a copy and replacing the database are separable acts, which is the whole
        // reason there are two names rather than one.
        var response = await SendAsync(HttpMethod.Post, ImportRoute, Permissions.ArchiveExport);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Missing(response)).Should().Equal(Permissions.ArchiveImport);
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, params string[] permissions)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(AuthorityFixture.Audience, permissions));
        return fixture.Server.Client.SendAsync(request);
    }

    private static async Task<IReadOnlyList<string>> Missing(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ForbiddenPermissionResponse>())!.MissingPermissions;
}

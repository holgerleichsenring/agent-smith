using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: the approval preview exposes the open proposal, so it carries dialog.write — proven on a
/// booted server with enforcement on. The route answers no 403 of its own, so the refusal is the declaration's.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ApprovalPreviewPermissionTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string Route = "/api/tickets/alpha/42/approval-preview";

    [Fact]
    public async Task ApprovalPreview_WithoutDialogWrite_Forbidden()
    {
        var refused = await SendAsync(Permissions.RunsRead);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<ForbiddenPermissionResponse>())!.MissingPermissions
            .Should().Equal(Permissions.DialogWrite);
        (await SendAsync(Permissions.DialogWrite)).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private Task<HttpResponseMessage> SendAsync(params string[] permissions)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(AuthorityFixture.Audience, permissions));
        return fixture.Server.Client.SendAsync(request);
    }
}

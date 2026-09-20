using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-18-7a05: the delete carries the dialog surface's own write permission — the shape
/// every destructive route but the run delete takes — on a booted server with enforcement on.
/// <para>
/// This is also why the route's own terminal answer is 204 rather than 403. The authorization
/// layer answers 403 for a missing permission; a route that answered 403 itself would satisfy
/// this test with <c>.Needs(...)</c> taken off it, and the permission would then be a comment.
/// The second half is the contrast that makes the first half mean something: holding the
/// permission, the same request is no longer forbidden — it reaches the route. What the route
/// then ANSWERS is pinned over the real durable store in
/// <see cref="DialogConversationDeleteTests"/>; this boot applies no migrations, so it cannot
/// answer a delete here, only prove that authorization stopped letting it through.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class DialogConversationDeletePermissionTests(EnforcingAuthorityFixture fixture)
    : IClassFixture<EnforcingAuthorityFixture>
{
    private const string Route = "/api/spec-dialog/conversations/s-7a05";

    [Fact]
    public async Task Delete_WithoutTheDialogPermission_IsForbidden()
    {
        var refused = await SendAsync(Permissions.RunsRead, Permissions.RunsDelete);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Missing(refused)).Should().Equal(Permissions.DialogWrite);

        var held = await SendAsync(Permissions.DialogWrite);

        held.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "the refusal above is the permission layer's — the route answers no 403 of its "
            + "own, so the declaration is what the first half tested");
    }

    [Fact]
    public async Task Delete_WithNoTokenAtAll_IsRefused() =>
        (await fixture.Server.Client.DeleteAsync(Route))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    private Task<HttpResponseMessage> SendAsync(params string[] permissions)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, Route);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", fixture.Issuer.Token(AuthorityFixture.Audience, permissions));
        return fixture.Server.Client.SendAsync(request);
    }

    private static async Task<IReadOnlyList<string>> Missing(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ForbiddenPermissionResponse>())!.MissingPermissions;
}

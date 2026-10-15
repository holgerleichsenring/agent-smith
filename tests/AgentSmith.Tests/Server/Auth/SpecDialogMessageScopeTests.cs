using System.Net;
using System.Net.Http.Json;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static AgentSmith.Tests.Server.Auth.ProjectScopeAuthorityFixture;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: the routes that carry their conversation in the body. A caller bound to alpha cannot
/// post into, open with "/spec", or resume onto a conversation of beta, and a bare "/spec" — which would
/// list every configured project — is refused them; each refusal is an empty 404 before anything runs.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class SpecDialogMessageScopeTests(ProjectScopeAuthorityFixture fixture)
    : IClassFixture<ProjectScopeAuthorityFixture>
{
    [Fact]
    public async Task SpecDialogMessage_DialogInAnotherProject_Is404AndNothingIsDispatched()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-msg-beta", "d-msg-beta", "beta", ticketKey: "B-2"));
        var before = fixture.Dispatched;

        var response = await PostAsync("d-msg-beta", "please widen the table");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        fixture.Dispatched.Should().Be(before);
    }

    [Fact]
    public async Task SpecDialogMessage_SpecCommandNamingAnotherProject_Is404AndOpensNoSession()
    {
        var before = fixture.Dispatched;

        var response = await PostAsync("d-msg-new", "/spec beta");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fixture.Dispatched.Should().Be(before);
        (await SessionsOnAsync("d-msg-new")).Should().Be(0);
    }

    [Fact]
    public async Task SpecDialogMessage_BareSpecCommand_ForAProjectOnlyCaller_Is404AndListsNoProject()
    {
        var before = fixture.Dispatched;

        var response = await PostAsync("d-msg-bare", "/spec");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        fixture.Dispatched.Should().Be(before, "nothing was routed that could answer with the project list");
    }

    [Fact]
    public async Task SpecDialogMessage_MessageInTheBoundProject_IsTaken()
    {
        var response = await PostAsync("d-msg-alpha", "a widget", project: "alpha");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task SpecDialogResume_OntoATicketBoundDialogOfAnotherProject_Is404AndClosesNothing()
    {
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(
            db, "s-res-alpha", "d-res-old", "alpha", owner: AlphaTeam + "-person"));
        await fixture.SeedAsync(db => ProjectScopeRows.SessionAsync(db, "s-res-beta", "d-res-beta", "beta", ticketKey: "B-3"));

        var response = await fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, HttpMethod.Post,
            "/api/spec-dialog/conversations/s-res-alpha/resume", JsonContent.Create(new { dialogId = "d-res-beta" })));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        (await SessionsOnAsync("d-res-beta", openOnly: true)).Should().Be(1, "beta's conversation is still open");
    }

    private Task<HttpResponseMessage> PostAsync(string dialogId, string text, string? project = null) =>
        fixture.Server.Client.SendAsync(fixture.As(AlphaTeam, HttpMethod.Post, "/api/spec-dialog/messages",
            JsonContent.Create(new { dialogId, text, project })));

    private async Task<int> SessionsOnAsync(string threadId, bool openOnly = false)
    {
        var count = 0;
        await fixture.SeedAsync(async db => count = await db.Set<SpecDialogSession>().AsNoTracking()
            .CountAsync(s => s.ThreadId == threadId && (!openOnly || s.IsOpen)));
        return count;
    }
}

using System.Security.Claims;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>
/// 2026-10-13-8762s: a person ratifies one mock version of a spec. Not found unless the conversation is
/// reachable and holds that version; refused only while the turn computes; otherwise one typed user turn,
/// saved so a bound ticket's page refreshes. The newest ratification of a spec wins.
/// </summary>
public sealed class MockRatificationTests : IDisposable
{
    private readonly RatificationRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task MockRatify_UnknownVersion_Is404NamingIt()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);

        var result = await RouteAsync(session, "spec-a", 2);

        var text = result.Should().BeOfType<ContentHttpResult>().Subject;
        text.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        text.ResponseContent.Should().Be("Mock v2 of spec-a is not held in this conversation.");
    }

    [Fact]
    public async Task MockRatify_UnreachableConversation_Is404()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);

        var stranger = await RouteAsync(session, "spec-a", 1, "person-b");

        stranger.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await _rig.TranscriptAsync()).Should().BeEmpty("nothing is appended for a caller who cannot reach it");
    }

    [Fact]
    public async Task MockRatify_WhileTurnComputes_Is409()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        _rig.Gate.TryEnter(session).Should().BeTrue();
        _rig.Gate.Begin(session);

        var result = await RouteAsync(session, "spec-a", 1);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        (await _rig.TranscriptAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task MockRatify_DuringApprovalWait_IsAccepted()
    {
        var session = await _rig.OpenAsync();
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        _rig.Gate.TryEnter(session).Should().BeTrue("the turn stays entered across the approval wait");
        _rig.Gate.Finish(session, _rig.Gate.Begin(session));

        var result = await RouteAsync(session, "spec-a", 1);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status200OK);
        _rig.Gate.TryEnter(session).Should().BeFalse("the ratification never takes or releases the gate");
    }

    [Fact]
    public async Task MockRatify_AppendsOneTypedTurn_AndNudgesTheBoundTicket()
    {
        var binding = TicketBinding.For("gh", "github", "42", "Widget");
        var session = await _rig.OpenAsync(binding);
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        await _rig.Mocks.AddAsync(session, "spec-a", "<p>2</p>", default);

        var result = await _rig.RatifyAsync(session, "spec-a", 2);

        result.Status.Should().Be(StatusCodes.Status200OK);
        var turn = (await _rig.TranscriptAsync()).Should().ContainSingle().Subject;
        turn.Role.Should().Be(TranscriptRole.User);
        turn.Text.Should().Be("Ratified mock v2 of spec-a.");
        turn.Kind.Should().Be(SpecDialogTurnKind.MockRatified);
        turn.Mock.Should().Be(new MockRatificationTurn("spec-a", 2, RatificationRig.Owner));
        _rig.Activity.Verify(a => a.ChangedAsync("sample", binding.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RatifiedMocks_LaterRatification_Supersedes()
    {
        TranscriptTurn Ratified(string spec, int version) => new(TranscriptRole.User, "r", DateTimeOffset.UnixEpoch,
            SpecDialogTurnKind.MockRatified, Mock: new MockRatificationTurn(spec, version, "p"));

        var ratified = RatifiedMocks.Of([Ratified("spec-a", 2), Ratified("spec-b", 1), Ratified("spec-a", 3),
            new TranscriptTurn(TranscriptRole.User, "v1 please", DateTimeOffset.UnixEpoch)]);

        ratified.Should().BeEquivalentTo(new Dictionary<string, int> { ["spec-a"] = 3, ["spec-b"] = 1 });
    }

    private async Task<IResult> RouteAsync(string session, string specId, int version, string owner = RatificationRig.Owner)
    {
        using var scope = _rig.Provider.CreateScope();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", owner)], "test")) };
        return await SpecDialogMockEndpoints.RatifyAsync(http, session, specId, new MockRatifyRequest(version),
            scope.ServiceProvider.GetRequiredService<SpecDialogOwnership>(),
            scope.ServiceProvider.GetRequiredService<MockRatification>(), CancellationToken.None);
    }
}

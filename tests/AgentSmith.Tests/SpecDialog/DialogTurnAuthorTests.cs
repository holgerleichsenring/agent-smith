using System.Security.Claims;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-14-da5eb: a person's turn names who wrote it — the token's display name, never its
/// subject — and the model reads that name in its transcript.
/// </summary>
public sealed class DialogTurnAuthorTests
{
    [Theory]
    [InlineData("name", "Ada Lovelace")]
    [InlineData("preferred_username", "ada@example.org")]
    [InlineData("upn", "ada.upn@example.org")]
    public void DisplayName_FromTheFirstDisplayClaim_IsTheAuthor(string claim, string value)
    {
        var caller = Caller(("sub", "0f9c"), (claim, value));

        DisplayName.Of(caller).Should().Be(value);
    }

    [Fact]
    public void DisplayName_NamePreferredOverUsernameAndUpn()
    {
        var caller = Caller(("upn", "u"), ("preferred_username", "p"), ("name", "Ada Lovelace"));

        DisplayName.Of(caller).Should().Be("Ada Lovelace");
    }

    [Fact]
    public void DisplayName_SubOnlyToken_IsNone()
    {
        DisplayName.Of(Caller(("sub", "0f9c"), ("name", "  "))).Should().BeNull();
    }

    [Fact]
    public void TurnSeeds_PersonTurnWithAuthor_CarriesItToTheModel()
    {
        var at = DateTimeOffset.UnixEpoch;
        var state = new ConversationState
        {
            JobId = "s-1", Project = "p", ChannelId = "d-1", UserId = "person-a",
            Platform = "dashboard", TicketId = string.Empty, StartedAt = at,
            Transcript = [new TranscriptTurn(TranscriptRole.User, "faster?", at, Author: "Ada"),
                new TranscriptTurn(TranscriptRole.User, "older turn", at)],
        };

        var seeds = SpecDialogTurnSeeds.Build(
            state, [new RepoConnection { Name = "sample-api" }], new Dictionary<string, ISandbox>(),
            new SpecDialogReplySlot(), DialogImageSet.None, Mock.Of<Contracts.Dialogue.IFiledTicketWithdrawal>());

        ((IReadOnlyList<SpecDialogTurn>)seeds[ContextKeys.SpecDialogTranscript])
            .Select(t => t.Author).Should().Equal("Ada", null);
    }

    [Fact]
    public void SpecDialogPrompt_TranscriptLine_CarriesTheAuthor()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn("user", "can it be faster?", Author: "Ada Lovelace"),
            new SpecDialogTurn("assistant", "yes", SpecDialogTurnKind.Answer),
            new SpecDialogTurn("user", "then do it"),
        ]);

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("[user: Ada Lovelace] can it be faster?")
            .And.Contain("[assistant] yes").And.Contain("[user] then do it");
    }

    private static ClaimsPrincipal Caller(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));
}

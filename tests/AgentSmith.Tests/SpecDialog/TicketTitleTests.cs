using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042eb: the schema allows a 2000-character goal and the trackers cap a title near
/// 255, so every title the framework files is cut where a tracker accepts it.
/// </summary>
public sealed class TicketTitleTests
{
    private static readonly string LongGoal = string.Join(' ', Enumerable.Repeat("widget", 80));

    [Fact]
    public void TicketTitle_LongGoal_IsCutAtAWordBoundaryAndTheBodyKeepsTheGoal()
    {
        var content = new PhaseTicketRenderer().RenderChildRequirement(
            new PhaseDraft("p9000a", LongGoal, $"phase: p9000a\ngoal: {LongGoal}", []), new HashSet<string>());

        content.Title.Length.Should().BeLessThanOrEqualTo(TicketTitle.MaxLength);
        content.Title.Should().StartWith("p9000a: widget").And.EndWith("widget…",
            "the cut falls between words and says it cut");
        content.Body.Should().Contain(LongGoal, "the goal is whole in the body");
    }

    [Fact]
    public void TicketTitle_ShortTitle_IsUnchanged() =>
        TicketTitle.Fit("p9000a: Widget storage layer").Should().Be("p9000a: Widget storage layer");

    [Fact]
    public void TicketTitle_Exactly255_IsUnchanged_And256_IsCut()
    {
        var exactly = new string('a', 250) + " bcde";
        TicketTitle.Fit(exactly).Should().Be(exactly);

        var over = exactly + "f";
        TicketTitle.Fit(over).Should().HaveLength(251).And.EndWith("a…",
            "the space sits in the last 40%, so the cut falls there");
    }

    [Fact]
    public void TicketTitle_NoSpaces_IsHardCutToTheLimit() =>
        TicketTitle.Fit(new string('x', 300)).Should().HaveLength(TicketTitle.MaxLength).And.EndWith("x…");

    [Fact]
    public void TicketTitle_EarlyBoundaryOnly_IsHardCutNotCutToTheId()
    {
        var title = TicketTitle.Fit("p9000a: https://example.test/" + new string('q', 300));

        title.Should().HaveLength(TicketTitle.MaxLength, "cutting at the only space would leave 'p9000a:…'");
    }

    [Fact]
    public void TicketTitle_SurrogatePairAtTheCut_IsNotSplit()
    {
        var title = TicketTitle.Fit(new string('x', 253) + "\U0001F600" + new string('y', 20));

        title.Should().Be(new string('x', 253) + "…", "half an emoji is not text any tracker stores");
    }

    [Fact]
    public void TicketTitle_NewlineInAnIntentTitle_BecomesASpace() =>
        TicketTitle.Fit("Add logging\r\nto the widget store").Should().Be("Add logging to the widget store");

    [Fact]
    public async Task BugFiling_LongTitle_IsCut()
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));

        var report = await Filer(provider.Object).FileAsync(
            State(), new BugOutcome(new BugTicketDraft(LongGoal, "The widget is lost.", null)), CancellationToken.None);

        report.Error.Should().BeNull("a title over the tracker's limit no longer fails the create");
        provider.Verify(p => p.CreateAsync(
            It.Is<string>(t => t.Length <= TicketTitle.MaxLength && t.EndsWith('…')),
            It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task CreateTicketIntent_LongTitle_IsCut()
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config());
        var handler = new CreateTicketIntentHandler(
            Mock.Of<IPlatformAdapter>(), loader.Object, factory.Object, NullLogger<CreateTicketIntentHandler>.Instance);

        await handler.HandleAsync(new CreateTicketIntent
        {
            RawText = "create", UserId = "U1", ChannelId = "C1", Platform = "slack", Project = "proj", Title = LongGoal,
        }, CancellationToken.None);

        provider.Verify(p => p.CreateAsync(
            It.Is<string>(t => t.Length <= TicketTitle.MaxLength), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()));
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["proj"] = new() { Name = "proj", Tracker = new TrackerConnection() },
        },
    };

    private static OutcomeTicketFiler Filer(ITicketProvider provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        return new OutcomeTicketFiler(
            Config(), factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            TestSupport.ApprovedSetDoubles.EpicFiler(),
            TestSupport.ApprovedSetDoubles.Recorder(),
            NullLogger<OutcomeTicketFiler>.Instance);
    }

    private static ConversationState State() => new()
    {
        JobId = "job-1", ChannelId = "C1", UserId = "U1", Platform = "slack",
        Project = "proj", TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
    };
}

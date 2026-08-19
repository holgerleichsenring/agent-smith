using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets.Expectations;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0461: the comment says where answering works, and the board stops saying "waiting for
/// you" once the run is working again.
/// </summary>
public sealed class AWaitingTicketPointsAtTheRunTests
{
    private const string RunId = "2026-08-19T10-00-00-48ca";
    private const string Dashboard = "https://agentsmith.example.com";
    private static readonly TrackerConnection AzureDevOps =
        new() { Name = "t", Type = TrackerType.AzureDevOps };

    [Fact]
    public async Task TheOpenQuestionsComment_LinksToWhereAnsweringWorks()
    {
        var provider = new Mock<ITicketProvider>();

        await Poster(Dashboard, provider).PostAsync(
            Pipeline(), AzureDevOps, Ticket(), [new PlanOpenQuestion("1", "Which module?", [])],
            "Needs Clarification", CancellationToken.None);

        Body(provider).Should().Contain($"{Dashboard}/jobs/{RunId}",
            "the comment invited a reply and named no other way to answer");
    }

    [Fact]
    public async Task WithNoConfiguredDashboard_TheCommentCarriesNoLink()
    {
        var provider = new Mock<ITicketProvider>();

        await Poster(baseUrl: null, provider).PostAsync(
            Pipeline(), AzureDevOps, Ticket(), [new PlanOpenQuestion("1", "Which module?", [])],
            "Needs Clarification", CancellationToken.None);

        Body(provider).Should().NotContain("/jobs/",
            "a guessed address printed into someone's work item is a broken link");
    }

    [Fact]
    public async Task TheExpectationComment_LinksToWhereRatifyingWorks()
    {
        var provider = new Mock<ITicketProvider>();
        var sut = new ExpectationTrackerCommenter(
            Templates(), Factory(provider), new RunAnswerLink(Config(Dashboard)),
            NullLogger<ExpectationTrackerCommenter>.Instance);

        await sut.PostAsync(Pipeline(), AzureDevOps, Ticket(), Draft(), CancellationToken.None);

        Comment(provider).Should().Contain($"{Dashboard}/jobs/{RunId}",
            "it said 'ratify it on the run's dashboard prompt' without saying where that was");
    }

    [Fact]
    public async Task AResumedRun_MovesItsTicketOffTheClarificationStatus()
    {
        var (factory, ticket) = ParkedTicketFixture.Provider();
        var logger = new CapturingLogger();
        var sut = new ParkedTicketDialogue(
            ParkedTicketFixture.Loader(inProgressStatus: "Active"), ParkedTicketFixture.Context,
            factory, new UnusedInbox(), logger);

        await sut.MoveToInProgressAsync(Checkpoint(), CancellationToken.None);

        ticket.Transitions.Should().ContainSingle().Which.Should().Be("Active",
            "the run is working again, so the board must stop saying it waits for a person");
    }

    [Fact]
    public async Task WithNoInProgressStatus_TheTicketIsLeftAloneAndSaidSo()
    {
        var (factory, ticket) = ParkedTicketFixture.Provider();
        var logger = new CapturingLogger();
        var sut = new ParkedTicketDialogue(
            ParkedTicketFixture.Loader(), ParkedTicketFixture.Context, factory,
            new UnusedInbox(), logger);

        await sut.MoveToInProgressAsync(Checkpoint(), CancellationToken.None);

        ticket.Transitions.Should().BeEmpty("nothing is invented when nothing is configured");
        logger.Lines.Should().ContainSingle(l => l.Contains("in_progress_status"),
            "a silent no-op is how a stale board goes unnoticed");
    }

    private static PlanOpenQuestionsPoster Poster(string? baseUrl, Mock<ITicketProvider> provider) =>
        new(Templates(), Factory(provider), new RunAnswerLink(Config(baseUrl)),
            NullLogger<PlanOpenQuestionsPoster>.Instance);

    private static AgentSmithConfig Config(string? baseUrl) =>
        new() { Dialogue = new DialogueGlobalConfig { DashboardUrl = baseUrl } };

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        return pipeline;
    }

    private static ServiceProvider Templates()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITicketCommentTemplate,
            AzureDevOpsOpenQuestionsCommentTemplate>("azuredevops");
        services.AddKeyedSingleton<IExpectationCommentTemplate,
            MarkdownExpectationCommentTemplate>("azuredevops");
        return services.BuildServiceProvider();
    }

    private static ITicketProviderFactory Factory(Mock<ITicketProvider> provider)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return factory.Object;
    }

    private static string Body(Mock<ITicketProvider> provider)
    {
        var captured = string.Empty;
        provider.Verify(p => p.FinalizeAsync(
            It.IsAny<TicketId>(), It.Is<string>(b => Capture(b, out captured)),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        return captured;
    }

    private static string Comment(Mock<ITicketProvider> provider)
    {
        var captured = string.Empty;
        provider.Verify(p => p.UpdateStatusAsync(
            It.IsAny<TicketId>(), It.Is<string>(b => Capture(b, out captured)),
            It.IsAny<CancellationToken>()), Times.Once);
        return captured;
    }

    private static bool Capture(string body, out string captured)
    {
        captured = body;
        return true;
    }

    private static Contracts.Models.RunCheckpointRecord Checkpoint() =>
        ParkedTicketFixture.Checkpoint(DateTimeOffset.Parse("2026-08-19T10:00:00Z"));

    private static ExpectationDraft Draft() =>
        new("Rename the call sites", ["Every call site is renamed."], [], null);

    private static Ticket Ticket() =>
        new(new TicketId("19213"), "T", "D", null, "Active", "AzureDevOps", null,
            new TicketPerson("Jane Operator", "3f7c1a2e-9b44-4d0e-8f21-6c5a0d9e1b73"));

    private sealed class CapturingLogger : ILogger<ParkedTicketDialogue>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    /// <summary>The status move never touches the inbox — saying so is cheaper than a stub that lies.</summary>
    private sealed class UnusedInbox : IDialogueAnswerInbox
    {
        public Task<bool> TryDeliverAsync(
            string dialogueJobId, Contracts.Dialogue.DialogAnswer answer, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Contracts.Dialogue.DialogAnswer?> GetAsync(
            string dialogueJobId, string questionId, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}

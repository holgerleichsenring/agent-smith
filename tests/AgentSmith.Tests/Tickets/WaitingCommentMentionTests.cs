using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets.Expectations;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// p0454: the three comments that WAIT for a person name that person — proven at the
/// posters, because that is where a ticket's assignee was being dropped, not in the
/// templates that never saw one.
/// </summary>
public sealed class WaitingCommentMentionTests
{
    private const string Guid = "3f7c1a2e-9b44-4d0e-8f21-6c5a0d9e1b73";
    private static readonly TrackerConnection AzureDevOps =
        new() { Name = "t", Type = TrackerType.AzureDevOps };

    [Fact]
    public async Task AParkedTicket_NamesThePersonItWaitsFor()
    {
        var (factory, provider) = Provider();
        var sut = new PlanOpenQuestionsPoster(
            Services(), factory.Object, NoLink(), NullLogger<PlanOpenQuestionsPoster>.Instance);

        await sut.PostAsync(
            new PipelineContext(), AzureDevOps, Assigned(),
            [new PlanOpenQuestion("1", "Which module?", [])],
            "Needs Clarification", CancellationToken.None);

        Body(provider).Should().Contain($"data-vss-mention=\"version:2.0,{Guid}\"");
    }

    [Fact]
    public async Task AParkedTicketWithNobodyOnIt_SaysNobodyWasNotified()
    {
        var (factory, provider) = Provider();
        var sut = new PlanOpenQuestionsPoster(
            Services(), factory.Object, NoLink(), NullLogger<PlanOpenQuestionsPoster>.Instance);

        await sut.PostAsync(
            new PipelineContext(), AzureDevOps, Unassigned(),
            [new PlanOpenQuestion("1", "Which module?", [])],
            "Needs Clarification", CancellationToken.None);

        Body(provider).Should().Contain(TicketMention.NobodyToNotify);
    }

    [Fact]
    public async Task AnExpectationToRatify_NamesThePersonItWaitsFor()
    {
        var (factory, provider) = Provider();
        var sut = new ExpectationTrackerCommenter(
            Services(), factory.Object, NoLink(),
            NullLogger<ExpectationTrackerCommenter>.Instance);

        await sut.PostAsync(
            new PipelineContext(), AzureDevOps, Assigned(), Draft(), CancellationToken.None);

        Comment(provider).Should().Contain($"data-vss-mention=\"version:2.0,{Guid}\"");
    }

    [Fact]
    public void AHandbackVerdict_NamesThePersonItWaitsFor()
    {
        var waiting = TicketMention.WaitingLine(TrackerType.AzureDevOps, Assigned());

        var body = SpecHandbackComment.Build(
            new SpecHandback(SpecHandbackCase.NotImplementable, "the API does not exist"),
            null, waiting);

        body.Should().Contain($"data-vss-mention=\"version:2.0,{Guid}\"");
    }

    // p0461: these cases are about the MENTION; an unconfigured dashboard renders no link.
    private static RunAnswerLink NoLink() => new(AgentSmithConfig.Empty());

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITicketCommentTemplate,
            AzureDevOpsOpenQuestionsCommentTemplate>("azuredevops");
        services.AddKeyedSingleton<IExpectationCommentTemplate,
            MarkdownExpectationCommentTemplate>("azuredevops");
        return services.BuildServiceProvider();
    }

    private static (Mock<ITicketProviderFactory>, Mock<ITicketProvider>) Provider()
    {
        var provider = new Mock<ITicketProvider>();
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return (factory, provider);
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

    private static ExpectationDraft Draft() =>
        new("Rename the call sites", ["Every call site is renamed."], [], null);

    private static Ticket Assigned() =>
        Ticket(new TicketPerson("Jane Operator", Guid));

    private static Ticket Unassigned() => Ticket(null);

    private static Ticket Ticket(TicketPerson? assignee) =>
        new(new TicketId("19213"), "T", "D", null, "Active", "AzureDevOps", null, assignee);
}

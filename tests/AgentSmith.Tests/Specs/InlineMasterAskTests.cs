using AgentSmith.Application;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-02-2b26e: the master's mid-run question on an inline-ticket run is asked in the terminal — the run has
/// no ticket to park on — and the master is re-engaged with the answer in its input.
/// </summary>
public sealed class InlineMasterAskTests
{
    private const string Question = "Should the boundary include exactly 100.00?";
    private readonly RecordingPoster _poster = new();

    [Fact]
    public async Task InlineRun_AsksAndReengagesWithTheAnswer()
    {
        var context = Context();

        var result = await Handler(Answering("yes, include it")).ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.InsertNext!.Select(c => c.Name).Should().Equal(CommandNames.AgenticMaster, CommandNames.MasterOpenQuestions);
        result.InsertNext.Should().OnlyContain(c => c.SpecId == "s1");
        result.Message.Should().Contain("yes, include it").And.NotContain("needs_clarification_status");
        _poster.Posted.Should().Be(0, "an inline run has no ticket to post on");
    }

    [Fact]
    public async Task Answer_ReachesPlanAnswers_AndClearsTheQuestion()
    {
        var context = Context();
        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);

        await Handler(Answering("yes, include it")).ExecuteAsync(context, default);

        Answers(context).Should().Equal(new Dictionary<string, string> { ["1"] = $"{Question} — yes, include it" });
        context.Pipeline.Has(ContextKeys.MasterOpenQuestions).Should().BeFalse();
        context.Pipeline.Has(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeFalse();
    }

    [Fact]
    public async Task SplicedSecondStep_FindsNoQuestion()
    {
        var context = Context();
        var handler = Handler(Answering("yes"));
        await handler.ExecuteAsync(context, default);

        var second = await handler.ExecuteAsync(context, default);

        second.Message.Should().Be("Master asked no mid-run question");
    }

    [Fact]
    public async Task EofInput_GivesTheNamedDefault_NotEmpty()
    {
        var context = Context();
        context.Pipeline.Set(ContextKeys.RunId, "run-eof");
        var gate = new DialogueAskGate(
            new ConsoleDialogueTransport(new StringReader(string.Empty), new StringWriter(),
                NullLogger<ConsoleDialogueTransport>.Instance),
            Mock.Of<IDialogueTrail>(), Mock.Of<IDialogueCheckpointWriter>(), new DialogueJobIdentity(),
            NullLogger<DialogueAskGate>.Instance);

        var result = await Handler(gate).ExecuteAsync(context, default).OrHang("the answer at end of input");

        result.IsSuccess.Should().BeTrue(result.Message);
        Answers(context)["1"].Should().Be($"{Question} — {InlineMasterAsk.NoAnswer}");
    }

    [Fact]
    public async Task AskedAgainAfterDefault_ReengagesOnceThenFails_AndClearsTheQuestion()
    {
        var handler = Handler(Answering(InlineMasterAsk.NoAnswer, by: "system"));
        var context = Context();
        (await handler.ExecuteAsync(context, default)).InsertNext.Should().NotBeNull("the first default re-engages");
        Ask(context);

        var second = await handler.ExecuteAsync(context, default);

        second.IsSuccess.Should().BeFalse();
        second.Message.Should().Contain(Question);
        context.Pipeline.Has(ContextKeys.MasterOpenQuestions).Should().BeFalse();
    }

    [Fact]
    public async Task TwoAsks_BothQuestionAnswerPairsInPlanAnswers()
    {
        var context = Context();
        await Handler(Answering("yes")).ExecuteAsync(context, default);
        Ask(context, "Should the test cover 99.99 too?");

        await Handler(Answering("no")).ExecuteAsync(context, default);

        Answers(context).Should().Equal(new Dictionary<string, string>
        {
            ["1"] = $"{Question} — yes",
            ["2"] = "Should the test cover 99.99 too? — no",
        });
    }

    [Fact]
    public async Task BlankChoiceAnswer_TakesTheDefault()
    {
        var context = Context(options: ["include", "exclude"]);

        await Handler(Answering(string.Empty)).ExecuteAsync(context, default);

        Answers(context)["1"].Should().Be($"{Question} — {InlineMasterAsk.NoAnswer}");
    }

    [Fact]
    public void Handler_ResolvesFromTheRealProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddInProcessSandbox();
        services.AddSingleton(Mock.Of<IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICommandHandler<MasterOpenQuestionsContext>>().Should().NotBeNull();
        provider.GetRequiredService<InlineMasterAsk>().Should().NotBeNull();
    }

    private static Dictionary<string, string> Answers(MasterOpenQuestionsContext context) =>
        context.Pipeline.Get<Dictionary<string, string>>(ContextKeys.PlanAnswers);

    private static void Ask(MasterOpenQuestionsContext context, string question = Question) =>
        context.Pipeline.Set<IReadOnlyList<PlanOpenQuestion>>(
            ContextKeys.MasterOpenQuestions, [new PlanOpenQuestion("1", question, [])]);

    private static MasterOpenQuestionsContext Context(IReadOnlyList<string>? options = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.InlineTicket, new InlineTicket("Fix the boundary", "Totals of 100.00 get it."));
        pipeline.Set<IReadOnlyList<PlanOpenQuestion>>(
            ContextKeys.MasterOpenQuestions, [new PlanOpenQuestion("1", Question, options ?? [])]);
        return new MasterOpenQuestionsContext(
            new InlineTicket("Fix the boundary", "Totals of 100.00 get it.").ToTicket(),
            new TrackerConnection { Type = TrackerType.GitHub }, pipeline,
            new PipelineCommand(CommandNames.MasterOpenQuestions) { SpecId = "s1" });
    }

    private static IDialogueAskGate Answering(string answer, string by = "console-user")
    {
        var gate = new Mock<IDialogueAskGate>();
        gate.Setup(g => g.AskAsync(It.IsAny<PipelineContext>(), It.IsAny<DialogQuestion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineContext _, DialogQuestion q, CancellationToken _) =>
                DialogueAskOutcome.Answered(new DialogAnswer(q.QuestionId, answer, null, DateTimeOffset.UtcNow, by)));
        return gate.Object;
    }

    private MasterOpenQuestionsHandler Handler(IDialogueAskGate gate)
    {
        var intake = new MasterAnswerIntake(Mock.Of<IDialogueTrail>(), NullLogger<MasterAnswerIntake>.Instance);
        return new MasterOpenQuestionsHandler(_poster, new ClarificationParkStatusResolver(),
            new MasterQuestionCheckpoint(Mock.Of<IDialogueCheckpointWriter>(), new DialogueJobIdentity(),
                NullLogger<MasterQuestionCheckpoint>.Instance),
            intake, new InlineMasterAsk(gate, intake, NullLogger<InlineMasterAsk>.Instance),
            new Application.Services.Lifecycle.UnmovedTicketReport(
                new Application.Services.Persistence.InMemoryUnmovedTicketStore(),
                NullLogger<Application.Services.Lifecycle.UnmovedTicketReport>.Instance),
            NullLogger<MasterOpenQuestionsHandler>.Instance);
    }

    private sealed class RecordingPoster : IPlanOpenQuestionsPoster
    {
        public int Posted { get; private set; }

        public Task<TicketFinalizeResult> PostAsync(
            PipelineContext pipeline, TrackerConnection ticketConfig, Ticket ticket,
            IReadOnlyList<PlanOpenQuestion> questions, string? parkStatus, CancellationToken ct)
        {
            Posted++;
            return Task.FromResult(TicketFinalizeResult.Moved());
        }
    }
}

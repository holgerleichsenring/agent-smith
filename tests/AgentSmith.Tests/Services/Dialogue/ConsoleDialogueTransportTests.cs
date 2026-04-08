using AgentSmith.Contracts.Dialogue;
using AgentSmith.Host.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Dialogue;

public sealed class ConsoleDialogueTransportTests
{
    private static ConsoleDialogueTransport CreateSut(string inputText, out StringWriter outputWriter)
    {
        var reader = new StringReader(inputText);
        outputWriter = new StringWriter();
        return new ConsoleDialogueTransport(reader, outputWriter, NullLogger<ConsoleDialogueTransport>.Instance);
    }

    private static DialogQuestion MakeQuestion(
        QuestionType type, string text = "Test?", string? context = null,
        IReadOnlyList<string>? choices = null, string? defaultAnswer = null,
        TimeSpan? timeout = null) =>
        new("q-1", type, text, context, choices, defaultAnswer, timeout ?? TimeSpan.FromMinutes(1));

    [Fact]
    public async Task InfoType_ReturnsNull_WithoutWaitingForInput()
    {
        var sut = CreateSut("ignored input", out var output);
        var question = MakeQuestion(QuestionType.Info, "Build completed successfully.");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().BeNull();
        output.ToString().Should().Contain("Build completed successfully.");
    }

    [Fact]
    public async Task Confirmation_DefaultYes_EmptyInput_ReturnsYes()
    {
        var sut = CreateSut("\n", out _);
        var question = MakeQuestion(QuestionType.Confirmation, "Continue?", defaultAnswer: "yes");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer.Should().Be("yes");
        result.AnsweredBy.Should().Be("console-user");
    }

    [Fact]
    public async Task Confirmation_InputNo_ReturnsNo()
    {
        var sut = CreateSut("n\n", out _);
        var question = MakeQuestion(QuestionType.Confirmation, "Continue?", defaultAnswer: "yes");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer.Should().Be("no");
    }

    [Fact]
    public async Task Choice_ValidNumber_ReturnsChoiceText()
    {
        var sut = CreateSut("2\n", out var output);
        var question = MakeQuestion(QuestionType.Choice, "Pick one", choices: ["Alpha", "Beta", "Gamma"]);

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer.Should().Be("Beta");

        var rendered = output.ToString();
        rendered.Should().Contain("1. Alpha");
        rendered.Should().Contain("2. Beta");
        rendered.Should().Contain("3. Gamma");
    }

    [Fact]
    public async Task FreeText_ReturnsRawInput()
    {
        var sut = CreateSut("my custom answer\n", out _);
        var question = MakeQuestion(QuestionType.FreeText, "Describe the issue");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer.Should().Be("my custom answer");
    }

    [Fact]
    public async Task Approval_InputA_ReturnsApprove()
    {
        var sut = CreateSut("a\n", out _);
        var question = MakeQuestion(QuestionType.Approval, "Approve these changes?");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer.Should().Be("approve");
    }

    [Fact]
    public async Task Approval_InputR_ReturnsReject()
    {
        var sut = CreateSut("r\n", out _);
        var question = MakeQuestion(QuestionType.Approval, "Approve these changes?");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Answer.Should().Be("reject");
    }

    [Fact]
    public async Task Timeout_ReturnsNull()
    {
        // Use a reader that blocks forever (no data)
        var blockingReader = new BlockingTextReader();
        var output = new StringWriter();
        var sut = new ConsoleDialogueTransport(blockingReader, output, NullLogger<ConsoleDialogueTransport>.Instance);
        var question = MakeQuestion(QuestionType.FreeText, "Will timeout");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        var result = await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromMilliseconds(100), CancellationToken.None);

        result.Should().BeNull();
        output.ToString().Should().Contain("Timed out");
    }

    [Fact]
    public async Task NoPendingQuestion_ReturnsNull()
    {
        var sut = CreateSut("ignored\n", out _);

        // Do NOT call PublishQuestionAsync — go straight to WaitForAnswer
        var result = await sut.WaitForAnswerAsync("job-1", "q-missing", TimeSpan.FromSeconds(1), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task PublishAnswerAsync_IsNoOp()
    {
        var sut = CreateSut("", out _);
        var answer = new DialogAnswer("q-1", "yes", null, DateTimeOffset.UtcNow, "user");

        // Should not throw
        await sut.PublishAnswerAsync("job-1", answer, CancellationToken.None);
    }

    [Fact]
    public async Task Context_IsDisplayedBeforeQuestion()
    {
        var sut = CreateSut("y\n", out var output);
        var question = MakeQuestion(QuestionType.Confirmation, "Continue?", context: "Step 3 of 5", defaultAnswer: "yes");

        await sut.PublishQuestionAsync("job-1", question, CancellationToken.None);
        await sut.WaitForAnswerAsync("job-1", "q-1", TimeSpan.FromSeconds(5), CancellationToken.None);

        var rendered = output.ToString();
        var contextIdx = rendered.IndexOf("Step 3 of 5", StringComparison.Ordinal);
        var questionIdx = rendered.IndexOf("Continue?", StringComparison.Ordinal);
        contextIdx.Should().BeLessThan(questionIdx);
    }

    [Fact]
    public void FormatPrompt_Confirmation_DefaultYes_ShowsCapitalY()
    {
        var question = MakeQuestion(QuestionType.Confirmation, "Continue?", defaultAnswer: "yes");
        var prompt = ConsoleDialogueTransport.FormatPrompt(question);
        prompt.Should().Contain("[Y/n]");
    }

    [Fact]
    public void FormatPrompt_Confirmation_DefaultNo_ShowsCapitalN()
    {
        var question = MakeQuestion(QuestionType.Confirmation, "Continue?", defaultAnswer: "no");
        var prompt = ConsoleDialogueTransport.FormatPrompt(question);
        prompt.Should().Contain("[y/N]");
    }

    [Fact]
    public void FormatPrompt_Approval_ShowsOptions()
    {
        var question = MakeQuestion(QuestionType.Approval, "Approve?");
        var prompt = ConsoleDialogueTransport.FormatPrompt(question);
        prompt.Should().Contain("[A]pprove / [R]eject");
    }

    [Fact]
    public void FormatPrompt_FreeText_ShowsInputMarker()
    {
        var question = MakeQuestion(QuestionType.FreeText, "Enter value");
        var prompt = ConsoleDialogueTransport.FormatPrompt(question);
        prompt.Should().Contain("> ");
    }

    /// <summary>
    /// A TextReader that blocks on ReadLine until disposed/cancelled,
    /// simulating a console waiting for input that never arrives.
    /// </summary>
    private sealed class BlockingTextReader : TextReader
    {
        private readonly SemaphoreSlim _semaphore = new(0);

        public override string? ReadLine()
        {
            _semaphore.Wait(TimeSpan.FromSeconds(30));
            return null;
        }
    }
}

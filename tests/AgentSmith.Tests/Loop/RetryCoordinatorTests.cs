using AgentSmith.Application.Services.Loop;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Loop;

public sealed class RetryCoordinatorTests
{
    private sealed class ScriptedChatClient(Queue<string> responses) : IChatClient
    {
        public int CallCount { get; private set; }
        public List<List<ChatMessage>> CapturedMessages { get; } = new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedMessages.Add(messages.Select(m => new ChatMessage(m.Role, m.Text)).ToList());
            var text = responses.Count > 0 ? responses.Dequeue() : "{}";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class StrictValidator : ISkillOutputValidator
    {
        private readonly Queue<ValidationResult> _results;
        public StrictValidator(params ValidationResult[] results) => _results = new Queue<ValidationResult>(results);
        public ValidationResult Validate(string output) => _results.Count > 0 ? _results.Dequeue() : ValidationResult.Valid();
    }

    private static List<ChatMessage> InitialMessages() => new()
    {
        new ChatMessage(ChatRole.System, "system"),
        new ChatMessage(ChatRole.User, "user")
    };

    [Fact]
    public async Task InvokeAsync_OkOnFirstTry_ReturnsImmediately_NoRetry()
    {
        var chat = new ScriptedChatClient(new Queue<string>(new[] { "{\"ok\":true}" }));
        var coord = new RetryCoordinator();

        var outcome = await coord.InvokeAsync(chat, InitialMessages(), new ChatOptions(),
            new NoOpSkillOutputValidator(), CancellationToken.None);

        outcome.Kind.Should().Be(RetryOutcomeKind.Ok);
        outcome.FinalOutput.Should().Be("{\"ok\":true}");
        chat.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_FailedParse_RetriesOnceWithJsonHint()
    {
        var chat = new ScriptedChatClient(new Queue<string>(new[] { "not json", "{\"ok\":true}" }));
        var coord = new RetryCoordinator();

        var outcome = await coord.InvokeAsync(chat, InitialMessages(), new ChatOptions(),
            new NoOpSkillOutputValidator(), CancellationToken.None);

        outcome.Kind.Should().Be(RetryOutcomeKind.Ok);
        chat.CallCount.Should().Be(2);
        chat.CapturedMessages[1].Last().Text.Should().Be(RetryCoordinator.ParseRetryHint);
    }

    [Fact]
    public async Task InvokeAsync_FailedValidation_RetriesOnceWithConcreteError()
    {
        var chat = new ScriptedChatClient(new Queue<string>(new[] { "{\"a\":1}", "{\"a\":2}" }));
        var validator = new StrictValidator(
            ValidationResult.Invalid("missing field 'b'"),
            ValidationResult.Valid());
        var coord = new RetryCoordinator();

        var outcome = await coord.InvokeAsync(chat, InitialMessages(), new ChatOptions(),
            validator, CancellationToken.None);

        outcome.Kind.Should().Be(RetryOutcomeKind.Ok);
        chat.CallCount.Should().Be(2);
        chat.CapturedMessages[1].Last().Text.Should().Be(
            RetryCoordinator.ValidationRetryHintPrefix + "missing field 'b'");
    }

    [Fact]
    public async Task InvokeAsync_TwoFailedParse_ReturnsParseFailureAfterRetry()
    {
        var chat = new ScriptedChatClient(new Queue<string>(new[] { "garbage1", "still garbage" }));
        var coord = new RetryCoordinator();

        var outcome = await coord.InvokeAsync(chat, InitialMessages(), new ChatOptions(),
            new NoOpSkillOutputValidator(), CancellationToken.None);

        outcome.Kind.Should().Be(RetryOutcomeKind.ParseFailedAfterRetry);
        outcome.FinalOutput.Should().Be("still garbage");
        outcome.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_TwoFailedValidation_ReturnsValidationFailureAfterRetry()
    {
        var chat = new ScriptedChatClient(new Queue<string>(new[] { "{\"a\":1}", "{\"a\":2}" }));
        var validator = new StrictValidator(
            ValidationResult.Invalid("err1"),
            ValidationResult.Invalid("err2"));
        var coord = new RetryCoordinator();

        var outcome = await coord.InvokeAsync(chat, InitialMessages(), new ChatOptions(),
            validator, CancellationToken.None);

        outcome.Kind.Should().Be(RetryOutcomeKind.ValidationFailedAfterRetry);
        outcome.FinalOutput.Should().Be("{\"a\":2}");
        outcome.FailureReason.Should().Be("err2");
    }

    [Fact]
    public async Task InvokeAsync_RetryHintAppendedToMessageThread()
    {
        var chat = new ScriptedChatClient(new Queue<string>(new[] { "not json", "{\"ok\":true}" }));
        var messages = InitialMessages();
        var coord = new RetryCoordinator();

        await coord.InvokeAsync(chat, messages, new ChatOptions(),
            new NoOpSkillOutputValidator(), CancellationToken.None);

        messages.Should().HaveCount(4);
        messages[2].Role.Should().Be(ChatRole.Assistant);
        messages[2].Text.Should().Be("not json");
        messages[3].Role.Should().Be(ChatRole.User);
        messages[3].Text.Should().Be(RetryCoordinator.ParseRetryHint);
    }
}

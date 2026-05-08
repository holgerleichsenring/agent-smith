using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentSmith.Tests.Triage;

public sealed class TriageHandlerSnapshotTests
{
    private readonly Mock<ITriageStrategySelector> _selectorMock = new();
    private readonly Mock<ITriageStrategy> _strategyMock = new();
    private readonly CapturingLogger<TriageHandler> _logger = new();

    public TriageHandlerSnapshotTests()
    {
        _selectorMock.Setup(s => s.Select(It.IsAny<PipelineType>())).Returns(_strategyMock.Object);
        _strategyMock.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandResult.Ok("done"));
    }

    [Fact]
    public async Task ExecuteAsync_TwoConceptsSet_LogsBothInSnapshot()
    {
        var pipeline = PipelineWithVocabulary();
        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        concepts.SetBool("source_available", true);
        concepts.SetEnum("pipeline_name", "fix-bug");

        await Handler().ExecuteAsync(new TriageContext(new AgentConfig(), pipeline), CancellationToken.None);

        var snapshot = FindSnapshotMessage();
        snapshot.Should().Contain("source_available=true");
        snapshot.Should().Contain("pipeline_name=fix-bug");
    }

    [Fact]
    public async Task ExecuteAsync_NoConceptsSet_LogsAllDefaults()
    {
        var pipeline = PipelineWithVocabulary();

        await Handler().ExecuteAsync(new TriageContext(new AgentConfig(), pipeline), CancellationToken.None);

        FindSnapshotMessage().Should().Contain("(all defaults)");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultValuedConcepts_OmittedFromSnapshot()
    {
        var pipeline = PipelineWithVocabulary();
        var concepts = RunStateConceptsTestFactory.Default(pipeline);
        // Setting bool to its default value (false) — should be omitted.
        concepts.SetBool("source_available", false);
        // Setting bool to non-default — should appear.
        concepts.SetBool("context_yaml_present", true);

        await Handler().ExecuteAsync(new TriageContext(new AgentConfig(), pipeline), CancellationToken.None);

        var snapshot = FindSnapshotMessage();
        snapshot.Should().NotContain("source_available");
        snapshot.Should().Contain("context_yaml_present=true");
    }

    private string FindSnapshotMessage()
    {
        var entry = _logger.Entries.SingleOrDefault(e => e.Message.StartsWith("Triage concept snapshot:"));
        entry.Should().NotBeNull();
        return entry!.Message;
    }

    private TriageHandler Handler() => new(
        _selectorMock.Object,
        RunStateConceptsTestFactory.Default,
        _logger);

    private static PipelineContext PipelineWithVocabulary()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ConceptVocabulary, RunStateConceptsTestFactory.P0125cVocabulary);
        return pipeline;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}

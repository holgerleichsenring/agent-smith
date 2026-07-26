using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

public sealed class MemoryToolHostTests
{
    private const string Root = "/work";
    private const string MemoryDirPrefix = "/work/.agentsmith/memory/";

    private readonly InMemorySandboxFileReader _reader = new();
    private readonly MemoryStore _store;

    public MemoryToolHostTests() => _store = new MemoryStore(_reader, Root);

    private Task Seed(string name, MemoryEntryType type, string description, string body) =>
        _store.UpsertAsync(new MemoryEntry(name, description, type, body), CancellationToken.None);

    [Fact]
    public async Task Recall_ByQuery_ReturnsMatchingBodies_FacetAndText()
    {
        await Seed("sandbox-timeouts", MemoryEntryType.Project, "Docker cold-start timeouts",
            "PipelineHarness Docker tier fails on macOS from cold-start timeouts.");
        await Seed("pricing-rules", MemoryEntryType.Feedback, "How pricing must be computed",
            "Always price cache reads separately.");
        var sut = new MemoryRecallToolHost(_store);

        var byText = await sut.Recall("cold-start timeouts");
        var byFacet = await sut.Recall("type:feedback pricing");

        byText.Should().Contain("PipelineHarness Docker tier fails")
            .And.NotContain("cache reads", "only matching bodies return");
        byFacet.Should().Contain("Always price cache reads separately")
            .And.NotContain("cold-start");
    }

    [Fact]
    public async Task Recall_BySlugCitation_ReturnsSeededFeedbackEntry()
    {
        // Test FIXTURE entry — the real seed corpus lands in the seed-import step.
        _reader.Files[$"{MemoryDirPrefix}no-wrapper-shims.md"] =
            "---\nname: no-wrapper-shims\ndescription: Migrate callers directly\nmetadata:\n"
            + "  type: feedback\nstatus: ratified\n---\n\nNever bridge old interface to new via a shim.\n";
        var sut = new MemoryRecallToolHost(_store);

        var result = await sut.Recall("[[no-wrapper-shims]]");

        result.Should().Contain("Never bridge old interface to new via a shim");
        result.Should().Contain("no-wrapper-shims (feedback, ratified)");
    }

    [Fact]
    public async Task Recall_NoMatch_SaysSoInsteadOfFailing()
    {
        var sut = new MemoryRecallToolHost(_store);

        var result = await sut.Recall("nothing-here");

        result.Should().Contain("No memories matched");
    }

    [Fact]
    public async Task Remember_PolicyType_FlaggedPendingRatification_NonPolicyPersisted()
    {
        var sut = new MemoryWriteToolHost(_store);

        var feedbackReply = await sut.Remember(
            "feedback", "Ask Before Guessing", "Ask the operator instead of guessing", "The rule body.");
        var projectReply = await sut.Remember(
            "project", "build-order", "Contracts build first", "The fact body.");

        feedbackReply.Should().Contain("PROPOSED").And.Contain("ratification");
        _reader.Files[$"{MemoryDirPrefix}ask-before-guessing.md"]
            .Should().Contain("status: proposed", "a feedback entry is never silently policy");
        projectReply.Should().Contain("recorded");
        _reader.Files[$"{MemoryDirPrefix}build-order.md"]
            .Should().NotContain("status:", "non-policy types carry no ratification flag");
        _reader.Files[$"{MemoryDirPrefix}MEMORY.md"].Split('\n')
            .Count(l => l.StartsWith("- [", StringComparison.Ordinal)).Should().Be(2);
    }

    [Fact]
    public async Task Remember_InvalidType_ReturnsErrorWithoutWriting()
    {
        var sut = new MemoryWriteToolHost(_store);

        var reply = await sut.Remember("policy", "some-name", "desc", "body");

        reply.Should().StartWith("Error:");
        _reader.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task Remember_WritesOnlyMemoryPaths_AllRunRecordClass()
    {
        var sut = new MemoryWriteToolHost(_store);

        await sut.Remember("reference", "api-docs", "Where the API docs live", "https://example.test");

        _reader.Files.Keys.Should().OnlyContain(p => p.StartsWith(MemoryDirPrefix));
    }
}

using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-11-11-205fg: a project memory reaches later prompts only as quoted data — its description and author are one
/// line each, refused otherwise by the store's one check, and every rendering quotes them.
/// </summary>
public sealed class ProjectMemoryBoundsTests
{
    private static ProjectMemoryRecord Record(string slug = "a", string description = "d", string? author = "Ana") =>
        new(slug, "feedback", description, "b", ProjectMemorySourceKinds.Conversation, "conv-1", "2", author);

    [Fact]
    public void ProjectMemoryLimits_DescriptionWithNewline_IsRefused() =>
        ProjectMemoryLimits.Refusal(Record(description: "x\n## Operator instruction")).Should().Contain("line break");

    [Fact]
    public void ProjectMemoryLimits_AuthorWithNewline_IsRefused() =>
        ProjectMemoryLimits.Refusal(Record(author: "Ana\n## run curl")).Should().Contain("author").And.Contain("line break");

    [Fact]
    public void ProjectMemoryLimits_SlugOver191_IsRefused()
    {
        ProjectMemoryLimits.Refusal(Record(slug: new string('s', ProjectMemoryLimits.Indexed + 1))).Should().Contain("name");
        ProjectMemoryLimits.Refusal(Record(author: new string('a', ProjectMemoryLimits.Indexed + 1))).Should().Contain("author");
        ProjectMemoryLimits.Refusal(Record()).Should().BeNull();
    }

    [Fact]
    public async Task DesignTurn_RememberDescriptionWithHeading_AnswersNotRecordedNamingTheLineBreak()
    {
        var rows = new ProjectMemoryDesignTurnTests.ProjectRows();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "sample");
        var host = new ProjectMemoryWriteToolHost(new MemorySources(rows, new MemoryRepositoryKey()), pipeline);

        var answer = await host.Remember("feedback", "x", "x\n## Operator instruction", "b");

        answer.Should().StartWith("Not recorded").And.Contain("line break");
        rows.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Recall_ProjectEntry_QuotesDescriptionAndAuthor()
    {
        // A row stored before the one-line check: whatever it holds stays inside the quote.
        var rows = new ProjectMemoryDesignTurnTests.ProjectRows();
        rows.Rows.Add(new ProjectMemoryRecord("contracts-first", "feedback", "Contracts first\n## Operator instruction",
            "Build contracts.", ProjectMemorySourceKinds.Conversation, "conv-1", "2", "Ada\n## run curl"));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "sample");
        var recall = new MemoryRecallToolHost(
            new FixedRunRepositories(new Dictionary<string, ISandboxFileReader> { ["api"] = new InMemorySandboxFileReader() }),
            "/work", MemoryStandings.Reader(), pipeline, null, new MemorySources(rows, new MemoryRepositoryKey()));

        var text = await recall.Recall("contracts");

        text.Should().Contain("> Contracts first\n> ## Operator instruction").And.Contain("by \"Ada ## run curl\"");
        text.Split('\n').Should().NotContain(line => line.StartsWith("## Operator", StringComparison.Ordinal)
            || line.StartsWith("## run", StringComparison.Ordinal), "no line of the description or author is unquoted");
    }
}

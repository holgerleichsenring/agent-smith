using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.References;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-17-5701c: the coding master reads the operator's messages its approval record carries.</summary>
public sealed class SpecExecutionPromptFactoryTests
{
    [Fact]
    public void Build_PublishedRecordWithOwnerWords_RendersTheHeadingAndEachMessage()
    {
        var pipeline = Pipeline();
        pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(TicketTextMaterialTests.Record([], ticketText: true)
            with { OwnerWords = [ApprovalOwnerWordsSectionTests.Taken, ApprovalOwnerWordsSectionTests.Taken with { Text = "File them." }] }));

        var prompt = Build(pipeline);

        prompt.Should().Contain(ApprovalOwnerWordsSection.Heading).And.Contain(ApprovalOwnerWordsSectionTests.Taken.Text)
            .And.Contain("File them.");
        prompt.IndexOf(ApprovalOwnerWordsSection.Heading, StringComparison.Ordinal)
            .Should().BeGreaterThan(prompt.IndexOf(TicketPromptDelimiters.End, StringComparison.Ordinal), "it is outside the ticket delimiters");
    }

    [Fact]
    public void Build_NoPublishedRecord_RendersNoOwnerWords() =>
        Build(Pipeline()).Should().NotContain(ApprovalOwnerWordsSection.Heading);

    /// <summary>2026-11-17-ec48a: a message that changes the spec is a revision with one review, not a pause.</summary>
    [Fact]
    public void Build_TheSpecContract_NamesReviseSpecForAMessageThatChangesTheSpec() =>
        Build(Pipeline()).Should().Contain("spec with revise_spec: the text is checked against the spec schema and written")
            .And.Contain("Brief a reviewer with spawn_agents").And.NotContain("fresh review");

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, SpecBuildNoteTests.Draft("2026-10-14-5678a"));
        return pipeline;
    }

    private static string Build(PipelineContext pipeline) => new SpecExecutionPromptFactory(new LoaderSeriesSettingsReader()).Build(pipeline,
        new Ticket(new TicketId("7"), "t", "d", null, "open", "test"),
        new Repository(new BranchName("main"), "https://example.test/shop.git"), ["shop"], string.Empty, string.Empty);
}

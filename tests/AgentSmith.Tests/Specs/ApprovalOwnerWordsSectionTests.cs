using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.References;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-15-53d0: the operator's messages of the approving conversation, rendered with author and UTC time under a
/// heading that says what they are — and nothing when the record carries none.
/// </summary>
public sealed class ApprovalOwnerWordsSectionTests
{
    internal static readonly OwnerWord Taken =
        new("Sample Operator", new DateTimeOffset(2026, 11, 13, 9, 12, 0, TimeSpan.FromHours(1)), "I take all your proposals; the limit is 50 per page.");

    [Fact]
    public void Render_Words_ShowsEachWithAuthorAndUtcTime()
    {
        var text = ApprovalOwnerWordsSection.Render([Taken, Taken with { Text = "File them." }]);

        text.Should().Contain(ApprovalOwnerWordsSection.Heading).And.Contain(ApprovalOwnerWordsSection.Description)
            .And.Contain("### Sample Operator, 2026-11-13 08:12 UTC").And.Contain(Taken.Text).And.Contain("File them.");
        text.IndexOf(Taken.Text, StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("File them.", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_NoWords_IsEmpty() => ApprovalOwnerWordsSection.Render([]).Should().BeEmpty();

    [Fact]
    public void For_PublishedRecordWithWords_RendersThem()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ApprovedSpecSet,
            SpecApprovalJson.Write(TicketTextMaterialTests.Record([], ticketText: true) with { OwnerWords = [Taken] }));

        ApprovalOwnerWordsSection.For(pipeline).Should().Contain(Taken.Text);
    }

    [Fact]
    public void For_OlderRecordOrNone_IsEmpty()
    {
        var pipeline = new PipelineContext();
        ApprovalOwnerWordsSection.For(pipeline).Should().BeEmpty();
        pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(TicketTextMaterialTests.Record([], ticketText: true)));
        ApprovalOwnerWordsSection.For(pipeline).Should().BeEmpty();
    }
}

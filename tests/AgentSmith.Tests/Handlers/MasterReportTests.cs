using AgentSmith.Application.Services.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>2026-11-22-96c8a: a pass's ending text is the text of its final message; the step's report is every
/// pass's ending text in order, an identical text once.</summary>
public sealed class MasterReportTests
{
    private static ChatResponse Pass(params ChatMessage[] messages) => new([.. messages]);
    private static ChatMessage Call() => new(ChatRole.Assistant, [new FunctionCallContent("c", "read_file")]);

    [Fact]
    public void OfPass_IsTheFinalMessagesText() =>
        MasterReport.OfPass(Pass(new ChatMessage(ChatRole.Assistant, "Let me look."), Call(),
            new ChatMessage(ChatRole.Assistant, "  ## Delivered\n- it  "))).Should().Be("## Delivered\n- it");

    [Fact]
    public void OfPass_TextBesideACall_CountsOnlyInTheFinalMessage()
    {
        MasterReport.OfPass(Pass(new ChatMessage(ChatRole.Assistant, [new TextContent("## Delivered"), new FunctionCallContent("c", "finish_spec")])))
            .Should().Be("## Delivered", "the final message carries the text beside its call");
        MasterReport.OfPass(Pass(new ChatMessage(ChatRole.Assistant, [new TextContent("now the tests"), new FunctionCallContent("c", "run_command")]), Call()))
            .Should().BeNull("text beside an earlier call is not the pass's ending");
    }

    [Fact]
    public void OfPass_EndedWithoutText_OrNoResponse_IsNull()
    {
        MasterReport.OfPass(Pass(Call(), new ChatMessage(ChatRole.Assistant, "   "))).Should().BeNull();
        MasterReport.OfPass(null).Should().BeNull();
    }

    [Fact]
    public void Append_AddsEachPassesEndingText_InOrder_AnIdenticalTextOnce()
    {
        IReadOnlyList<string> texts = [];
        texts = MasterReport.Append(texts, Pass(new ChatMessage(ChatRole.Assistant, "## Delivered\n- it")));
        texts = MasterReport.Append(texts, Pass(Call()));
        texts = MasterReport.Append(texts, Pass(new ChatMessage(ChatRole.Assistant, "Marked the remaining steps done.")));
        texts = MasterReport.Append(texts, Pass(new ChatMessage(ChatRole.Assistant, "## Delivered\n- it")));

        MasterReport.Join(texts).Should().Be("## Delivered\n- it\n\nMarked the remaining steps done.");
        MasterReport.Join([]).Should().BeNull();
    }
}

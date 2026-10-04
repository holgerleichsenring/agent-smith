using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-02-3f06b: the one line-count rule every evidence probe shares.</summary>
public sealed class EvidenceLineCountTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("one", 1)]
    [InlineData("one\n", 1)]
    [InlineData("one\ntwo", 2)]
    [InlineData("one\r\ntwo\r\n", 2)]
    [InlineData("\n", 1)]
    public void Of_Content_CountsLinesWithoutTheTrailingNewline(string content, int expected) =>
        EvidenceLineCount.Of(content).Should().Be(expected);
}

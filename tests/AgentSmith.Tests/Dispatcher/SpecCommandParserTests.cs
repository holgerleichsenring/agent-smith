using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.Dispatcher;

public sealed class SpecCommandParserTests
{
    private readonly SpecCommandParser _parser = new();

    [Fact]
    public void Parse_BareSpec_ReturnsOpenWithoutProject() =>
        _parser.Parse("/spec").Should().Be(new SpecOpenCommand(Project: null));

    [Fact]
    public void Parse_SpecWithProject_ReturnsOpenWithProject() =>
        _parser.Parse("/spec backend").Should().Be(new SpecOpenCommand("backend"));

    /// <summary>
    /// 2026-09-22-2a86: the three spellings only this parser ever built. They are not
    /// keywords any more — each is read as the project it names, which is answered with
    /// "unknown project" by the scope resolver rather than silently doing something else.
    /// </summary>
    [Theory]
    [InlineData("/spec list", "list")]
    [InlineData("/spec resume abc123", "resume")]
    [InlineData("/spec resume", "resume")]
    [InlineData("/spec new", "new")]
    [InlineData("/spec new backend", "new")]
    public void Parse_TheListResumeAndForkSpellings_AreNoLongerParsed(string text, string project) =>
        _parser.Parse(text).Should().Be(new SpecOpenCommand(project));

    /// <summary>The door chat still has, unchanged — including the case it is typed in.</summary>
    [Fact]
    public void Parse_TheOpeningSpelling_IsUnchangedForChat()
    {
        _parser.Parse("/SPEC").Should().Be(new SpecOpenCommand(Project: null));
        _parser.Parse("/SPEC Backend").Should().Be(new SpecOpenCommand("Backend"));
        _parser.Parse("  /spec backend  ").Should().Be(new SpecOpenCommand("backend"));
    }

    [Theory]
    [InlineData("fix #42 in sample")]
    [InlineData("spec without slash")]
    [InlineData("/specification")]
    [InlineData("hello")]
    public void Parse_NonSpecText_ReturnsNull(string text) =>
        _parser.Parse(text).Should().BeNull();
}

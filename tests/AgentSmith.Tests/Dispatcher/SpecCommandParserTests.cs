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

    [Fact]
    public void Parse_SpecList_ReturnsList() =>
        _parser.Parse("/spec list").Should().Be(new SpecListCommand());

    [Fact]
    public void Parse_SpecResumeWithId_ReturnsResume() =>
        _parser.Parse("/spec resume abc123").Should().Be(new SpecResumeCommand("abc123"));

    [Fact]
    public void Parse_SpecResumeWithoutId_ReturnsResumeWithEmptyId() =>
        _parser.Parse("/spec resume").Should().Be(new SpecResumeCommand(string.Empty));

    [Fact]
    public void Parse_SpecNew_ReturnsNewWithoutProject() =>
        _parser.Parse("/spec new").Should().Be(new SpecNewCommand(Project: null));

    [Fact]
    public void Parse_SpecNewWithProject_ReturnsNewWithProject() =>
        _parser.Parse("/spec new backend").Should().Be(new SpecNewCommand("backend"));

    [Fact]
    public void Parse_IsCaseInsensitive() =>
        _parser.Parse("/SPEC LIST").Should().Be(new SpecListCommand());

    [Theory]
    [InlineData("fix #42 in sample")]
    [InlineData("spec without slash")]
    [InlineData("/specification")]
    [InlineData("hello")]
    public void Parse_NonSpecText_ReturnsNull(string text) =>
        _parser.Parse(text).Should().BeNull();
}

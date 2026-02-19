using AgentSmith.Application.Services;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public class RegexIntentParserTests
{
    private readonly RegexIntentParser _sut = new(NullLogger<RegexIntentParser>.Instance);

    [Theory]
    [InlineData("fix #123 in todo-list", "123", "todo-list")]
    [InlineData("#34237 todo-list", "34237", "todo-list")]
    [InlineData("todo-list #123", "123", "todo-list")]
    [InlineData("fix 123 in todo-list", "123", "todo-list")]
    [InlineData("resolve ticket #42 in api", "42", "api")]
    [InlineData("#7 myproject", "7", "myproject")]
    public async Task ParseAsync_ValidInput_ReturnsCorrectIntent(
        string input, string expectedTicketId, string expectedProject)
    {
        var result = await _sut.ParseAsync(input);

        result.TicketId.Value.Should().Be(expectedTicketId);
        result.ProjectName.Value.Should().Be(expectedProject);
    }

    [Theory]
    [InlineData("no ticket here")]
    [InlineData("just some random text")]
    public async Task ParseAsync_NoTicketId_ThrowsConfigurationException(string input)
    {
        var act = () => _sut.ParseAsync(input);

        await act.Should().ThrowAsync<ConfigurationException>();
    }

    [Fact]
    public async Task ParseAsync_OnlyNumber_ThrowsConfigurationException()
    {
        var act = () => _sut.ParseAsync("#123");

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*project name*");
    }

    [Fact]
    public async Task ParseAsync_EmptyInput_ThrowsArgumentException()
    {
        var act = () => _sut.ParseAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

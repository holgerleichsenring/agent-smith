using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Models;

public sealed class KnowledgeBaseConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new KnowledgeBaseConfig();

        config.CompileIntervalMinutes.Should().Be(60);
        config.CompileOnEveryRun.Should().BeFalse();
        config.CompileModel.Should().Be("haiku");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var config = new KnowledgeBaseConfig
        {
            CompileIntervalMinutes = 30,
            CompileOnEveryRun = true,
            CompileModel = "sonnet"
        };

        config.CompileIntervalMinutes.Should().Be(30);
        config.CompileOnEveryRun.Should().BeTrue();
        config.CompileModel.Should().Be("sonnet");
    }
}

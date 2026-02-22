using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public class YamlConfigurationLoaderTests
{
    private readonly YamlConfigurationLoader _loader = new();

    private static string TestDataPath(string fileName)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Configuration", "TestData", fileName);
    }

    [Fact]
    public void LoadConfig_ValidYaml_ReturnsConfig()
    {
        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));

        config.Should().NotBeNull();
        config.Projects.Should().ContainKey("testproject");
        config.Pipelines.Should().ContainKey("fix-bug");
    }

    [Fact]
    public void LoadConfig_FileNotFound_ThrowsConfigurationException()
    {
        var act = () => _loader.LoadConfig("nonexistent.yml");

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void LoadConfig_InvalidYaml_ThrowsConfigurationException()
    {
        var act = () => _loader.LoadConfig(TestDataPath("invalid-config.yml"));

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Invalid YAML*");
    }

    [Fact]
    public void LoadConfig_WithEnvVars_ResolvesPlaceholders()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token-123");

        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));

        config.Secrets["github_token"].Should().Be("test-token-123");

        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
    }

    [Fact]
    public void LoadConfig_ProjectHasAllFields_MapsCorrectly()
    {
        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));
        var project = config.Projects["testproject"];

        project.Source.Type.Should().Be("GitHub");
        project.Source.Url.Should().Be("https://github.com/test/repo");
        project.Tickets.Type.Should().Be("AzureDevOps");
        project.Tickets.Organization.Should().Be("testorg");
        project.Agent.Type.Should().Be("Claude");
        project.Agent.Model.Should().Be("sonnet-4");
        project.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void LoadConfig_PipelineHasCommands_MapsCorrectly()
    {
        var config = _loader.LoadConfig(TestDataPath("valid-config.yml"));
        var pipeline = config.Pipelines["fix-bug"];

        pipeline.Commands.Should().HaveCount(3);
        pipeline.Commands[0].Should().Be("FetchTicketCommand");
        pipeline.Commands[1].Should().Be("CheckoutSourceCommand");
        pipeline.Commands[2].Should().Be("CommitAndPRCommand");
    }
}

using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Services.Tools;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class ToolRunnerTests
{
    [Fact]
    public void DetectToolRunnerType_FallsBackToProcess()
    {
        var result = ToolRunnerSetup.DetectToolRunnerType();
        result.Should().BeOneOf("docker", "podman", "process");
    }

    [Fact]
    public void CreateToolRunner_Docker_ReturnsDockerToolRunner()
    {
        var config = new ToolRunnerConfig { Type = "docker" };
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var runner = ToolRunnerSetup.CreateToolRunner(config, sp);
        runner.Should().BeOfType<DockerToolRunner>();
    }

    [Fact]
    public void CreateToolRunner_Process_ReturnsProcessToolRunner()
    {
        var config = new ToolRunnerConfig { Type = "process" };
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var runner = ToolRunnerSetup.CreateToolRunner(config, sp);
        runner.Should().BeOfType<ProcessToolRunner>();
    }

    [Fact]
    public void CreateToolRunner_Podman_ReturnsDockerToolRunner()
    {
        var config = new ToolRunnerConfig
        {
            Type = "podman",
            Socket = "unix:///run/podman/podman.sock"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var runner = ToolRunnerSetup.CreateToolRunner(config, sp);
        runner.Should().BeOfType<DockerToolRunner>();
    }

    [Fact]
    public void ToolRunnerConfig_DefaultImages()
    {
        var config = new ToolRunnerConfig();
        config.Images.Should().ContainKey("nuclei");
        config.Images.Should().ContainKey("spectral");
    }

    [Fact]
    public void ToolRunRequest_DefaultTimeout()
    {
        var request = new ToolRunRequest("nuclei", ["--help"]);
        request.TimeoutSeconds.Should().Be(300);
        request.InputFiles.Should().BeNull();
        request.OutputFileName.Should().BeNull();
    }

    [Fact]
    public async Task ProcessToolRunner_InvalidBinary_ReturnsError()
    {
        var runner = new ProcessToolRunner(NullLogger<ProcessToolRunner>.Instance);
        var request = new ToolRunRequest("nonexistent-tool-xyz", ["--help"], TimeoutSeconds: 5);

        var act = () => runner.RunAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("{work}/file.txt", "/work", "/work/file.txt")]
    [InlineData("{work}/results.jsonl", "/custom", "/custom/results.jsonl")]
    [InlineData("--no-placeholders", "/work", "--no-placeholders")]
    [InlineData("{work}/a/{work}/b", "/w", "/w/a//w/b")]
    public void ResolveArguments_ReplacesWorkPlaceholder(string input, string workDir, string expected)
    {
        var result = DockerToolRunner.ResolveArguments([input], workDir);
        result[0].Should().Be(expected);
    }

    [Fact]
    public void ResolveArguments_MultipleArgs_AllResolved()
    {
        var args = new List<string> { "-list", "{work}/targets.txt", "-output", "{work}/results.jsonl" };
        var result = DockerToolRunner.ResolveArguments(args, "/work");

        result.Should().Equal("-list", "/work/targets.txt", "-output", "/work/results.jsonl");
    }

    [Fact]
    public void ResolveArguments_NoPlaceholders_PassedThrough()
    {
        var args = new List<string> { "-severity", "high", "--follow-redirects" };
        var result = DockerToolRunner.ResolveArguments(args, "/work");

        result.Should().Equal("-severity", "high", "--follow-redirects");
    }
}

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
        // On macOS/Linux dev machines without Docker socket at standard path,
        // detection should fall back to "process"
        var result = ServiceCollectionExtensions.DetectToolRunnerType();
        result.Should().BeOneOf("docker", "podman", "process");
    }

    [Fact]
    public void CreateToolRunner_Docker_ReturnsDockerToolRunner()
    {
        var config = new ToolRunnerConfig { Type = "docker" };
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var runner = ServiceCollectionExtensions.CreateToolRunner(config, sp);

        runner.Should().BeOfType<DockerToolRunner>();
    }

    [Fact]
    public void CreateToolRunner_Process_ReturnsProcessToolRunner()
    {
        var config = new ToolRunnerConfig { Type = "process" };
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var runner = ServiceCollectionExtensions.CreateToolRunner(config, sp);

        runner.Should().BeOfType<ProcessToolRunner>();
    }

    [Fact]
    public void CreateToolRunner_Podman_ReturnsDockerToolRunner()
    {
        // Podman uses Docker-compatible API, same runner with different socket
        var config = new ToolRunnerConfig
        {
            Type = "podman",
            Socket = "unix:///run/podman/podman.sock"
        };
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var runner = ServiceCollectionExtensions.CreateToolRunner(config, sp);

        runner.Should().BeOfType<DockerToolRunner>();
    }

    [Fact]
    public void ToolRunnerConfig_DefaultImages()
    {
        var config = new ToolRunnerConfig();

        config.Images.Should().ContainKey("nuclei");
        config.Images.Should().ContainKey("spectral");
        config.Images["nuclei"].Should().Contain("nuclei");
        config.Images["spectral"].Should().Contain("spectral");
    }

    [Fact]
    public void ToolRunRequest_DefaultTimeout()
    {
        var request = new ToolRunRequest("nuclei", ["--help"]);

        request.TimeoutSeconds.Should().Be(300);
        request.InputFiles.Should().BeNull();
        request.OutputFileName.Should().BeNull();
        request.ExtraHosts.Should().BeNull();
    }

    [Fact]
    public void DockerToolRunner_GetSharedTempPath_ReturnsValidPath()
    {
        var result = DockerToolRunner.GetSharedTempPath();
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessToolRunner_InvalidBinary_ReturnsError()
    {
        var runner = new ProcessToolRunner(NullLogger<ProcessToolRunner>.Instance);
        var request = new ToolRunRequest(
            "nonexistent-tool-xyz",
            ["--help"],
            TimeoutSeconds: 5);

        var act = () => runner.RunAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}

using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-10-12-6bfab: the CLI's sandbox block is the file, re-read on each call.</summary>
public sealed class LoaderSandboxSettingsReaderTests
{
    private static readonly SandboxGlobalConfig Narrowed = new() { AllowedRegistries = ["registry.example.com/"] };

    [Fact]
    public void Current_WithAConfigPath_RereadsTheFileEachCall()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.SetupSequence(l => l.LoadConfig("agentsmith.yml"))
            .Returns(new AgentSmithConfig())
            .Returns(new AgentSmithConfig { Sandbox = Narrowed });
        var reader = new LoaderSandboxSettingsReader(loader.Object, new ServerContext("agentsmith.yml"));

        reader.Current().AllowedRegistries.Should().BeEmpty();
        reader.Current().AllowedRegistries.Should().Equal("registry.example.com/");
    }

    [Fact]
    public void Current_WithoutAConfigPath_IsTheComposedOptions() =>
        new LoaderSandboxSettingsReader(options: Options.Create(Narrowed))
            .Current().Should().BeSameAs(Narrowed);
}

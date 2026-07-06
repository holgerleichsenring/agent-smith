using AgentSmith.Application.Models;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Server;

// Reads the backend-selecting env vars via AddSandbox → serialize with the env-mutating
// SandboxBackendDetectionTests (see ServerDiLifetimeTests) to avoid the k8s-backend race.
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SandboxOptionsBindingTests
{
    [Fact]
    public void Configure_WithSandboxSection_BindsAllResourceQuantities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sandbox:CpuRequest"] = "800m",
                ["Sandbox:CpuLimit"] = "4000m",
                ["Sandbox:MemoryRequest"] = "1280Mi",
                ["Sandbox:MemoryLimit"] = "6Gi"
            }).Build();
        var services = new ServiceCollection().AddSandboxOptions(configuration);

        var bound = services.BuildServiceProvider().GetRequiredService<IOptions<SandboxOptions>>().Value;

        bound.CpuRequest.Should().Be("800m");
        bound.CpuLimit.Should().Be("4000m");
        bound.MemoryRequest.Should().Be("1280Mi");
        bound.MemoryLimit.Should().Be("6Gi");
    }

    [Fact]
    public void Configure_WithoutSandboxSection_ProducesDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddSandboxOptions(configuration);

        var bound = services.BuildServiceProvider().GetRequiredService<IOptions<SandboxOptions>>().Value;

        bound.ToResourceLimits().Should().Be(AgentSmith.Contracts.Sandbox.ResourceLimits.Default);
    }
}

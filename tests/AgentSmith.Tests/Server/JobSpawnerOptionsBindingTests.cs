using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Server;

public sealed class JobSpawnerOptionsBindingTests
{
    [Fact]
    public void Configure_WithJobSpawnerSection_BindsAllProperties()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JobSpawner:Namespace"] = "agentsmith-prod",
                ["JobSpawner:Image"] = "registry/agentsmith-cli:1.2.3",
                ["JobSpawner:ImagePullPolicy"] = "Always",
                ["JobSpawner:SecretName"] = "agentsmith-prod-secrets",
                ["JobSpawner:TtlSecondsAfterFinished"] = "600",
                ["JobSpawner:DockerNetwork"] = "host"
            }).Build();
        var services = new ServiceCollection().AddJobSpawnerOptions(configuration);

        var bound = services.BuildServiceProvider().GetRequiredService<IOptions<JobSpawnerOptions>>().Value;

        bound.Namespace.Should().Be("agentsmith-prod");
        bound.Image.Should().Be("registry/agentsmith-cli:1.2.3");
        bound.ImagePullPolicy.Should().Be("Always");
        bound.SecretName.Should().Be("agentsmith-prod-secrets");
        bound.TtlSecondsAfterFinished.Should().Be(600);
        bound.DockerNetwork.Should().Be("host");
    }

    [Fact]
    public void Configure_WithoutSection_FallsBackToLegacyDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddJobSpawnerOptions(configuration);

        var bound = services.BuildServiceProvider().GetRequiredService<IOptions<JobSpawnerOptions>>().Value;

        bound.Namespace.Should().Be("default");
        bound.Image.Should().Be("agentsmith-cli:latest");
        bound.ImagePullPolicy.Should().Be("IfNotPresent");
        bound.SecretName.Should().Be("agentsmith-secrets");
    }
}

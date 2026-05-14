using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

public sealed class SandboxSpecBuilderResourcesTests
{
    [Fact]
    public void Build_PopulatesResourcesFromResolver()
    {
        var resolverResult = new ResourceLimits("750m", "3000m", "1Gi", "8Gi");
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver(resolverResult));

        var spec = sut.Build(new ProjectConfig(), language: "csharp");

        spec.Resources.Should().BeSameAs(resolverResult);
    }

    [Fact]
    public void Build_WithoutLanguage_StillPopulatesResources()
    {
        var sut = new SandboxSpecBuilder(new StubSandboxResourceResolver());

        var spec = sut.Build(new ProjectConfig(), language: null);

        spec.Resources.Should().Be(ResourceLimits.Default);
    }
}

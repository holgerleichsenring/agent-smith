using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-6c46: the LLM-authored block is judged by its own type now — accepted whole
/// and clamped, or rejected whole. Held apart from the layer ORDER so the resolver that
/// walks the four layers could take the layer accessor the project sandbox tab needs.
/// </summary>
public sealed class ContextResourceAcceptanceTests
{
    private static ContextResourceAcceptance NewSut(SandboxOptions? options = null) =>
        new(Options.Create(options ?? new SandboxOptions()));

    [Fact]
    public void Accept_AWholeParseableBlock_ReturnsIt()
    {
        var accepted = NewSut().Accept(new ContextYamlStackResources("500m", "1", "1Gi", "2Gi"));

        accepted.Should().Be(new ResourceLimits("500m", "1", "1Gi", "2Gi"));
    }

    [Fact]
    public void Accept_APartialBlock_RejectsItWhole()
    {
        NewSut().Accept(new ContextYamlStackResources("500m", null, "1Gi", "2Gi")).Should().BeNull();
    }

    [Fact]
    public void Accept_NoBlockAtAll_ReturnsNull()
    {
        NewSut().Accept(null).Should().BeNull();
    }

    [Fact]
    public void Accept_ABlockOverTheCeiling_ClampsRatherThanRejects()
    {
        var sut = NewSut(new SandboxOptions { MaxCpuLimit = "2", MaxMemoryLimit = "4Gi" });

        var accepted = sut.Accept(new ContextYamlStackResources("500m", "8", "1Gi", "16Gi"));

        accepted.Should().Be(new ResourceLimits("500m", "2", "1Gi", "4Gi"));
    }
}

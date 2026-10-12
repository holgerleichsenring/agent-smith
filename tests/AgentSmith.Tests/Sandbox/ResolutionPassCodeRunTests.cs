using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-12-006d: the effective snapshot's sandbox resources and cost cap are what a run of
/// the code pipeline gets. A run reads its OWN pipeline, chosen per run; the project's pipeline
/// field decided the snapshot and showed the light profile under a global-default badge.
/// </summary>
public sealed class ResolutionPassCodeRunTests
{
    private static readonly CostCapValues CodeCap = new() { Usd = 42m, Tokens = 4_200_000 };

    [Theory]
    [InlineData("")]
    [InlineData("security-scan")]
    public void ConfigResolutionPass_SizesResourcesAndCostCapForACodeRun(string pipelineField)
    {
        var config = new AgentSmithConfig
        {
            PipelineCostCap = new PipelineCostCapConfig
            {
                Default = new CostCapValues { Usd = 5m, Tokens = 500_000 },
                PerPipeline = new Dictionary<string, CostCapValues>(StringComparer.OrdinalIgnoreCase)
                { ["code"] = CodeCap },
            },
        };
        var pass = new ConfigResolutionPass(
            Options.Create(new SandboxGlobalConfig()), WiredResourceResolver.Create(),
            new StubAgentImageResolver(), config);

        var settings = pass.Resolve(new ResolvedProject { Name = "p", Pipeline = pipelineField });

        settings.SandboxResources.Value.Should().NotBe(ResourceLimits.LightProfile);
        settings.SandboxResources.Source.Should().Be(ResolutionSource.GlobalDefault);
        settings.CostCap.Value.Should().BeSameAs(CodeCap);
    }
}

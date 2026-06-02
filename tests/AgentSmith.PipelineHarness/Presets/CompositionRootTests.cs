using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 falsifiability anchor for the p0198-followup bug class. These
/// tests DO NOT verify what handlers do — they verify that the production
/// composition root (ServerCompositionBuilder) hands the right pieces to
/// handlers. If someone reintroduces the AddSingleton ordering bug or any
/// other composition-root drift that hides operator config, these tests
/// turn red BEFORE production.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CompositionRootTests
{
    [Fact]
    public async Task ServerComposition_LoadsRegistriesBlock_HandlerSeesOperatorConfig()
    {
        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN", "fixture-pat-xyz");
        try
        {
            await using var harness = RealCompositionHarness.Build(FixturePath("agentsmith.yml"));

            var config = harness.Config;

            config.Registries.Should().HaveCount(1,
                "the fixture YAML's `registries:` block must reach DI through ServerCompositionBuilder. " +
                "If empty, the p0198-followup ordering bug is back (override before AddCoreDispatcherServices " +
                "→ Empty() placeholder overwrites the loaded config).");
            config.Registries[0].Host.Should().Be("pkgs.dev.azure.com");
            config.Registries[0].Token.Should().Be("fixture-pat-xyz",
                "secret reference `${azdo_token}` must resolve through the secrets dict.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN", null);
        }
    }

    [Fact]
    public async Task ServerComposition_WithoutRegistriesBlock_ConfigIsEmpty_HandlerToleratesIt()
    {
        await using var harness = RealCompositionHarness.Build(FixturePath("agentsmith-no-registries.yml"));

        var config = harness.Config;

        config.Registries.Should().BeEmpty(
            "docs-only / public-only YAML must round-trip to an empty list — not a parse error, not a null.");
    }

    [Fact]
    public async Task ServerComposition_ResolvesSetupRegistryAuthHandler_FromDI()
    {
        Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN", "fixture-pat");
        try
        {
            await using var harness = RealCompositionHarness.Build(FixturePath("agentsmith.yml"));

            var handler = harness.Services.GetService<ICommandHandler<SetupRegistryAuthContext>>();

            handler.Should().NotBeNull(
                "SetupRegistryAuthHandler MUST resolve from the production DI graph or every " +
                "code-touching pipeline preset will fail at step 4/15.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTSMITH_TEST_AZDO_TOKEN", null);
        }
    }

    private static string FixturePath(string filename)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        return Path.Combine(dir, filename);
    }
}

using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0345c: single-source-of-truth coverage for the capabilities descriptor
/// (mirrors CommandBeatsCoverageTests). Every list served by
/// <c>GET /api/config/capabilities</c> must stay derived from code truth — a new
/// tracker/repo type, resolution strategy, chat-client builder, or pipeline
/// preset that the descriptor does not know fails here, not in a drifted form.
/// </summary>
public sealed class ConfigCapabilitiesTests
{
    private static IReadOnlyList<string> Required(ConfigCapabilities capabilities, string trackerType) =>
        capabilities.TrackerTypes.Single(t => t.Type == trackerType)
            .Fields.Where(f => f.Required).Select(f => f.Key).ToList();

    private static ConfigCapabilities BuildFromRegisteredBuilders()
    {
        using var services = new ServiceCollection().AddAgentProviders().BuildServiceProvider();
        return ConfigStudioCapabilities.Build(
            services.GetServices<IChatClientBuilder>().SelectMany(b => b.SupportedTypes));
    }

    // p0345c spec test: Capabilities_ServesBackendTruth_TypesFieldsStrategies
    [Fact]
    public void Capabilities_ServesBackendTruth_TypesFieldsStrategies()
    {
        var capabilities = BuildFromRegisteredBuilders();

        // Tracker types cover the ENUM the loader binds — no type without a field set.
        capabilities.TrackerTypes.Select(t => t.Type).Should().BeEquivalentTo(
            Enum.GetValues<TrackerType>().Select(ConfigStudioCapabilities.WireName));

        // Connection types cover every discoverable git host (Local is a repo
        // locator, not a host) with the host-specific org label.
        capabilities.ConnectionTypes.Select(c => c.Type).Should().BeEquivalentTo(
            Enum.GetValues<RepoType>().Where(t => t != RepoType.Local)
                .Select(ConfigStudioCapabilities.WireName));
        capabilities.ConnectionTypes.Single(c => c.Type == "azure_devops").OrgLabel.Should().Be("organization");
        capabilities.ConnectionTypes.Single(c => c.Type == "github").OrgLabel.Should().Be("owner");
        capabilities.ConnectionTypes.Single(c => c.Type == "gitlab").OrgLabel.Should().Be("group");

        // Strategies cover the enum; pipelines are the code-defined presets.
        capabilities.ResolutionStrategies.Should().BeEquivalentTo(
            Enum.GetValues<ResolutionStrategy>().Select(ConfigStudioCapabilities.WireName));
        capabilities.Pipelines.Should().BeEquivalentTo(PipelinePresets.Names);

        // Every type authenticates via a secret NAME; the ADO identity pair and the
        // hosts' URL requirements match what TicketProviderFactory actually consumes.
        capabilities.TrackerTypes.Should().OnlyContain(t =>
            t.Fields.Any(f => f.Key == "authSecret" && f.Required));
        Required(capabilities, "azure_devops").Should().Contain(["organization", "project"]);
        Required(capabilities, "github").Should().Contain("url");
        Required(capabilities, "jira").Should().Contain("url");
        Required(capabilities, "gitlab").Should().Contain("project");
    }

    // Every strategy the capabilities serve is ACCEPTED by the runtime parser
    // (EffectiveTriggerBuilder) — the UI can never offer a strategy the loader
    // would then reject. A new enum value that the builder does not parse fails here.
    [Fact]
    public void Capabilities_EveryResolutionStrategy_AcceptedByEffectiveTriggerBuilder()
    {
        foreach (var strategy in ConfigStudioCapabilities.ResolutionStrategyNames)
        {
            var project = new RawProjectEntry
            {
                Resolution = new Dictionary<string, string> { [strategy] = "match-value" },
            };
            new EffectiveTriggerBuilder().Apply(
                "coverage", project, new RawTrackerEntry { Type = TrackerType.AzureDevOps });

            project.AzuredevopsTrigger.Should().NotBeNull(
                $"strategy '{strategy}' must produce an effective trigger");
            project.AzuredevopsTrigger!.ProjectResolution!.Value.Should().Be("match-value");
        }
    }

    // Reflection tripwire: every IChatClientBuilder implementation in the
    // Infrastructure assembly must be DI-registered (AddAgentProviders), and the
    // served provider list must be exactly the registered builders' supported
    // types — a new provider cannot ship without the capabilities knowing it.
    [Fact]
    public void Capabilities_AgentProviders_CoverEveryRegisteredBuilder()
    {
        using var services = new ServiceCollection().AddAgentProviders().BuildServiceProvider();
        var builders = services.GetServices<IChatClientBuilder>().ToList();

        var implementations = typeof(IChatClientBuilder).Assembly.GetTypes()
            .Where(t => typeof(IChatClientBuilder).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToList();
        builders.Select(b => b.GetType()).Should().BeEquivalentTo(
            implementations,
            "every IChatClientBuilder implementation must be registered by AddAgentProviders");

        var capabilities = ConfigStudioCapabilities.Build(builders.SelectMany(b => b.SupportedTypes));
        capabilities.AgentProviders.Should().BeEquivalentTo(
            builders.SelectMany(b => b.SupportedTypes).Distinct(StringComparer.OrdinalIgnoreCase));
        capabilities.AgentProviders.Should().Contain(["claude", "anthropic", "openai", "azure_openai", "gemini", "ollama"]);
    }

    // The descriptor is also the write-side gate: unknown types and missing
    // per-type required fields are rejected before anything persists.
    [Fact]
    public void ValidateTracker_EnforcesPerTypeRequiredFields_FromTheDescriptor()
    {
        var missingOrg = () => ConfigStudioCapabilities.ValidateTracker(
            new TrackerEntity("t", "azure_devops", "ado_token", Project: "Platform"));
        missingOrg.Should().Throw<ConfigurationException>().WithMessage("*organization*");

        var unknownType = () => ConfigStudioCapabilities.ValidateTracker(
            new TrackerEntity("t", "bugzilla", "token"));
        unknownType.Should().Throw<ConfigurationException>().WithMessage("*unknown type*bugzilla*");

        var valid = () => ConfigStudioCapabilities.ValidateTracker(
            new TrackerEntity("t", "azure_devops", "ado_token", Organization: "acme", Project: "Platform"));
        valid.Should().NotThrow();
    }

    [Fact]
    public void ValidateProjectResolution_RejectsUnknownStrategyAndEmptyValue()
    {
        var unknown = () => ConfigStudioCapabilities.ValidateProjectResolution(
            new ProjectEntity("p", "a", "t", ["r"], "fix-bug", ["fix-bug"], new ProjectResolution("labels", "x")));
        unknown.Should().Throw<ConfigurationException>().WithMessage("*labels*not a known*");

        var empty = () => ConfigStudioCapabilities.ValidateProjectResolution(
            new ProjectEntity("p", "a", "t", ["r"], "fix-bug", ["fix-bug"], new ProjectResolution("tag", " ")));
        empty.Should().Throw<ConfigurationException>().WithMessage("*must not be empty*");

        var valid = () => ConfigStudioCapabilities.ValidateProjectResolution(
            new ProjectEntity("p", "a", "t", ["r"], "fix-bug", ["fix-bug"], new ProjectResolution("area_path", "Acme/Platform")));
        valid.Should().NotThrow();
    }

    // p0351 spec test: the model-role vocabulary is the fixed TaskType set (coding + roles).
    [Fact]
    public void Capabilities_Roles_AreCodingPlusTaskTypeSet_ReasoningOptional()
    {
        var roles = BuildFromRegisteredBuilders().Roles;
        var keys = roles.Select(r => r.Key).ToList();

        keys.Should().Contain("coding");
        // every TaskType role is covered — a new TaskType without a role trips this.
        foreach (var t in Enum.GetValues<TaskType>())
            keys.Should().Contain(char.ToLowerInvariant(t.ToString()[0]) + t.ToString()[1..]);

        roles.Single(r => r.Key == "coding").Optional.Should().BeFalse();
        roles.Single(r => r.Key == "reasoning").Optional.Should().BeTrue();
    }

    [Fact]
    public void ValidateAgent_UnknownRoleKey_Throws()
    {
        var agent = AgentWith(new Dictionary<string, AgentModelAssignment> { ["bogus"] = new("m") });
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent);
        act.Should().Throw<ConfigurationException>().WithMessage("*unknown model role 'bogus'*");
    }

    [Fact]
    public void ValidateAgent_RoleModelMissingFromPricing_Throws()
    {
        var agent = AgentWith(new Dictionary<string, AgentModelAssignment> { ["coding"] = new("gpt-5.6-terra") });
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent);
        act.Should().Throw<ConfigurationException>().WithMessage("*gpt-5.6-terra*no pricing entry*");
    }

    [Fact]
    public void ValidateAgent_AllRolesPriced_Passes()
    {
        var agent = AgentWith(
            new Dictionary<string, AgentModelAssignment>
            {
                ["coding"] = new("gpt-5.6-terra"),
                ["scout"] = new("gpt-4.1-mini"),
                ["reasoning"] = new(""), // an unset optional role is skipped
            },
            new AgentPricing(new Dictionary<string, AgentModelPricing>
            {
                ["gpt-5.6-terra"] = new(2.5m, 15m),
                ["gpt-4.1-mini"] = new(0.4m, 1.6m),
            }));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent);
        act.Should().NotThrow();
    }

    private static AgentEntity AgentWith(
        IReadOnlyDictionary<string, AgentModelAssignment> models, AgentPricing? pricing = null) =>
        new("a", "claude", null, null, null, null, models, pricing, null, null, null);
}

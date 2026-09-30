using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Pricing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
    private static readonly BundledModelPriceList Prices = new();

    [Fact]
    public void Capabilities_TrackerFields_DeclareDefaultPipelineOptional()
    {
        // 2026-09-16-a4d7: declared, so every tracker form renders it — and OPTIONAL,
        // because Required is enforced by ValidateTracker on every upsert and would make
        // every tracker configured before the phase unsaveable.
        var capabilities = ConfigStudioCapabilities.Build(["claude"]);

        foreach (var type in capabilities.TrackerTypes)
        {
            var field = type.Fields.Should().ContainSingle(f => f.Key == "defaultPipeline").Which;
            field.Required.Should().BeFalse();
            field.Kind.Should().Be(CapabilityFieldKind.Text);
        }
    }

    private static IReadOnlyList<string> Required(ConfigCapabilities capabilities, string trackerType) =>
        capabilities.TrackerTypes.Single(t => t.Type == trackerType)
            .Fields.Where(f => f.Required).Select(f => f.Key).ToList();

    // p0416: enumerating the registered builders CONSTRUCTS them, and a real builder may
    // take collaborators (the external-worker bridge takes the run context and a logger).
    // Register what every production host registers rather than a graph that only happens
    // to work while all builders are dependency-free.
    private static ServiceCollection ProductionShapedProviders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRunContextAccessor, AsyncLocalRunContextAccessor>();
        services.AddAgentProviders();
        return services;
    }

    private static ConfigCapabilities BuildFromRegisteredBuilders()
    {
        using var services = ProductionShapedProviders().BuildServiceProvider();
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
        using var services = ProductionShapedProviders().BuildServiceProvider();
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
    public void TrackerTypes_AllDeclareWorkflowFields_DoneAndFailedStatus()
    {
        // The tracker-owned workflow (p0281b) must be editable in the studio:
        // every tracker type declares trigger/done/failed status fields, all
        // optional — without them a failed run could never be given a native
        // failed_status from the UI and the ticket stayed claimable.
        var capabilities = ConfigStudioCapabilities.Build([]);
        capabilities.TrackerTypes.Should().OnlyContain(t =>
            t.Fields.Any(f => f.Key == "triggerStatuses" && !f.Required)
            && t.Fields.Any(f => f.Key == "doneStatus" && !f.Required)
            && t.Fields.Any(f => f.Key == "failedStatus" && !f.Required)
            && t.Fields.Any(f => f.Key == "openStates" && !f.Required));
    }

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
            new ProjectEntity("p", "a", "t", ["r"], "code", ["code"], new ProjectResolution("labels", "x")));
        unknown.Should().Throw<ConfigurationException>().WithMessage("*labels*not a known*");

        var empty = () => ConfigStudioCapabilities.ValidateProjectResolution(
            new ProjectEntity("p", "a", "t", ["r"], "code", ["code"], new ProjectResolution("tag", " ")));
        empty.Should().Throw<ConfigurationException>().WithMessage("*must not be empty*");

        var valid = () => ConfigStudioCapabilities.ValidateProjectResolution(
            new ProjectEntity("p", "a", "t", ["r"], "code", ["code"], new ProjectResolution("area_path", "Acme/Platform")));
        valid.Should().NotThrow();
    }

    // p0351 spec test: the model-role vocabulary is the fixed TaskType set. 2026-09-30-62bab:
    // 'coding' folded into primary, which is the one required role.
    [Fact]
    public void Capabilities_Roles_AreTheTaskTypeSet_PrimaryRequired()
    {
        var roles = BuildFromRegisteredBuilders().Roles;
        var keys = roles.Select(r => r.Key).ToList();

        keys.Should().NotContain("coding");
        // every TaskType role is covered — a new TaskType without a role trips this.
        foreach (var t in Enum.GetValues<TaskType>())
            keys.Should().Contain(char.ToLowerInvariant(t.ToString()[0]) + t.ToString()[1..]);
        keys.Should().HaveCount(Enum.GetValues<TaskType>().Length);

        roles[0].Key.Should().Be("primary");
        roles.Single(r => r.Key == "primary").Optional.Should().BeFalse();
        roles.Single(r => r.Key == "reasoning").Optional.Should().BeTrue();
    }

    [Fact]
    public void Capabilities_Roles_NeedStrongWhereTheyDecideStructure() =>
        BuildFromRegisteredBuilders().Roles.Where(r => r.NeedsStrong).Select(r => r.Key)
            .Should().BeEquivalentTo("primary", "planning", "reasoning", "contextGeneration", "codeMapGeneration");

    [Fact]
    public void ValidateAgent_UnknownRoleKey_Throws()
    {
        var agent = AgentWith(Catalog(("m", "gpt-5.6")), Roles(("primary", "m"), ("bogus", "m")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*unknown model role 'bogus'*");
    }

    [Fact]
    public void ValidateAgent_CodingRole_IsNoLongerARole()
    {
        var agent = AgentWith(Catalog(("m", "gpt-5.6")), Roles(("primary", "m"), ("coding", "m")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*unknown model role 'coding'*");
    }

    [Fact]
    public void ValidateAgent_NoPrimary_Throws()
    {
        var agent = AgentWith(Catalog(("m", "gpt-5.6")), Roles(("scout", "m")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*primary role must use a catalog entry*");
    }

    [Fact]
    public void ValidateAgent_RoleNamingAnUndeclaredEntry_Throws()
    {
        var agent = AgentWith(Catalog(("m", "gpt-5.6")), Roles(("primary", "m"), ("scout", "mini")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*role 'scout' uses catalog entry 'mini'*does not*");
    }

    [Fact]
    public void ValidateAgent_EntryWithoutAModel_Throws()
    {
        var agent = AgentWith(Catalog(("m", " ")), Roles(("primary", "m")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*catalog entry 'm' names no model*");
    }

    [Fact]
    public void ValidateAgent_EntryModelMissingFromPricing_Throws()
    {
        // 2026-09-30-62baa: gpt-5.6-terra is a real, list-priced id now; the refused id is a
        // typo that starts with real ids (gpt-5.6), which a prefix match would have let through.
        var agent = AgentWith(Catalog(("m", "gpt-5.6-nonesuch")), Roles(("primary", "m")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*entry 'm'*gpt-5.6-nonesuch*no pricing entry*");
    }

    [Fact]
    public void ValidateAgent_UnusedEntry_IsPricedToo()
    {
        var agent = AgentWith(Catalog(("m", "gpt-5.6"), ("spare", "gpt-5.6-nonesuch")), Roles(("primary", "m")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().Throw<ConfigurationException>().WithMessage("*entry 'spare'*no pricing entry*");
    }

    [Fact]
    public void ValidateAgent_ListPricedEntries_PassWithoutPricingBlock()
    {
        var agent = AgentWith(
            Catalog(("m", "gpt-5.6"), ("haiku", "claude-haiku-4-5-20251001")),
            Roles(("primary", "m"), ("scout", "haiku"), ("reasoning", "")));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().NotThrow("an empty role is unset and inherits");
    }

    [Fact]
    public void ValidateAgent_OverridePricedEntry_PassesThoughTheListLacksIt()
    {
        var agent = AgentWith(
            Catalog(("coder", "in-house-coder-7")), Roles(("primary", "coder")),
            new AgentPricing(new Dictionary<string, AgentModelPricing> { ["in-house-coder-7"] = new(1m, 2m) }));
        var act = () => ConfigStudioCapabilities.ValidateAgent(agent, Prices);
        act.Should().NotThrow();
    }

    private static Dictionary<string, AgentCatalogModel> Catalog(params (string Name, string Model)[] entries) =>
        entries.ToDictionary(e => e.Name, e => new AgentCatalogModel(e.Model));

    private static Dictionary<string, string> Roles(params (string Role, string Use)[] roles) =>
        roles.ToDictionary(r => r.Role, r => r.Use);

    private static AgentEntity AgentWith(
        IReadOnlyDictionary<string, AgentCatalogModel> catalog, IReadOnlyDictionary<string, string> models,
        AgentPricing? pricing = null) =>
        new("a", "claude", null, null, null, null, catalog, models, pricing, null, null, null);
}

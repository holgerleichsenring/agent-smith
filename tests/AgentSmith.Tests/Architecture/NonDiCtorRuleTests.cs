using System.Reflection;
using AgentSmith.Application.Services.RedisDisabled;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0157d: enforces the "function classes are DI-only" rule against the
/// Server DI graph. Iterates every ServiceDescriptor whose ImplementationType
/// is in AgentSmith.Application / Infrastructure / Server / Sandbox.Agent and
/// asserts that its public ctor parameters are all DI-legal — interface,
/// IOptions/IOptionsMonitor/IOptionsSnapshot, ILogger/ILogger&lt;T&gt;, framework
/// well-knowns (HttpClient, TimeProvider, IServiceProvider, IConfiguration),
/// or a concrete type that is itself registered in the same IServiceCollection.
/// Known violators at this phase's commit-time are listed in
/// <see cref="Allowlist"/>; the list shrinks as later phases land.
/// </summary>
public sealed class NonDiCtorRuleTests
{
    // Allowlist format: "{FullClassName}::{ParameterTypeFullName} {parameterName}".
    // To remove an entry, fix the ctor so the parameter is DI-legal (interface,
    // IOptions<T>, or a type registered in IServiceCollection).
    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        // Populated empty initially — first run produces the baseline below.
    };

    private static readonly HashSet<string> TargetAssemblyPrefixes = new(StringComparer.Ordinal)
    {
        "AgentSmith.Application",
        "AgentSmith.Infrastructure",
        "AgentSmith.Infrastructure.Core",
        "AgentSmith.Server",
        "AgentSmith.Sandbox.Agent",
    };

    [Fact]
    public void AllRegisteredServices_HaveDiOnlyCtorParameters_OrAreAllowlisted()
    {
        var services = BuildServerLikeServices();
        var registeredTypes = CollectRegisteredTypes(services);

        var violations = new List<string>();
        foreach (var descriptor in services)
        {
            var implType = descriptor.ImplementationType;
            if (implType is null) continue; // factory-based: cannot reflect on ctor
            if (!IsTargetType(implType)) continue;
            CollectViolations(implType, registeredTypes, violations);
        }

        var newViolations = violations.Where(v => !Allowlist.Contains(v)).ToList();
        newViolations.Should().BeEmpty(
            "every DI-registered service must take only DI-legal ctor parameters; " +
            "add new violators to the allowlist only as a temporary measure.\n" +
            "Current violations:\n  " + string.Join("\n  ", newViolations));
    }

    [Fact]
    public void Sanity_TheScanFindsTargetClasses_ScopeIsNotEmpty()
    {
        var services = BuildServerLikeServices();
        var targetClasses = services
            .Select(d => d.ImplementationType)
            .Where(t => t is not null && IsTargetType(t))
            .ToList();

        targetClasses.Should().HaveCountGreaterThan(20,
            "the scan must hit a meaningful slice of the codebase; an empty scope " +
            "would silently pass the main rule. Found target classes: " +
            string.Join(", ", targetClasses.Select(t => t!.Name).Distinct()));
    }

    [Fact]
    public void Rule_HasTeeth_SyntheticViolatorWithNonDiCtorParam_GetsFlagged()
    {
        var services = BuildServerLikeServices();
        services.AddSingleton<SyntheticBadService>();
        var registeredTypes = CollectRegisteredTypes(services);

        var violations = new List<string>();
        foreach (var descriptor in services)
        {
            var implType = descriptor.ImplementationType;
            if (implType is null) continue;
            if (implType != typeof(SyntheticBadService)) continue; // narrow scope to the synthetic
            CollectViolations(implType, registeredTypes, violations);
        }

        violations.Should().Contain(v => v.Contains("SyntheticBadService") && v.Contains("apiKey"),
            "the rule must catch a public ctor parameter that's a bare primitive " +
            "and not registered in the DI container");
    }

    // A small bad-citizen used only to prove the rule has teeth. NOT
    // registered in production DI; appears only inside this test's scope.
    internal sealed class SyntheticBadService(string apiKey)
    {
        public string ApiKey => apiKey;
    }

    private static void CollectViolations(
        Type implType, HashSet<Type> registered, List<string> violations)
    {
        var ctor = SelectCtor(implType);
        if (ctor is null) return;
        foreach (var parameter in ctor.GetParameters())
        {
            if (IsDiLegal(parameter.ParameterType, registered)) continue;
            violations.Add($"{implType.FullName}::{parameter.ParameterType.FullName} {parameter.Name}");
        }
    }

    private static ConstructorInfo? SelectCtor(Type type)
    {
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (ctors.Length == 0) return null;
        // Pick the ctor with the most parameters — primary-ctor classes have one
        // public ctor; classes with overloads use the largest as the DI target.
        return ctors.OrderByDescending(c => c.GetParameters().Length).First();
    }

    private static bool IsDiLegal(Type parameterType, HashSet<Type> registered)
    {
        if (parameterType.IsInterface) return true;
        if (IsOptionsWrapper(parameterType)) return true;
        if (IsLogger(parameterType)) return true;
        if (FrameworkWellKnowns.Contains(parameterType)) return true;
        if (registered.Contains(parameterType)) return true;
        if (parameterType.IsGenericType
            && registered.Contains(parameterType.GetGenericTypeDefinition()))
            return true;
        return false;
    }

    private static bool IsOptionsWrapper(Type t)
        => t.IsGenericType && OptionsGenerics.Contains(t.GetGenericTypeDefinition());

    private static bool IsLogger(Type t)
        => t == typeof(ILogger)
            || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILogger<>));

    private static readonly HashSet<Type> OptionsGenerics = new()
    {
        typeof(IOptions<>), typeof(IOptionsMonitor<>), typeof(IOptionsSnapshot<>),
    };

    private static readonly HashSet<Type> FrameworkWellKnowns = new()
    {
        typeof(HttpClient),
        typeof(TimeProvider),
        typeof(IServiceProvider),
        typeof(IConfiguration),
        typeof(IServiceScopeFactory),
        typeof(ILoggerFactory),
    };

    private static HashSet<Type> CollectRegisteredTypes(IServiceCollection services)
    {
        var set = new HashSet<Type>();
        foreach (var descriptor in services)
        {
            set.Add(descriptor.ServiceType);
            if (descriptor.ImplementationType is not null)
                set.Add(descriptor.ImplementationType);
        }
        return set;
    }

    private static bool IsTargetType(Type t)
    {
        var asm = t.Assembly.GetName().Name;
        return asm is not null
            && TargetAssemblyPrefixes.Any(p => asm.Equals(p, StringComparison.Ordinal));
    }

    // Mirrors ServerDiLifetimeTests.BuildServerLikeServices — single source of
    // truth for "the full Server DI graph" would be nice, but the existing
    // copy is internal to that test and the rule needs the same shape here.
    private static IServiceCollection BuildServerLikeServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton(new ServerContext("/tmp/agentsmith.yml"));
        AddNullRedisStack(services);
        services.AddSingleton(Mock.Of<IJobSpawner>());
        var configuration = new ConfigurationBuilder().Build();
        services.AddCoreDispatcherServices()
                .AddServerCompositionOverrides()
                .AddSandbox()
                .AddSandboxOptions(configuration)
                .AddSandboxGlobalConfig()
                .AddOrchestratorGlobalConfig()
                .AddSlackAdapter()
                .AddTeamsAdapter()
                .AddIntentHandlers()
                .AddWebhookHandlers()
                .AddLongRunningServices();
        services.AddJobSpawnerOptions(configuration);
        return services;
    }

    private static void AddNullRedisStack(IServiceCollection services)
    {
        services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        services.AddSingleton<IRedisJobQueue, NullRedisJobQueue>();
        services.AddSingleton(Mock.Of<IRedisClaimLock>());
        services.AddSingleton<IRedisLeaderLease, NullRedisLeaderLease>();
        services.AddSingleton<IJobHeartbeatService, NullJobHeartbeatService>();
        services.AddSingleton<IConversationLookup, NullConversationLookup>();
        services.AddSingleton(Mock.Of<AgentSmith.Contracts.Dialogue.IDialogueTransport>());
        services.AddSingleton(Mock.Of<IProgressReporter>());
    }
}

using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// Turns a <see cref="BootPlan"/> into the one composition step the server exposes for a
/// test: what this case has instead of what it does without. Applied to the real service
/// collection, after the real composition, immediately before the container is built.
/// </summary>
internal static class BootSubstitutions
{
    /// <summary>Hosted services registered by the product, which a case opts into by name.</summary>
    private const string ProductAssemblyPrefix = "AgentSmith";

    internal static Action<IServiceCollection> For(BootPlan plan) => services =>
    {
        SubstituteTransport(plan, services);
        SubstituteClock(plan, services);
        SelectHostedServices(plan, services);
    };

    /// <summary>A case that asserts on a duration supplies the clock that measures it.</summary>
    private static void SubstituteClock(BootPlan plan, IServiceCollection services)
    {
        if (plan.Clock is null) return;
        services.RemoveAll<TimeProvider>();
        services.AddSingleton(plan.Clock);
    }

    /// <summary>
    /// A case that does not assert on a broken transport gets one that answers. A case that
    /// does keeps the registration the server composed, pointed at nothing.
    /// </summary>
    private static void SubstituteTransport(BootPlan plan, IServiceCollection services)
    {
        if (plan.UnreachableRedis is not null) return;
        services.RemoveAll<IConnectionMultiplexer>();
        services.AddSingleton(InMemoryRedis.Connection());
    }

    /// <summary>
    /// The host resolves EVERY hosted service before it starts any, so one that drags a
    /// connection into its constructor makes the whole boot wait. The listener is not one of
    /// the product's own and stays: without it there is no server to assert against.
    /// </summary>
    private static void SelectHostedServices(BootPlan plan, IServiceCollection services)
    {
        foreach (var descriptor in services.Where(IsProductHostedService).ToList())
            services.Remove(descriptor);
        foreach (var type in plan.HostedServices)
            services.AddSingleton(typeof(IHostedService), type);
    }

    private static bool IsProductHostedService(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && OwningAssembly(descriptor)?.StartsWith(ProductAssemblyPrefix, StringComparison.Ordinal) == true;

    private static string? OwningAssembly(ServiceDescriptor descriptor) =>
        (descriptor.ImplementationType
         ?? descriptor.ImplementationInstance?.GetType()
         ?? descriptor.ImplementationFactory?.Method.DeclaringType)
        ?.Assembly.GetName().Name;
}

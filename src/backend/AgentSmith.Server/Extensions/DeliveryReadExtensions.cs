using AgentSmith.Server.Services.Delivery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-25-0099b: the delivery read model — computed over the runs on every request, nothing stored. Its two
/// repositories are registered with the relational store they read.
/// </summary>
internal static class DeliveryReadExtensions
{
    internal static IServiceCollection AddDeliveryReads(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services.AddTransient<DeliveryWindowFactory>()
            .AddTransient<DeliveryAttemptOutcome>()
            .AddTransient<DeliveryTicketComposer>()
            .AddTransient<DeliveryFiguresCalculator>()
            .AddTransient<DeliveryProjectRows>()
            .AddTransient<DeliveryEndCauseCounter>()
            .AddTransient<DeliveryTicketViews>()
            .AddTransient<DeliveryReader>();
    }
}

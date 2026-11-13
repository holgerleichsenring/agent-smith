using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>The harness's event publisher, failing on the run's end — after the run was named on the row.</summary>
internal sealed class ThrowingOnRunFinished(IEventPublisher inner) : IEventPublisher
{
    public const string Message = "the event store went away";

    public static void Install(IServiceCollection services)
    {
        var registered = services.Last(d => d.ServiceType == typeof(IEventPublisher));
        services.Remove(registered);
        services.AddSingleton<IEventPublisher>(provider => new ThrowingOnRunFinished(Inner(registered, provider)));
    }

    public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        runEvent is RunFinishedEvent ? throw new InvalidOperationException(Message) : inner.PublishAsync(runEvent, cancellationToken);

    private static IEventPublisher Inner(ServiceDescriptor registered, IServiceProvider provider) =>
        (IEventPublisher)(registered.ImplementationInstance
            ?? registered.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, registered.ImplementationType!));
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// 2026-11-22-747db: how every product worker reaches the host. The host resolves all hosted services and awaits each
/// StartAsync BEFORE Kestrel binds, so a worker whose constructor pulled in the Redis multiplexer (a synchronous
/// connect: 5.8 s refused, 15.8 s blackholed, measured) or whose StartAsync read Redis held the port shut. This
/// launcher's StartAsync does nothing but wait for ApplicationStarted — the listener is up — and only then builds the
/// worker (the registered singleton when there is one, never a second copy) and starts it on the thread pool.
/// <para>
/// A worker that cannot be built or started, or whose ExecuteAsync faults later, is logged here: behind a launcher
/// the host no longer sees it, and BackgroundServiceExceptionBehavior.Ignore would leave no trace.
/// </para>
/// </summary>
public sealed class WorkerLauncher<T>(
    IServiceProvider services, ILogger<WorkerLauncher<T>> logger, IHostApplicationLifetime? lifetime = null)
    : IHostedService, IDisposable where T : class, IHostedService
{
    private readonly CancellationTokenSource _stopping = new();
    private CancellationTokenRegistration _started;
    private volatile Task? _launch;
    private volatile T? _worker;
    private bool _owned;

    /// <summary>The worker once it started; null before ApplicationStarted or when it could not start.</summary>
    public T? Worker => _worker;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Outside a host (a composition test) there is no listener to wait for: launch at once, still off this call.
        if (lifetime is null) _launch = Launch();
        else _started = lifetime.ApplicationStarted.Register(() => _launch = Launch());
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _started.DisposeAsync();
        await _stopping.CancelAsync();
        if (_launch is { } launch) await WaitAsync(launch, cancellationToken);
        if (_worker is { } worker) await worker.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        _started.Dispose();
        _stopping.Dispose();
        // A worker the container does not hold was built here, so it is disposed here.
        if (_owned && _worker is IDisposable owned) owned.Dispose();
    }

    // A dedicated thread, not the pool: some thirty workers are built at once, and a constructor that waits (the
    // multiplexer's synchronous connect, or the DI lock around it) must not starve the threads Kestrel answers on.
    private Task Launch() =>
        Task.Factory.StartNew(LaunchAsync, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .Unwrap();

    private async Task LaunchAsync()
    {
        try
        {
            var worker = Build();
            await worker.StartAsync(_stopping.Token);
            // When StopAsync gave up waiting for this start, nobody else will stop it.
            if (_stopping.IsCancellationRequested) await worker.StopAsync(CancellationToken.None);
            else Publish(worker);
        }
        catch (Exception ex) when (_stopping.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Worker {Worker} was stopped while it started", typeof(T).Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worker {Worker} could not start; the server runs without it", typeof(T).Name);
        }
    }

    private T Build()
    {
        var registered = services.GetService<T>();
        _owned = registered is null;
        return registered ?? ActivatorUtilities.CreateInstance<T>(services);
    }

    private void Publish(T worker)
    {
        _worker = worker;
        if (worker is BackgroundService { ExecuteTask: { } running }) _ = WatchAsync(running);
    }

    private async Task WatchAsync(Task running)
    {
        try
        {
            await running;
        }
        catch (Exception ex) when (!_stopping.IsCancellationRequested)
        {
            logger.LogError(ex, "Worker {Worker} stopped with a fault; the server runs without it", typeof(T).Name);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Worker {Worker} ended while the server stopped", typeof(T).Name);
        }
    }

    private async Task WaitAsync(Task launch, CancellationToken cancellationToken)
    {
        try
        {
            await launch.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Worker {Worker} was still starting when the stop deadline passed", typeof(T).Name);
        }
    }
}

using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Services;

/// <summary>
/// p0201: dedicated System.Threading.Timer that writes the sandbox heartbeat
/// key every <see cref="WriteInterval"/> with TTL <see cref="KeyTtl"/>. The
/// timer fires on a thread-pool thread independent of the step-executor task,
/// so a blocking step (compile, test run, GC pause &gt;5s) never delays the
/// write past the TTL. On <see cref="Stop"/> the key is deleted so the
/// server-side watcher reports "clean exit" not "vanished".
/// </summary>
internal sealed class HeartbeatLoop : IStepInFlightMarker, IAsyncDisposable
{
    public static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan KeyTtl = TimeSpan.FromSeconds(10);

    private readonly IDatabase _database;
    private readonly string _jobId;
    private readonly string _heartbeatKey;
    private readonly int _processId;
    private readonly ILogger _logger;
    private Timer? _timer;
    private long _stepInFlight;
    private long _disposed;

    public HeartbeatLoop(IConnectionMultiplexer multiplexer, string jobId, ILogger logger)
    {
        _database = multiplexer.GetDatabase();
        _jobId = jobId;
        _heartbeatKey = RedisKeys.HeartbeatKey(jobId);
        _processId = Environment.ProcessId;
        _logger = logger;
    }

    public void Start()
    {
        _timer = new Timer(OnTick, state: null, dueTime: TimeSpan.Zero, period: WriteInterval);
        _logger.LogInformation(
            "HeartbeatLoop started for job {JobId} (interval={Interval}, ttl={Ttl})",
            _jobId, WriteInterval, KeyTtl);
    }

    public void MarkStepInFlight(bool inFlight) =>
        Interlocked.Exchange(ref _stepInFlight, inFlight ? 1 : 0);

    private void OnTick(object? _)
    {
        if (Interlocked.Read(ref _disposed) == 1) return;
        try
        {
            var payload = BuildPayload();
            // Fire-and-forget: the timer thread must not block; StackExchange
            // pipelines the write. Failure surfaces as a missed heartbeat,
            // which is the exact signal the server-side watcher cares about.
            _ = _database.StringSetAsync(_heartbeatKey, payload, KeyTtl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HeartbeatLoop tick failed for job {JobId}", _jobId);
        }
    }

    private string BuildPayload() => JsonSerializer.Serialize(new HeartbeatPayload(
        Ts: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Pid: _processId,
        StepInFlight: Interlocked.Read(ref _stepInFlight) == 1));

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        if (_timer is not null) await _timer.DisposeAsync();
        try { await _database.KeyDeleteAsync(_heartbeatKey); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HeartbeatLoop cleanup delete failed for job {JobId}", _jobId);
        }
    }

    private sealed record HeartbeatPayload(long Ts, int Pid, bool StepInFlight);
}

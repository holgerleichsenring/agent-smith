namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Consumer-side queue configuration for the PipelineQueueConsumer.
/// MaxParallelJobs is the sole backpressure knob — webhook receivers never block,
/// the consumer throttles downstream pipeline execution.
/// </summary>
public sealed class QueueConfig
{
    public int MaxParallelJobs { get; set; } = 4;
    public int ConsumeBlockSeconds { get; set; } = 5;
    public int ShutdownGraceSeconds { get; set; } = 30;

    // Server subsystems poll IConnectionMultiplexer.IsConnected at this cadence
    // when Redis is configured but unreachable. Default 30s — short enough that ops
    // sees recovery quickly, long enough that logs stay quiet during outages (p0101).
    public int RedisRetryIntervalSeconds { get; set; } = 30;
}

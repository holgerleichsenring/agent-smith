using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-85e2: how many times the compactor folded the middle of this run's conversation. A tool that answers "it
/// stands above" asks first whether a fold happened since, because a fold summarizes what stood above.
/// </summary>
public sealed class TurnFolds
{
    private const string Key = "TurnFolds";
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Fold() => Interlocked.Increment(ref _count);

    /// <summary>The run's counter, created on first use.</summary>
    public static TurnFolds Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<TurnFolds>(Key, out var folds) && folds is not null) return folds;
        folds = new TurnFolds();
        pipeline.Set(Key, folds);
        return folds;
    }
}

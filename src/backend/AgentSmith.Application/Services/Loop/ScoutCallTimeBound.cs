namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-17-274ca: the wall-clock bound of one scout call — the client call-site reading and the
/// registry-auth staging. A call that outlives it ends with a <see cref="TimeoutException"/> whose
/// message names the value, the reason and the key, where it used to end as a bare cancelled task.
/// A cancellation from outside stays an <see cref="OperationCanceledException"/>.
/// </summary>
public static class ScoutCallTimeBound
{
    public const string Key = "limits.max_seconds_per_skill_call";

    public static async Task<T> RunAsync<T>(
        int seconds, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            return await call(bound.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(Passed(seconds), ex);
        }
    }

    public static string Passed(int seconds) =>
        $"the call passed {Key} = {seconds} s, the wall-clock bound of one scout call; "
        + "raise it in the limits settings if the reading needs longer";
}

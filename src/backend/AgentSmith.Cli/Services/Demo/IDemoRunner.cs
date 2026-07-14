namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: the demo's pipeline leg — materialize the sample workspace, run
/// fix-bug headless against it, present the result. Behind an interface so
/// DemoExecutor's preflight gate is unit-testable (a failing preflight must
/// prove this was never invoked — zero pipeline tokens spent).
/// </summary>
internal interface IDemoRunner
{
    Task<int> RunAsync(DemoInvocation invocation, TextWriter output, CancellationToken cancellationToken);
}

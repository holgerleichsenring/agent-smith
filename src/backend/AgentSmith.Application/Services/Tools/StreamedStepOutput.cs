using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The live output feed of one step, collected into two bounded buffers.
/// <para>
/// 2026-10-07-6b9db: each stream is its own <see cref="StreamCapture"/> — head, rolling tail,
/// count and flag — so a stdout that fills its buffer no longer stops stderr, nor the reverse.
/// </para>
/// <para>
/// Synchronous by construction: <c>Progress&lt;T&gt;</c> dispatches asynchronously via the
/// captured SynchronizationContext / ThreadPool, which races the awaited run — events can
/// arrive after the sandbox returns and end up missing from the labeled-section output. The
/// inline sync collector closes that race.
/// </para>
/// <para>
/// 2026-09-22-46ef: extracted from <see cref="SandboxStepRunner"/>, which now has two ways to
/// send a step (a program with its arguments, and a model-authored shell command) and should
/// not also own how a stream is buffered.
/// </para>
/// </summary>
internal sealed class StreamedStepOutput
{
    public StreamCapture Stdout { get; } = new();
    public StreamCapture Stderr { get; } = new();

    /// <summary>The one collector to hand a step — built once, so two reads cannot buffer
    /// into the same instance through two different sinks.</summary>
    public IProgress<StepEvent> Collector { get; }

    public StreamedStepOutput() => Collector = new SyncProgress<StepEvent>(ev =>
    {
        switch (ev.Kind)
        {
            case StepEventKind.Stdout: Stdout.Append(ev.Line); break;
            case StepEventKind.Stderr: Stderr.Append(ev.Line); break;
        }
    });
}

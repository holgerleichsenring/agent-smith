using System.Text;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The live output feed of one step, collected into two bounded buffers.
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
    private const string TruncationNotice = "\n... (output truncated at 1 MB)";

    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();

    public bool Truncated { get; private set; }
    public string Stdout => _stdout.ToString();
    public string Stderr => _stderr.ToString();

    /// <summary>The one collector to hand a step — built once, so two reads cannot buffer
    /// into the same instance through two different sinks.</summary>
    public IProgress<StepEvent> Collector { get; }

    public StreamedStepOutput() => Collector = new SyncProgress<StepEvent>(ev =>
    {
        switch (ev.Kind)
        {
            case StepEventKind.Stdout: Append(_stdout, ev.Line); break;
            case StepEventKind.Stderr: Append(_stderr, ev.Line); break;
        }
    });

    private void Append(StringBuilder sb, string line)
    {
        if (Truncated) return;
        var addedBytes = Encoding.UTF8.GetByteCount(line) + 1;
        if (sb.Length + addedBytes > SizeLimits.RunCommandMaxBufferBytes)
        {
            Truncated = true;
            sb.Append(TruncationNotice);
            return;
        }
        sb.Append(line).Append('\n');
    }
}

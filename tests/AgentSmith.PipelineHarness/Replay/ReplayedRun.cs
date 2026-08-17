using AgentSmith.Application.Services.Trace;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Replay;

/// <summary>
/// p0427: the real composition driven by a RECORDED run — every model call answered from
/// the recording, everything else (parsing, tools, sandbox, the delivery gate) for real.
/// <para>
/// This is the instrument the 45 runs of 2026-08-14..16 did not have: the defects they
/// found were all deterministic and local, and none of them needed a model to be detected.
/// </para>
/// </summary>
public sealed class ReplayedRun : IAsyncDisposable
{
    public RealCompositionHarness Harness { get; }
    public ReplayChatClient Client { get; }
    public PipelineRunner Runner { get; }

    private ReplayedRun(RealCompositionHarness harness, ReplayChatClient client)
    {
        Harness = harness;
        Client = client;
        Runner = new PipelineRunner(harness.Services);
    }

    public static ReplayedRun Of(RecordedTrace trace, string? configPath = null)
    {
        var client = new ReplayChatClient(trace);
        var harness = RealCompositionHarness.Build(
            configPath ?? FixturePaths.For(FixturePaths.Default),
            services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddSingleton<IChatClientFactory>(new ReplayChatClientFactory(client));
            });
        return new ReplayedRun(harness, client);
    }

    /// <summary>Every path the replayed run wrote into a sandbox.</summary>
    public IReadOnlyList<string> WrittenPaths =>
        [.. Harness.StubSandboxFactory!.Spawned
            .SelectMany(s => s.Sandbox.RanSteps)
            .Where(s => s.Kind == Sandbox.Wire.StepKind.WriteFile && s.Path is not null)
            .Select(s => s.Path!)];

    public ValueTask DisposeAsync() => Harness.DisposeAsync();
}

using System.Net;
using AgentSmith.Server.Contracts;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-24-035a: /health/ready is the readiness probe. It holds traffic back until the first startup
/// probe pass has run (or its budget is spent), while /health — liveness — answers from the first moment.
/// </summary>
[Trait("TestProcess", "env-3")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ReadinessEndpointTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task Ready_BeforeTheFirstPass_Answers503AndLivenessStillAnswers()
    {
        var runner = new HeldRunner();
        await using var server = await BootedServer.StartAsync(Plan() with
        {
            Services = s => { s.RemoveAll<IStartupProbeRunner>(); s.AddSingleton<IStartupProbeRunner>(runner); },
        });

        var ready = await server.Client.GetAsync("/health/ready");
        var live = await server.Client.GetAsync("/health");

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ready.Content.ReadAsStringAsync()).Should().Contain("StartupProbePass.ReadBudget");
        live.StatusCode.Should().Be(HttpStatusCode.OK, "liveness never waits for the probes");
        runner.Release();
    }

    [Fact]
    public async Task Ready_AfterTheFirstPass_Answers200()
    {
        var runner = new HeldRunner();
        await using var server = await BootedServer.StartAsync(Plan() with
        {
            Services = s => { s.RemoveAll<IStartupProbeRunner>(); s.AddSingleton<IStartupProbeRunner>(runner); },
        });

        runner.Release();
        await runner.Finished;

        // The pass is completed right after the runner returns, so the read polls for that one step.
        (await TestWaits.ReachedAsync(async () =>
                (await server.Client.GetAsync("/health/ready")).StatusCode == HttpStatusCode.OK))
            .Should().BeTrue("the first pass has run");
    }

    private BootPlan Plan()
    {
        var dbPath = Temp(".db");
        var configPath = Temp(".yml");
        File.WriteAllText(configPath, $"""
            persistence:
              provider: sqlite
              connection_string: Data Source={dbPath}

            """);
        return new BootPlan(configPath);
    }

    private sealed class HeldRunner : IStartupProbeRunner
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Finished => _finished.Task;
        public void Release() => _gate.TrySetResult();

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            try { await _gate.Task.WaitAsync(cancellationToken); }
            finally { _finished.TrySetResult(); }
        }
    }

    private string Temp(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-ready-{Guid.NewGuid():N}{extension}");
        _tempFiles.AddRange([path, path + "-wal", path + "-shm"]);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }
}

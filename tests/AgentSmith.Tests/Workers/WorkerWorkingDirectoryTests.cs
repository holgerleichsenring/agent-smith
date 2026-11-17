using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Workers;
using FluentAssertions;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Workers;

/// <summary>2026-11-17-166ac: the worker CLI runs in its own empty directory under the temp directory — never the
/// temp directory itself, which holds every sandbox clone — and AGENTSMITH_WORKER_CWD still overrides it.</summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class WorkerWorkingDirectoryTests
{
    [Fact]
    public void CliOptions_WorkingDirectory_IsTheWorkersOwnDirectoryUnderTemp()
    {
        using var _ = new CwdOverride(null);

        var directory = Options().WorkingDirectory;

        directory.Should().Be(Path.Combine(Path.GetTempPath(), ExternalWorkerCliOptionsFactory.WorkingDirectoryName));
        Directory.Exists(directory).Should().BeTrue("the factory creates it");
        Path.GetFullPath(directory).TrimEnd('/').Should().NotBe(Path.GetFullPath(Path.GetTempPath()).TrimEnd('/'));
    }

    [Fact]
    public void CliOptions_EnvironmentOverride_Wins()
    {
        using var _ = new CwdOverride("/opt/worker-cwd");

        Options().WorkingDirectory.Should().Be("/opt/worker-cwd");
    }

    [Fact]
    public async Task Run_TheCliProcess_StartsInTheWorkersDirectory()
    {
        if (OperatingSystem.IsWindows()) return;
        using var _ = new CwdOverride(null);
        var options = Options() with { Binary = "/bin/sh", Arguments = ["-c", "pwd -P"] };

        var result = await new AgentCliWorkerProcessRunner(NullLogger<AgentCliWorkerProcessRunner>.Instance)
            .RunAsync("{}", options, CancellationToken.None);

        // macOS resolves /var to /private/var, so both sides are compared resolved.
        result.StandardOutput.Trim().Should().Be(Resolve(options.WorkingDirectory));
    }

    private static string Resolve(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd('/');
        return OperatingSystem.IsMacOS() && full.StartsWith("/var/", StringComparison.Ordinal) ? "/private" + full : full;
    }

    private static ExternalWorkerCliOptions Options() =>
        new ExternalWorkerCliOptionsFactory().Create(new AgentConfig { Type = "external_worker" }, new ModelAssignment());

    private sealed class CwdOverride : IDisposable
    {
        private readonly string? _previous = Environment.GetEnvironmentVariable(ExternalWorkerCliOptionsFactory.WorkingDirectoryEnv);

        public CwdOverride(string? value) =>
            Environment.SetEnvironmentVariable(ExternalWorkerCliOptionsFactory.WorkingDirectoryEnv, value);

        public void Dispose() =>
            Environment.SetEnvironmentVariable(ExternalWorkerCliOptionsFactory.WorkingDirectoryEnv, _previous);
    }
}

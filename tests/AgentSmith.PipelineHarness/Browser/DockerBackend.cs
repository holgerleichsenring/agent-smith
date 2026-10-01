using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.PipelineHarness.Browser;

/// <summary>
/// 2026-10-01-283de: the production Docker spawner over the harness's sandbox network and Redis,
/// configured from the same environment the liveness tier reads, plus the two images to run.
/// </summary>
internal sealed record DockerBackend(DockerSandboxFactory Factory)
{
    public static string AgentImage => Environment.GetEnvironmentVariable("HARNESS_AGENT_IMAGE") ?? "agent-smith-sandbox-agent:latest";
    public static string BrowserImage => Environment.GetEnvironmentVariable("HARNESS_BROWSER_IMAGE") ?? "agent-smith-sandbox-browser:latest";
    private static string SandboxRedisUrl => Environment.GetEnvironmentVariable("HARNESS_SANDBOX_REDIS_URL") ?? "redis:6379";
    public static string RedisHostName => SandboxRedisUrl.Split(':')[0];

    public static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "version.txt"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("repository root (version.txt) not found");
        }
    }

    public static async Task<DockerBackend> BuildAsync()
    {
        var options = new DockerSandboxOptions
        {
            RedisUrl = SandboxRedisUrl,
            DockerSocketUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock",
            Network = Environment.GetEnvironmentVariable("HARNESS_SANDBOX_NETWORK") ?? "deploy_default",
        };
        var docker = new DockerClientConfiguration(new Uri(options.DockerSocketUri)).CreateClient();
        var redis = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379");
        redis.AbortOnConnectFail = false;
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(redis);
        var logs = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        return new DockerBackend(new DockerSandboxFactory(docker, multiplexer,
            new DockerContainerSpecBuilder(new SandboxOwnerIdentity("browser-harness")), options,
            new DockerPackageCaches(docker, options, logs.CreateLogger<DockerPackageCaches>()),
            new DockerImagePresence(docker, logs.CreateLogger<DockerImagePresence>()),
            Options.Create(new SandboxGlobalConfig { StepTimeoutSeconds = 300 }),
            new WireProtocolWatcher(new StartupFindings(), logs.CreateLogger<WireProtocolWatcher>()), logs));
    }
}

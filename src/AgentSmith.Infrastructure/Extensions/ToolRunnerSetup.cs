using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure;

internal static class ToolRunnerSetup
{
    internal static IToolRunner CreateToolRunner(ToolRunnerConfig config, IServiceProvider sp)
    {
        var type = config.Type.ToLowerInvariant();

        if (type == "auto")
            type = DetectToolRunnerType();

        return type switch
        {
            "docker" or "podman" => new DockerToolRunner(config,
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<DockerToolRunner>()),
            "process" => new ProcessToolRunner(
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<ProcessToolRunner>()),
            _ => new DockerToolRunner(config,
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<DockerToolRunner>()),
        };
    }

    internal static string DetectToolRunnerType()
    {
        // Check for Docker socket
        if (File.Exists("/var/run/docker.sock"))
            return "docker";

        // Check for Podman socket (rootful)
        if (File.Exists("/run/podman/podman.sock"))
            return "podman";

        // Check for Podman socket (rootless)
        var uid = Environment.GetEnvironmentVariable("UID") ?? "1000";
        if (File.Exists($"/run/user/{uid}/podman/podman.sock"))
            return "podman";

        // Fallback to process
        return "process";
    }

    internal static ToolRunnerConfig LoadToolRunnerConfig()
    {
        var path = Path.Combine("config", "agentsmith.yml");
        if (!File.Exists(path))
            return new ToolRunnerConfig();

        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var wrapper = deserializer.Deserialize<ToolRunnerConfigWrapper>(yaml);
            return wrapper?.ToolRunner ?? new ToolRunnerConfig();
        }
        catch
        {
            return new ToolRunnerConfig();
        }
    }

    private sealed class ToolRunnerConfigWrapper
    {
        public ToolRunnerConfig? ToolRunner { get; set; }
    }
}

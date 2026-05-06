using System.CommandLine;
using AgentSmith.Sandbox.Agent.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Sandbox.Agent;

internal static class Program
{
    public const int ExitUnhandledError = 3;

    private static async Task<int> Main(string[] args)
    {
        var redisUrlOption = new Option<string?>("--redis-url",
            getDefaultValue: () => Environment.GetEnvironmentVariable("REDIS_URL"),
            description: "Redis connection string (default from REDIS_URL env)");
        var jobIdOption = new Option<string?>("--job-id", "Sandbox job identifier");
        var verboseOption = new Option<bool>("--verbose", "Enable Debug-level logging");
        var injectOption = new Option<string?>("--inject",
            "Self-copy the binary to the given path and exit (init-container mode)");

        var root = new RootCommand("AgentSmith.Sandbox.Agent — Redis-driven sandbox worker")
        {
            redisUrlOption, jobIdOption, verboseOption, injectOption
        };
        root.SetHandler(async (redisUrl, jobId, verbose, injectTarget) =>
        {
            using var loggerFactory = BuildLoggerFactory(verbose);
            try
            {
                Environment.ExitCode = await DispatchAsync(redisUrl, jobId, injectTarget, loggerFactory);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Program").LogError(ex, "Unhandled error");
                Environment.ExitCode = ExitUnhandledError;
            }
        }, redisUrlOption, jobIdOption, verboseOption, injectOption);

        await root.InvokeAsync(args);
        return Environment.ExitCode;
    }

    private static async Task<int> DispatchAsync(
        string? redisUrl, string? jobId, string? injectTarget, ILoggerFactory loggerFactory)
    {
        if (!string.IsNullOrEmpty(injectTarget))
        {
            return new BinaryInjector(loggerFactory.CreateLogger<BinaryInjector>()).Inject(injectTarget);
        }
        if (string.IsNullOrEmpty(redisUrl) || string.IsNullOrEmpty(jobId))
        {
            loggerFactory.CreateLogger("Program").LogError("--redis-url and --job-id are required in run mode");
            return JobLoop.ExitIdleTimeout;
        }
        return await RunLoopAsync(redisUrl, jobId, loggerFactory);
    }

    private static async Task<int> RunLoopAsync(string redisUrl, string jobId, ILoggerFactory loggerFactory)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await using var bus = await RedisJobBus.ConnectAsync(redisUrl,
            loggerFactory.CreateLogger<RedisJobBus>(), cts.Token);
        var processRunner = new ProcessRunner();
        var fileHandler = new FileStepHandler(loggerFactory.CreateLogger<FileStepHandler>());
        var grepHandler = new GrepStepHandler(processRunner, loggerFactory.CreateLogger<GrepStepHandler>());
        var executor = new StepExecutor(processRunner, fileHandler, grepHandler, loggerFactory.CreateLogger<StepExecutor>());
        var loop = new JobLoop(bus, executor, loggerFactory.CreateLogger<JobLoop>());
        return await loop.RunAsync(jobId, cts.Token);
    }

    private static ILoggerFactory BuildLoggerFactory(bool verbose) =>
        LoggerFactory.Create(builder => builder
            .AddConsole()
            .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information));
}

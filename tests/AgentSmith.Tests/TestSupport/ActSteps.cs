using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Services.Tickets.Closing;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-17-e061a: an act's run in process — the preset's REAL steps, built by the real context builders and
/// dispatched by the real command executor (the builders listed here, which need nothing) over <paramref name="services"/>, on a context seeded the way
/// <see cref="ActRun"/> seeds it, without the run lifecycle. What the act suites drive in place of a server write;
/// the real composition's run is proven in the pipeline harness.
/// </summary>
internal sealed class ActSteps(AgentSmithConfig config, IServiceProvider services) : IActRun
{
    /// <summary>
    /// The act steps over a provider of their own: the real context builders and act handlers, the server's close
    /// failure class, a quiet log and clock, and loose mocks for every host and store <paramref name="register"/>
    /// leaves out. <paramref name="register"/> adds the ones a suite watches.
    /// </summary>
    public static ActSteps Over(AgentSmithConfig config, Action<IServiceCollection> register)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddTicketActPipelines();
        services.AddSingleton<ITicketCloseFailureClassifier, TicketCloseFailureClassifier>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Mock.Of<ISourceProviderFactory>()).AddSingleton(Mock.Of<ITicketClosedMarkStore>());
        services.AddSingleton(Mock.Of<IReworkNudges>()).AddSingleton(Mock.Of<ITicketActivityNudge>());
        register(services);
        return new ActSteps(config, services.BuildServiceProvider());
    }

    private static readonly KeyedContextBuilder[] Builders =
    [
        new(CommandNames.CloseTicket, new CloseTicketContextBuilder()),
        new(CommandNames.ReopenTicket, new ReopenTicketContextBuilder()),
    ];

    /// <summary>Every preset this double ran, in order.</summary>
    public List<string> Runs { get; } = [];

    public async Task<CommandResult> RunAsync(string project, string pipeline, IReadOnlyDictionary<string, object> seeds)
    {
        lock (Runs) Runs.Add(pipeline);
        if (LiveConfig.Projects(config).Of(project) is not { } resolved)
            return CommandResult.Fail($"Project '{project}' not found in configuration.");
        var context = new PipelineContext();
        foreach (var (key, value) in seeds) context.Set(key, value);
        context.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, resolved.Repos);
        var factory = new CommandContextFactory(Builders);
        await using var scope = services.CreateAsyncScope();
        var executor = new CommandExecutor(scope.ServiceProvider, NullLogger<CommandExecutor>.Instance);
        var result = CommandResult.Ok($"'{pipeline}' has no steps");
        foreach (var step in PipelinePresets.TryResolve(pipeline) ?? [])
        {
            result = await executor.ExecuteAsync(factory.Create(new PipelineCommand(step), resolved, context), CancellationToken.None);
            if (!result.IsSuccess) return result;
        }
        return result;
    }
}

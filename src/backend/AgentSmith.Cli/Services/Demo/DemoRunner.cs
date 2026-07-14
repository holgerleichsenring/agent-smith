using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Demo;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: the demo's pipeline leg. Materializes the embedded sample into a
/// local git workspace, then runs the REAL fix-bug preset against it headless
/// and in-process — inline ticket instead of a tracker, RepoType.Local
/// ephemeral project (the p0281d --agent seam) instead of a projects: entry,
/// local commit instead of a PR.
/// </summary>
internal sealed class DemoRunner(
    IConfigurationLoader configLoader,
    DemoWorkspaceMaterializer materializer,
    ExecutePipelineUseCase useCase,
    DemoResultPresenter presenter) : IDemoRunner
{
    public async Task<int> RunAsync(
        DemoInvocation invocation, TextWriter output, CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(invocation.ConfigPath);
        if (!TryResolveAgent(config, invocation.AgentName, output, out var agentName)) return 1;

        var workspace = await materializer.MaterializeAsync(
            invocation.WorkspaceDir ?? DefaultWorkspaceDir(), cancellationToken);
        output.WriteLine($"Demo workspace (local git repo, seeded bug): {workspace}");
        output.WriteLine($"Running the fix-bug pipeline with agent '{agentName}' — this takes a few minutes ...");

        var result = await useCase.ExecuteAsync(
            BuildRequest(agentName, workspace), invocation.ConfigPath, cancellationToken);

        await presenter.PresentAsync(result, workspace, output, cancellationToken);
        return result.IsSuccess ? 0 : 1;
    }

    // The ephemeral-project seam requires a real agents: entry; resolve the
    // explicit --agent or default to the first configured one.
    private static bool TryResolveAgent(
        AgentSmithConfig config, string? requested, TextWriter output, out string agentName)
    {
        agentName = requested ?? config.Agents.Keys.FirstOrDefault() ?? string.Empty;
        if (agentName.Length > 0 && config.Agents.ContainsKey(agentName)) return true;
        output.WriteLine(requested is null
            ? "The config has no agents: entry. Add one agent (type + model + API key) and re-run."
            : $"--agent '{requested}' is not in the agents: catalog. Known: [{string.Join(", ", config.Agents.Keys)}].");
        return false;
    }

    private static PipelineRequest BuildRequest(string agentName, string workspace) => new(
        ProjectName: "demo",
        PipelineName: "fix-bug",
        Headless: true,
        AgentName: agentName,
        InlineTicket: DemoTicket.Create(),
        Context: new Dictionary<string, object> { [ContextKeys.SourcePath] = workspace });

    private static string DefaultWorkspaceDir() => Path.Combine(
        Path.GetTempPath(), $"agentsmith-demo-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
}

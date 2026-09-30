using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// p0428: the run is standing on a real configuration, not the empty placeholder.
/// <para>
/// p0419's structural defect: the `run` verb built its DI container without the config
/// path, so eight handlers received <c>AgentSmithConfig.Empty()</c>. Nothing said so.
/// The run lost its private-feed credentials and spent a whole phase diagnosing a 401
/// that was never real. The placeholder is indistinguishable from a real config by
/// type — it is distinguishable by CONTENT, and one look is enough.
/// </para>
/// </summary>
public sealed class ConfiguredAgentCheck(AgentSmithConfig config) : IRunPreflightCheck
{
    private const string ConfigLever =
        "the run received an empty configuration — pass --config to the CLI verb "
        + "(the container's AGENTSMITH_CONFIG / the server's mounted agentsmith.yml)";

    public string Name => "config-loaded";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (config.Agents.Count == 0)
            return Task.FromResult(RunPreflightFinding.Fail(
                Name, "no agents are configured at all", ConfigLever));

        var agent = pipeline.Resolved().Agent;
        var model = ResolvedModel(agent);
        var missing = MissingFields(agent, model);
        return Task.FromResult(missing.Count == 0
            ? RunPreflightFinding.Pass(Name, $"agent '{agent.Type}' model '{model}'")
            : RunPreflightFinding.Fail(
                Name,
                $"the pipeline's agent is missing {string.Join(" and ", missing)}",
                "name a provider type on the agent this pipeline resolves to "
                + "(agents.<name>.type), and a model either as agents.<name>.model or as "
                + "agents.<name>.models.primary.model, or a declared catalog entry as "
                + "agents.<name>.models.primary.use"));
    }

    /// <summary>
    /// p0436: an agent carries its model as <c>model</c>, as <c>models.primary.model</c>, or
    /// both. The gate asks the one chain the runtime resolves through, so a shape the
    /// runtime accepts is never one the gate refuses, and the model it names is the one
    /// that will answer.
    /// </summary>
    private static string ResolvedModel(AgentConfig agent) =>
        new ModelRoleChain(agent).PrimaryModel;

    private static IReadOnlyList<string> MissingFields(AgentConfig agent, string? model)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(agent.Type)) missing.Add("its provider type");
        if (string.IsNullOrWhiteSpace(model)) missing.Add("its model");
        return missing;
    }
}

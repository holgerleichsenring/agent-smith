using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: env-credential client construction for the opt-in eval tier — the
/// same convention as the LiveLLM skill probe (Azure OpenAI first, public
/// OpenAI fallback). Returns null when no paid-API key is configured; the
/// eval test then skips loudly instead of failing.
/// </summary>
internal static class EvalChatClientEnv
{
    public static (IChatClient Client, string ModelId)? TryBuild()
    {
        return TryBuildAzure() ?? TryBuildOpenAi();
    }

    private static (IChatClient, string)? TryBuildAzure()
    {
        var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(deployment))
            return null;
        var model = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL") ?? "gpt-4.1";
        var agent = new AgentConfig { Type = "azure_openai", Endpoint = endpoint, Deployment = deployment };
        var assignment = new ModelAssignment { Model = model, Deployment = deployment };
        return (new OpenAiChatClientBuilder().Build(agent, assignment), model);
    }

    private static (IChatClient, string)? TryBuildOpenAi()
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1";
        var agent = new AgentConfig { Type = "openai" };
        var assignment = new ModelAssignment { Model = model };
        return (new OpenAiChatClientBuilder().Build(agent, assignment), model);
    }
}

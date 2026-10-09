using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-08-e8b9j: whether a conversation's model sees the images the operator uploaded — told
/// to the OPERATOR, because the model is told already and a person attaching a screenshot to a
/// text-only model would otherwise wait for an answer about it that cannot come.
/// <para>
/// TWO ANSWERS, because two paths carry images. An image attached through the Image entry rides
/// the user message, which needs only <c>agent.supports_vision</c>. An image INSIDE a set reaches
/// the model through view_reference_image, a tool result — which Copilot and the external worker
/// cannot follow with an image whatever the model sees. The agent's own type picks the builder.
/// </para>
/// </summary>
public sealed class DialogImageSight(IConfigurationLoader configLoader, IEnumerable<IChatClientBuilder> builders)
{
    public DialogImageSightView For(string project)
    {
        if (!configLoader.LoadConfig(DispatcherDefaults.ConfigPath).Projects.TryGetValue(project, out var resolved))
            return DialogImageSightView.Unknown;
        var agent = resolved.Agent;
        var afterTools = builders.FirstOrDefault(b => b.SupportedTypes.Contains(agent.Type, StringComparer.OrdinalIgnoreCase))
            ?.AcceptsImageAfterToolResult ?? true;
        return new DialogImageSightView(agent.SupportsVision, agent.SupportsVision && afterTools);
    }
}

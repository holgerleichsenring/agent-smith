using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Webhooks;

/// <summary>
/// PR-comment intent parser (p0146e). CommentIntentParser is stateless — slash regexes
/// + an IIntentParser delegate. Singleton so the singleton PR-comment webhook handlers
/// can take it as a constructor dependency without a scope mismatch. The transient
/// IIntentParser is captured once at construction; LlmIntentParser holds no mutable
/// state (only DI-resolved factories + logger), so capture is safe.
/// </summary>
public static class CommentIntentExtensions
{
    public static IServiceCollection AddWebhookCommentIntent(this IServiceCollection services)
    {
        services.AddSingleton<CommentIntentParser>();
        return services;
    }
}

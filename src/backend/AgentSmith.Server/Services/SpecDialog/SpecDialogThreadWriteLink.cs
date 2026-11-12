using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-12-92e9a: for the turn about to run, has its capture's thread written as it grows, each write in a scope of
/// its own because the turn's scope is in use around it. The returned writer is closed by the turn, on every exit.
/// </summary>
public sealed class SpecDialogThreadWriteLink(
    IServiceScopeFactory scopes, SpecDialogThreadStore threads, ILoggerFactory loggers)
{
    public SpecDialogThreadWriter Observe(ConversationState started, AgentConfig agent, ForwardedThreadCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var writer = new SpecDialogThreadWriter(threads.CanFold(agent),
            (messages, stewarded) => WriteAsync(started, messages, stewarded), loggers.CreateLogger<SpecDialogThreadWriter>());
        capture.Observe(writer);
        return writer;
    }

    // CancellationToken.None: a write of a cancelled turn is the work it keeps.
    private async Task WriteAsync(ConversationState started, IReadOnlyList<ChatMessage> messages, bool stewarded)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SpecDialogThreadStore>()
            .KeepGrowingAsync(started, messages, stewarded, CancellationToken.None);
    }
}

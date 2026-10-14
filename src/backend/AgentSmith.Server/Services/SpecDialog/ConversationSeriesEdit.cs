using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The bound ticket's series edited in its conversation by the turn's poster (<see cref="SeriesEditAct"/>). Each
/// edit runs in its own scope, apart from the turn's unit of work.
/// </summary>
public sealed class ConversationSeriesEdit(
    IServiceScopeFactory scopes, ResolvedProject project, string ticketId, string conversation, string poster) : ISeriesEdit
{
    public async Task<string> EditAsync(IReadOnlyList<SeriesEdit> edits, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SeriesEditAct>()
            .EditAsync(project, ticketId, conversation, poster, edits, cancellationToken);
    }
}

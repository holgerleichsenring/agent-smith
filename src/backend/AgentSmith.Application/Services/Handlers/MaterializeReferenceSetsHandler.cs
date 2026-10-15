using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283df: the step before SpecSequence that hands the master the uploaded material the approval
/// cites — see <see cref="ReferenceMaterializer"/>, which carries it.
/// <para>
/// 2026-10-14-dbcel: it CARRIES ON EVERY RUN, also when the keys are already published — they are checkpointed
/// and a resume re-runs this step in a fresh sandbox, so a published key does not mean the files are there.
/// </para>
/// </summary>
public sealed class MaterializeReferenceSetsHandler(ReferenceMaterializer materializer)
    : ICommandHandler<MaterializeReferenceSetsContext>
{
    internal const string NoneCited = ReferenceMaterializer.NoneCited;

    public Task<CommandResult> ExecuteAsync(
        MaterializeReferenceSetsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return materializer.CarryAsync(context.Pipeline, cancellationToken);
    }
}

using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-10-ccd9: the spec-dialog branch of a button click, over a test's store — by default with no
/// conversation in the thread, which is every click that belongs to something other than a spec dialog.
/// </summary>
internal static class SpecDialogButtons
{
    public static SpecDialogChatClick Over(SqliteConnection store, SpecDialogRouter? router = null) =>
        new(Sessions(store), new SpecDialogProposalClaim(MigratedStoreTemplate.Context(store)), router!);

    public static SpecDialogSessionManager Sessions(SqliteConnection store)
    {
        var repository = new SpecDialogSessionRepository(MigratedStoreTemplate.Context(store));
        return new SpecDialogSessionManager(repository, BoundSessionNudges.Quiet(repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
    }
}

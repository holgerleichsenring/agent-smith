using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-26-0f19b: the spec-dialog branch of a button click, over a test's store — by default with
/// no question open, which is every click that belongs to something other than a spec dialog.
/// </summary>
internal static class SpecDialogButtons
{
    public static SpecDialogButtonAnswer Over(
        SqliteConnection store, IDialogueTransport transport, IOpenDialogQuestions? open = null) =>
        new(Sessions(store), open ?? Mock.Of<IOpenDialogQuestions>(), transport);

    public static SpecDialogSessionManager Sessions(SqliteConnection store)
    {
        var repository = new SpecDialogSessionRepository(MigratedStoreTemplate.Context(store));
        return new SpecDialogSessionManager(repository, BoundSessionNudges.Quiet(repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
    }
}

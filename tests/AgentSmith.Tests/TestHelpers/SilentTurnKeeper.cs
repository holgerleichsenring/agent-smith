using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// The turn keeper for tests that are NOT about the digest: its digest writer runs on <see cref="SilentChatModel"/>'s
/// model, whose empty answer appends nothing. A test about the digest builds its own keeper.
/// </summary>
internal static class SilentTurnKeeper
{
    internal static SpecDialogTurnKeeper Over(
        SpecDialogSessionRepository repository, SpecDialogSessionManager sessions, string project = "sample") =>
        new(sessions, new SpecDialogSubjectStore(repository), Writer(repository, project));

    private static SpecDialogDigestWriter Writer(SpecDialogSessionRepository repository, string project = "sample") =>
        new(new SilentChatModel.SilentChatClients(), SilentChatModel.Loader(project),
            new ServerContext("agentsmith.yml"), repository, BoundSessionNudges.Quiet(repository), TimeProvider.System,
            NullLogger<SpecDialogDigestWriter>.Instance);
}

using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// The turn keeper for tests that are NOT about the subject or the digest: its minter and its digest writer share
/// <see cref="SilentSubjectMinter"/>'s model, whose empty answer stores nothing. A test about either builds its own keeper.
/// </summary>
internal static class SilentTurnKeeper
{
    internal static SpecDialogTurnKeeper Over(
        SpecDialogSessionRepository repository, SpecDialogSessionManager sessions, string project = "sample") =>
        new(sessions, SilentSubjectMinter.Over(repository, project), Writer(repository, project));

    private static SpecDialogDigestWriter Writer(SpecDialogSessionRepository repository, string project = "sample") =>
        new(new SilentSubjectMinter.SilentChatClients(), SilentSubjectMinter.Loader(project),
            new ServerContext("agentsmith.yml"), repository, BoundSessionNudges.Quiet(repository), TimeProvider.System,
            NullLogger<SpecDialogDigestWriter>.Instance);
}

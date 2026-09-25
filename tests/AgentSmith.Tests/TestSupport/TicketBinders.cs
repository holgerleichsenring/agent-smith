using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-25-8e51b: the ticket binder a dispatcher needs but a test about something else does
/// not use. A conversation that names no ticket never reaches it, so an unreachable one keeps
/// those tests saying what they are about.
/// </summary>
internal static class TicketBinders
{
    internal static TicketConversationBinder Unused(SpecDialogSessionRepository sessions) =>
        new(new Mock<ITicketProviderFactory>().Object, sessions,
            NullLogger<TicketConversationBinder>.Instance);

    internal static IConfigurationLoader NoConfig() => new Mock<IConfigurationLoader>().Object;

    internal static ServerContext NoPath() => new(string.Empty);
}

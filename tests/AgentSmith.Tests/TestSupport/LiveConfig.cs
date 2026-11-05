using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-04-df50b: what a class that reads the configuration per call is handed in a unit test —
/// the real readers over a store that holds one fixed configuration.
/// </summary>
internal static class LiveConfig
{
    internal static readonly ServerContext Context = new("config.yml");

    internal static TicketProjects Projects(AgentSmithConfig config) =>
        new(new FixedConfigurationLoader(config), Context);
}

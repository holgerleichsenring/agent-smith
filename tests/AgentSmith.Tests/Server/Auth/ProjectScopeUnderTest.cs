using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4db: the project scope built by hand — an access over a stated resolver and the projects
/// a configuration names, and the dialog message scope over a store. A non-enforcing scope reads no store,
/// so a case about one passes none.
/// </summary>
internal static class ProjectScopeUnderTest
{
    public static ProjectAccess Access(
        TokenAuthorityConfig auth, CallerIdentityResolver? resolver = null, params string[] configured) =>
        new(auth, resolver ?? ResolverUnderTest.With(auth), Projects(configured));

    public static DialogMessageScope Scope(ProjectAccess access, IUnitOfWork? store = null) =>
        new(access, new ResourceProjectReader(store!), new SpecCommandParser());

    public static TicketProjects Projects(params string[] names) =>
        new(new FixedLoader(Configured(names)), new ServerContext("agentsmith.yml"));

    /// <summary>A configuration naming these projects, keyed as the catalog resolver keys them.</summary>
    public static AgentSmithConfig Configured(params string[] names) => new()
    {
        Projects = names.ToDictionary(
            name => name, name => new ResolvedProject { Name = name }, ConfigNames.Comparer),
    };

    internal sealed class FixedLoader(AgentSmithConfig config) : IConfigurationLoader
    {
        public ConfigFileReadFact? LastRead => null;

        public AgentSmithConfig LoadConfig(string configPath) => config;
    }
}

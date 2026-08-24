using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0345: the config studio's surface over <see cref="IConfigStore"/> — CRUD per catalog
/// entity, the capability and validation reads, catalog transfer, the attributed change
/// feed and revert, and the settings singletons. Referential integrity is enforced in the
/// store (unknown agent/tracker/repo ref on a project → <see cref="ConfigurationException"/>
/// surfaced as 400). Mapped only inside Program.cs's <c>AGENTSMITH_UI_API_ENABLED</c>
/// block, like the other dashboard endpoints, so a dashboard-less deployment never
/// exposes the mutation surface.
/// <para>
/// p0510: this is the entry point only — each surface maps its own routes from its own
/// file, and they share one write guard (<see cref="Services.Config.ConfigStudioWriteGuard"/>).
/// </para>
/// </summary>
internal static class ConfigStudioEndpoints
{
    internal static WebApplication MapConfigStudioEndpoints(this WebApplication app)
    {
        app.MapConfigEntityRoutes();
        app.MapConfigCapabilityEndpoints();
        app.MapConfigTransferEndpoints();
        app.MapConfigChangeEndpoints();
        app.MapConfigSettingsEndpoints();
        return app;
    }
}

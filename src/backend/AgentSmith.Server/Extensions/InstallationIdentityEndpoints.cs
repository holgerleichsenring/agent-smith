using AgentSmith.Server.Services.Startup;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-27-729e: what this installation is running, over HTTP. Beside the findings route
/// and anonymous for the same reason: an operator asking "which build am I on" is often
/// asking BECAUSE the authority, the database or the configuration is not answering, and a
/// read-out that needs the thing it reports on cannot report on it.
/// </summary>
internal static class InstallationIdentityEndpoints
{
    internal static WebApplication MapInstallationIdentityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/installation",
            ([FromServices] InstallationIdentityReporter reporter, CancellationToken cancellationToken) =>
                reporter.ReadAsync(cancellationToken)).Anonymous(
            "which build an installation runs is the question asked when nothing else answers");
        return app;
    }
}

using AgentSmith.Server.Services.Catalog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0221: dashboard catalog browser API. Lists the resolved catalog's masters,
/// skills and concept vocabulary, and serves one skill/master SKILL.md body on
/// demand (on expand) so the per-run event stream is never bloated with bodies.
/// </summary>
internal static class CatalogEndpoints
{
    internal static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/catalog",
            (CatalogContentsReader reader, CancellationToken ct) => reader.GetContentsAsync(ct));

        app.MapGet("/api/catalog/skills/{name}", GetSkillBodyAsync);
        return app;
    }

    private static async Task<IResult> GetSkillBodyAsync(
        string name, CatalogContentsReader reader, CancellationToken ct)
    {
        var body = await reader.GetSkillBodyAsync(name, ct);
        return body is null ? Results.NotFound() : Results.Ok(body);
    }
}

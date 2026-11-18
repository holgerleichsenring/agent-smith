using Microsoft.AspNetCore.Mvc;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Admin.Secrets;
using AgentSmith.Server.Services.Config;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-17-f3a8d: the admin area's secret values. Write-only: a value is posted here straight from the field and
/// stored by a secret run; no route answers with a value — the reads say where a value comes from, who set it and when.
/// </summary>
internal static class SecretValueEndpoints
{
    internal sealed record SecretValueBody(string? Value);

    internal static WebApplication MapSecretValueEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/secret-values", ([FromServices] SecretValuesReader reader, CancellationToken ct) => reader.ReadAsync(ct))
           .Needs(Permissions.SecretsRead);
        app.MapPut("/api/config/secret-values/{name}", (Delegate)SetAsync).Needs(Permissions.SecretsWrite);
        app.MapDelete("/api/config/secret-values/{name}", (string name, HttpContext ctx, [FromServices] ISecretWriteRun runs) =>
            Answer(runs.RunAsync(new SecretWriteRequest(name, SecretWriteRequest.Delete, null, Person(ctx)))))
           .Needs(Permissions.SecretsWrite);
        app.MapPost("/api/config/secret-values/rewrap", (HttpContext ctx, [FromServices] ISecretWriteRun runs) =>
            Answer(runs.RunAsync(new SecretWriteRequest(string.Empty, SecretWriteRequest.Rewrap, null, Person(ctx)))))
           .Needs(Permissions.SecretsWrite);
        return app;
    }

    internal static Task<IResult> SetAsync(string name, [FromBody] SecretValueBody body, HttpContext ctx, [FromServices] ISecretWriteRun runs) =>
        string.IsNullOrEmpty(body.Value)
            ? Task.FromResult(Results.BadRequest(new { error = "A value is required; to remove one, delete it." }))
            : Answer(runs.RunAsync(new SecretWriteRequest(name, SecretWriteRequest.Set, new SecretInput(body.Value), Person(ctx))));

    private static async Task<IResult> Answer(Task<SecretWriteReport> run)
    {
        var report = await run;
        return report.Written ? Results.Ok(new { line = report.Line }) : Results.BadRequest(new { error = report.Line });
    }

    private static string Person(HttpContext ctx) => ConfigStudioWriteGuard.Attribution(ctx).Actor;
}

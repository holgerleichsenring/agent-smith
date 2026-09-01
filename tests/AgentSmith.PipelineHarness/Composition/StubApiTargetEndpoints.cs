using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-09-01-6686: what the stub target actually DOES, so a scan that probes it is
/// answered by the behaviour its document describes.
/// <para>
/// The master's surface carries http_request, and the eval's sandbox runs it on the host,
/// so the served endpoints are reachable. A document alone would let a scan be scored on
/// reading rather than on scanning; the declared weakness is therefore in the RESPONSE
/// too — an unauthenticated read that answers, an identifier nobody scopes, an error that
/// says too much, an escalation nobody gates.
/// </para>
/// <para>
/// Everything here is fictional and structural. Nothing in any credential issuer's format
/// appears, because a deliberately weak target in a public repository must not carry a
/// literal that looks like a real secret.
/// </para>
/// </summary>
internal static class StubApiTargetEndpoints
{
    private const string KnownBearer = "member-1";

    internal static void Map(WebApplication app, string openApiJson)
    {
        app.MapGet("/openapi.json", () => Results.Text(openApiJson, "application/json"));
        app.MapGet("/health", () => Results.Json(new { status = "ok" }));

        // WEAK: no authorization at all — any caller reads any member, contact included.
        app.MapGet("/members/{id}", (string id) => Results.Json(Member(id)));

        // WEAK: any bearer sets any member's role, its own included.
        app.MapPut("/members/{id}/role", (string id, RoleChange change, HttpRequest request) =>
            Bearer(request).Length == 0
                ? Results.Unauthorized()
                : Results.Json(new { id, displayName = $"Member {id}", role = change.Role }));

        // WEAK: memberId is the caller's word and nothing checks it against the bearer.
        app.MapGet("/orders", (HttpRequest request) =>
        {
            if (Bearer(request).Length == 0) return Results.Unauthorized();
            var memberId = request.Query["memberId"].ToString();
            return Results.Json(new[]
            {
                new { id = $"order-{memberId}-1", memberId, totalCents = 4200 },
            });
        });

        // SOUND: the same shape, scoped to the bearer — anyone else's order is a 404.
        app.MapGet("/orders/{id}", (string id, HttpRequest request) =>
        {
            var bearer = Bearer(request);
            if (bearer.Length == 0) return Results.Unauthorized();
            return id.StartsWith($"order-{bearer}-", StringComparison.Ordinal)
                ? Results.Json(new { id, memberId = bearer, totalCents = 4200 })
                : Results.NotFound(new { error = "no such order" });
        });

        // WEAK: a malformed body answers with the internal failure and the failing SQL.
        app.MapPost("/invoices", async (HttpRequest request) =>
        {
            if (Bearer(request).Length == 0) return Results.Unauthorized();
            var body = await new StreamReader(request.Body).ReadToEndAsync();
            return body.Contains("\"orderId\"", StringComparison.Ordinal)
                ? Results.Json(new { id = "invoice-1", status = "created" }, statusCode: 201)
                : Results.Json(new
                {
                    error = "NullReferenceException in InvoiceRepository.Insert(order)",
                    sql = "INSERT INTO billing.invoices (order_id, member_id, total_cents) VALUES (@0, @1, @2)",
                    stack = "at InvoiceRepository.Insert(String) in /srv/app/billing/InvoiceRepository.cs:line 84",
                }, statusCode: 500);
        });

        // SOUND: reads like a credential surface, answers one boolean and echoes nothing.
        app.MapPost("/tokens/introspect", (HttpRequest request) =>
            Results.Json(new { active = Bearer(request).Length > 0 }));
    }

    private static object Member(string id) => new
    {
        id,
        displayName = $"Member {id}",
        role = id == KnownBearer ? "admin" : "member",
        contactEmail = $"member-{id}@example.com",
    };

    private static string Bearer(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }

    internal sealed record RoleChange(string Role);
}

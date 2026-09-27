using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-27-5c1eb: the tickets a person can pick from after typing a few characters.
/// <para>
/// Its own file because <see cref="TicketConversationEndpoints"/> had thirty lines of headroom
/// under the per-file limit and this needed more; the SWEEP itself is a service, so what lives
/// here is the route, the minimum, and the shape the page reads.
/// </para>
/// </summary>
internal static class TicketSearchEndpoints
{
    internal static WebApplication MapTicketSearchEndpoints(this WebApplication app)
    {
        // A LITERAL segment: a literal out-ranks a parameter in ASP.NET routing whatever the order,
        // so this cannot be read as a ticket id — and no tracker's id grammar produces the word
        // "search", so nothing real is shadowed either.
        app.MapGet("/api/spec-dialog/tickets/search", (Delegate)SearchTicketsAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    /// <summary>
    /// 2026-09-27-5c1eb: the same permission as its neighbours. It reveals more than they do — an
    /// operator can read a board's open ticket TITLES from three characters, where the routes beside
    /// it need the id in hand — and dialog.write is operator-and-admin only, held by exactly the
    /// people who already file onto those trackers. Their "returns an identifier and no content"
    /// sentence is not extended here: it was already untrue of both, which return a title.
    /// </summary>
    internal static async Task<IResult> SearchTicketsAsync(
        string? q,
        IConfigurationLoader configLoader,
        ServerContext serverContext,
        TicketSearchAcrossTrackers search,
        CancellationToken cancellationToken)
    {
        var typed = (q ?? string.Empty).Trim();
        // Below the minimum NO TRACKER IS ASKED, and the page is told the minimum rather than
        // left to hold the same number twice.
        if (typed.Length < TicketSearchAcrossTrackers.MinimumText)
            return Results.Ok(new
            {
                found = Array.Empty<object>(),
                moreHeldBack = false,
                unsearchable = Array.Empty<string>(),
                minimum = TicketSearchAcrossTrackers.MinimumText,
            });

        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        var answer = await search.ForAsync(config, typed, cancellationToken);
        return Results.Ok(new
        {
            found = answer.Found,
            moreHeldBack = answer.MoreHeldBack,
            unsearchable = answer.Unsearchable,
            minimum = TicketSearchAcrossTrackers.MinimumText,
        });
    }
}

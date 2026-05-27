using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Extensions;

internal static class SlackOptionsEndpointHandler
{
    internal static async Task<IResult> HandleAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SlackOptions");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request, ctx.RequestAborted))
            return Results.Unauthorized();

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var payloadJson = form["payload"];

        if (string.IsNullOrWhiteSpace(payloadJson)) return Results.BadRequest();

        var json = JsonNode.Parse(payloadJson);
        if (json is null) return Results.BadRequest();

        var actionId = json["action_id"]?.GetValue<string>() ?? string.Empty;
        var searchQuery = json["value"]?.GetValue<string>();

        if (actionId == DispatcherDefaults.SlackActionProject)
        {
            var configLoader = ctx.RequestServices.GetRequiredService<AgentSmith.Contracts.Services.IConfigurationLoader>();
            var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
            var projectNames = config.Projects.Keys.ToList();

            var result = SlackModalBuilder.BuildProjectOptions(projectNames, searchQuery);
            return Results.Json(result);
        }

        if (actionId == DispatcherDefaults.SlackActionTicket)
        {
            var selectedProject = SlackPayloadExtractor.ExtractSelectedProjectFromOptionsPayload(json);
            if (string.IsNullOrWhiteSpace(selectedProject))
                return Results.Json(new { options = Array.Empty<object>() });

            var ticketSearch = ctx.RequestServices.GetRequiredService<CachedTicketSearch>();
            var tickets = await ticketSearch.SearchAsync(selectedProject, searchQuery, ctx.RequestAborted);

            var result = SlackModalBuilder.BuildTicketOptions(tickets, searchQuery: null);
            return Results.Json(result);
        }

        logger.LogWarning("Unknown options action_id: {ActionId}", actionId);
        return Results.Json(new { options = Array.Empty<object>() });
    }
}

using AgentSmith.Dispatcher.Adapters;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using AgentSmith.Infrastructure;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

PrintBanner();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

// --- Redis ---
var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisUrl));

// --- Core services ---
builder.Services.AddSingleton<IMessageBus, RedisMessageBus>();
builder.Services.AddSingleton<ConversationStateManager>();
builder.Services.AddSingleton<ChatIntentParser>();

// --- Job Spawner ---
var k8sConfig = KubernetesClientConfiguration.IsInCluster()
    ? KubernetesClientConfiguration.InClusterConfig()
    : KubernetesClientConfiguration.BuildConfigFromConfigFile();

builder.Services.AddSingleton<IKubernetes>(new Kubernetes(k8sConfig));
builder.Services.AddSingleton(new JobSpawnerOptions
{
    Namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "default",
    Image = Environment.GetEnvironmentVariable("AGENTSMITH_IMAGE") ?? "agentsmith:latest",
    ImagePullPolicy = Environment.GetEnvironmentVariable("IMAGE_PULL_POLICY") ?? "IfNotPresent",
    SecretName = Environment.GetEnvironmentVariable("K8S_SECRET_NAME") ?? "agentsmith-secrets",
    TtlSecondsAfterFinished = 300
});
builder.Services.AddSingleton<JobSpawner>();

// --- Platform adapters ---
builder.Services.AddHttpClient();
builder.Services.AddSingleton(new SlackAdapterOptions
{
    BotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? string.Empty,
    SigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty
});
builder.Services.AddSingleton<SlackAdapter>();
builder.Services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());

// --- Background listener ---
builder.Services.AddSingleton<MessageBusListener>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageBusListener>());

// --- Agent Smith infrastructure (for list/create fast commands) ---
builder.Services.AddAgentSmithInfrastructure();

var app = builder.Build();

// -------------------------------------------------------------------------
// GET /health
// -------------------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }));

// -------------------------------------------------------------------------
// POST /slack/events
// Receives Slack Events API payloads (URL verification + message events).
// -------------------------------------------------------------------------
app.MapPost("/slack/events", async (HttpContext ctx) =>
{
    var signingSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty;

    if (!await VerifySlackSignatureAsync(ctx.Request, signingSecret))
        return Results.Unauthorized();

    var body = await ReadBodyAsync(ctx.Request);
    var json = JsonNode.Parse(body);
    if (json is null) return Results.BadRequest();

    // Slack URL verification challenge (one-time on app setup)
    var type = json["type"]?.GetValue<string>();
    if (type == "url_verification")
    {
        var challenge = json["challenge"]?.GetValue<string>() ?? string.Empty;
        return Results.Ok(new { challenge });
    }

    if (type != "event_callback")
        return Results.Ok();

    var eventNode = json["event"];
    var eventType = eventNode?["type"]?.GetValue<string>();

    // Only handle messages from humans (not bots, not our own messages)
    if (eventType != "message" && eventType != "app_mention")
        return Results.Ok();

    var botId = eventNode?["bot_id"]?.GetValue<string>();
    if (!string.IsNullOrWhiteSpace(botId))
        return Results.Ok(); // ignore bot messages

    var text = eventNode?["text"]?.GetValue<string>() ?? string.Empty;
    var userId = eventNode?["user"]?.GetValue<string>() ?? string.Empty;
    var channelId = eventNode?["channel"]?.GetValue<string>() ?? string.Empty;

    // Strip bot mention prefix if present (<@BOTID> fix #65 in todo-list)
    text = StripMention(text);

    if (string.IsNullOrWhiteSpace(text))
        return Results.Ok();

    // Fire and forget - Slack expects a 200 within 3 seconds
    _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        await HandleSlackMessageAsync(scope.ServiceProvider, text, userId, channelId);
    });

    return Results.Ok();
});

// -------------------------------------------------------------------------
// POST /slack/interact
// Receives Slack interactive component payloads (button clicks).
// -------------------------------------------------------------------------
app.MapPost("/slack/interact", async (HttpContext ctx) =>
{
    var signingSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty;

    if (!await VerifySlackSignatureAsync(ctx.Request, signingSecret))
        return Results.Unauthorized();

    var body = await ReadBodyAsync(ctx.Request);

    // Slack sends payload as form-encoded: payload=<urlencoded json>
    var form = System.Web.HttpUtility.ParseQueryString(body);
    var payloadJson = form["payload"];
    if (string.IsNullOrWhiteSpace(payloadJson))
        return Results.BadRequest();

    var json = JsonNode.Parse(payloadJson);
    if (json is null) return Results.BadRequest();

    var interactionType = json["type"]?.GetValue<string>();
    if (interactionType != "block_actions")
        return Results.Ok();

    var userId = json["user"]?["id"]?.GetValue<string>() ?? string.Empty;
    var channelId = json["channel"]?["id"]?.GetValue<string>() ?? string.Empty;
    var action = json["actions"]?[0];
    var actionId = action?["action_id"]?.GetValue<string>() ?? string.Empty;
    var value = action?["value"]?.GetValue<string>() ?? string.Empty;

    // actionId format: "{questionId}:yes" or "{questionId}:no"
    var separatorIndex = actionId.LastIndexOf(':');
    if (separatorIndex < 0)
        return Results.Ok();

    var questionId = actionId[..separatorIndex];
    var answer = actionId[(separatorIndex + 1)..];

    _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        await HandleSlackInteractionAsync(
            scope.ServiceProvider, channelId, questionId, answer, json);
    });

    // Acknowledge immediately so Slack removes the loading spinner
    return Results.Ok();
});

app.Run();

// =========================================================================
// Handlers
// =========================================================================

static async Task HandleSlackMessageAsync(
    IServiceProvider services, string text, string userId, string channelId)
{
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var parser = services.GetRequiredService<ChatIntentParser>();
        var intent = parser.Parse(text, userId, channelId, "slack");

        switch (intent)
        {
            case FixTicketIntent fix:
                await HandleFixTicketAsync(services, fix);
                break;

            case ListTicketsIntent list:
                await HandleListTicketsAsync(services, list);
                break;

            case CreateTicketIntent create:
                await HandleCreateTicketAsync(services, create);
                break;

            default:
                var adapter = services.GetRequiredService<SlackAdapter>();
                await adapter.SendMessageAsync(channelId,
                    ":question: I didn't understand that. Try:\n" +
                    "• `fix #65 in todo-list`\n" +
                    "• `list tickets in todo-list`\n" +
                    "• `create ticket \"Add logging\" in todo-list`");
                break;
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error handling Slack message from {UserId} in {ChannelId}", userId, channelId);

        try
        {
            var adapter = services.GetRequiredService<SlackAdapter>();
            await adapter.SendErrorAsync(channelId, ex.Message);
        }
        catch
        {
            // best-effort
        }
    }
}

static async Task HandleFixTicketAsync(IServiceProvider services, FixTicketIntent intent)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var adapter = services.GetRequiredService<SlackAdapter>();
    var spawner = services.GetRequiredService<JobSpawner>();
    var stateManager = services.GetRequiredService<ConversationStateManager>();
    var listener = services.GetRequiredService<MessageBusListener>();

    // Check if there's already a running job for this channel
    var existing = await stateManager.GetAsync(intent.Platform, intent.ChannelId);
    if (existing is not null)
    {
        await adapter.SendMessageAsync(intent.ChannelId,
            $":hourglass: There is already a job running for this channel (job `{existing.JobId}` for ticket #{existing.TicketId}). " +
            "Please wait for it to complete.");
        return;
    }

    await adapter.SendMessageAsync(intent.ChannelId,
        $":rocket: Starting Agent Smith for ticket *#{intent.TicketId}* in *{intent.Project}*...");

    var jobId = await spawner.SpawnAsync(intent);

    var state = new ConversationState
    {
        JobId = jobId,
        ChannelId = intent.ChannelId,
        UserId = intent.UserId,
        Platform = intent.Platform,
        Project = intent.Project,
        TicketId = intent.TicketId,
        StartedAt = DateTimeOffset.UtcNow
    };

    await stateManager.SetAsync(state);
    await stateManager.IndexJobAsync(state);
    await listener.TrackJobAsync(jobId);

    logger.LogInformation(
        "Job {JobId} spawned for ticket #{TicketId} in {Project} (channel={ChannelId})",
        jobId, intent.TicketId, intent.Project, intent.ChannelId);
}

static async Task HandleListTicketsAsync(IServiceProvider services, ListTicketsIntent intent)
{
    var adapter = services.GetRequiredService<SlackAdapter>();
    var configLoader = services.GetRequiredService<AgentSmith.Contracts.Services.IConfigurationLoader>();
    var ticketFactory = services.GetRequiredService<AgentSmith.Contracts.Providers.ITicketProviderFactory>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var config = configLoader.LoadConfig("config/agentsmith.yml");

        if (!config.Projects.TryGetValue(intent.Project, out var projectConfig))
        {
            await adapter.SendMessageAsync(intent.ChannelId,
                $":x: Project *{intent.Project}* not found in configuration.");
            return;
        }

        var ticketProvider = ticketFactory.Create(projectConfig.Tickets);
        var tickets = await ticketProvider.ListOpenAsync(cancellationToken: default);

        if (!tickets.Any())
        {
            await adapter.SendMessageAsync(intent.ChannelId,
                $":white_check_mark: No open tickets found in *{intent.Project}*.");
            return;
        }

        var lines = tickets.Take(20).Select(t => $"• *#{t.Id}* — {t.Title} `[{t.Status}]`");
        var text = $":ticket: *Open tickets in {intent.Project}* ({tickets.Count} total):\n"
                   + string.Join("\n", lines);

        await adapter.SendMessageAsync(intent.ChannelId, text);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to list tickets for project {Project}", intent.Project);
        await adapter.SendErrorAsync(intent.ChannelId, ex.Message);
    }
}

static async Task HandleCreateTicketAsync(IServiceProvider services, CreateTicketIntent intent)
{
    var adapter = services.GetRequiredService<SlackAdapter>();
    var configLoader = services.GetRequiredService<AgentSmith.Contracts.Services.IConfigurationLoader>();
    var ticketFactory = services.GetRequiredService<AgentSmith.Contracts.Providers.ITicketProviderFactory>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var config = configLoader.LoadConfig("config/agentsmith.yml");

        if (!config.Projects.TryGetValue(intent.Project, out var projectConfig))
        {
            await adapter.SendMessageAsync(intent.ChannelId,
                $":x: Project *{intent.Project}* not found in configuration.");
            return;
        }

        var ticketProvider = ticketFactory.Create(projectConfig.Tickets);
        var ticketId = await ticketProvider.CreateAsync(intent.Title, intent.Description ?? string.Empty);

        await adapter.SendMessageAsync(intent.ChannelId,
            $":white_check_mark: Ticket *#{ticketId}* created in *{intent.Project}*: _{intent.Title}_\n" +
            $"To start working on it: `fix #{ticketId} in {intent.Project}`");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create ticket in project {Project}", intent.Project);
        await adapter.SendErrorAsync(intent.ChannelId, ex.Message);
    }
}

static async Task HandleSlackInteractionAsync(
    IServiceProvider services, string channelId, string questionId, string answer, JsonNode payload)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var messageBus = services.GetRequiredService<IMessageBus>();
    var stateManager = services.GetRequiredService<ConversationStateManager>();
    var adapter = services.GetRequiredService<SlackAdapter>();

    try
    {
        var state = await stateManager.GetAsync("slack", channelId);
        if (state is null)
        {
            logger.LogWarning(
                "Received interaction for channel {ChannelId} but no active job found", channelId);
            return;
        }

        if (state.PendingQuestionId != questionId)
        {
            logger.LogWarning(
                "Received answer for question {QuestionId} but pending is {Pending}",
                questionId, state.PendingQuestionId);
            return;
        }

        // Publish the answer to the agent's inbound stream
        await messageBus.PublishAnswerAsync(state.JobId, questionId, answer);

        // Update the Slack message to show the selected answer (remove buttons)
        var messageTs = payload["message"]?["ts"]?.GetValue<string>() ?? string.Empty;
        var questionText = payload["actions"]?[0]?["block_id"]?.GetValue<string>()
                           ?? "Question";

        if (!string.IsNullOrWhiteSpace(messageTs))
        {
            await adapter.UpdateQuestionAnsweredAsync(
                channelId, messageTs, questionText, answer);
        }

        await stateManager.ClearPendingQuestionAsync("slack", channelId);

        logger.LogInformation(
            "Answer '{Answer}' for question '{QuestionId}' forwarded to job {JobId}",
            answer, questionId, state.JobId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error handling Slack interaction for channel {ChannelId}", channelId);
    }
}

// =========================================================================
// Helpers
// =========================================================================

static async Task<bool> VerifySlackSignatureAsync(HttpRequest request, string signingSecret)
{
    if (string.IsNullOrWhiteSpace(signingSecret))
        return true; // skip verification in development if not configured

    if (!request.Headers.TryGetValue("X-Slack-Request-Timestamp", out var tsValues) ||
        !request.Headers.TryGetValue("X-Slack-Signature", out var sigValues))
        return false;

    var timestamp = tsValues.FirstOrDefault() ?? string.Empty;
    var signature = sigValues.FirstOrDefault() ?? string.Empty;

    // Reject requests older than 5 minutes (replay attack prevention)
    if (long.TryParse(timestamp, out var ts))
    {
        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts;
        if (Math.Abs(age) > 300) return false;
    }

    var body = await ReadBodyAsync(request);
    var baseString = $"v0:{timestamp}:{body}";

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
    var expected = $"v0={Convert.ToHexString(hash).ToLowerInvariant()}";

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(signature));
}

static async Task<string> ReadBodyAsync(HttpRequest request)
{
    request.EnableBuffering();
    request.Body.Position = 0;
    using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    request.Body.Position = 0;
    return body;
}

static void PrintBanner()
{
    var original = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(@"
  █████╗  ██████╗ ███████╗███╗   ██╗████████╗    ███████╗███╗   ███╗██╗████████╗██╗  ██╗
 ██╔══██╗██╔════╝ ██╔════╝████╗  ██║╚══██╔══╝    ██╔════╝████╗ ████║██║╚══██╔══╝██║  ██║
 ███████║██║  ███╗█████╗  ██╔██╗ ██║   ██║       ███████╗██╔████╔██║██║   ██║   ███████║
 ██╔══██║██║   ██║██╔══╝  ██║╚██╗██║   ██║       ╚════██║██║╚██╔╝██║██║   ██║   ██╔══██║
 ██║  ██║╚██████╔╝███████╗██║ ╚████║   ██║       ███████║██║ ╚═╝ ██║██║   ██║   ██║  ██║
 ╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═══╝  ╚═╝       ╚══════╝╚═╝     ╚═╝╚═╝   ╚═╝   ╚═╝  ╚═╝");
    Console.ForegroundColor = ConsoleColor.DarkGreen;
    Console.WriteLine("  Dispatcher · Slack / Teams · Redis Streams · K8s Jobs\n");
    Console.ForegroundColor = original;
}

static string StripMention(string text)
{
    // Remove <@USERID> prefix that Slack adds in app_mention events
    var trimmed = text.Trim();
    if (trimmed.StartsWith("<@", StringComparison.Ordinal))
    {
        var end = trimmed.IndexOf('>', 2);
        if (end >= 0)
            trimmed = trimmed[(end + 1)..].TrimStart();
    }
    return trimmed;
}

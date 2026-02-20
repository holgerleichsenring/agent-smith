using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Adapters;

/// <summary>
/// Slack Web API implementation of IPlatformAdapter.
/// Uses raw HTTP calls to the Slack API - no SDK dependency.
/// Requires SLACK_BOT_TOKEN and SLACK_SIGNING_SECRET environment variables.
/// </summary>
public sealed class SlackAdapter(
    HttpClient httpClient,
    SlackAdapterOptions options,
    ILogger<SlackAdapter> logger) : IPlatformAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Tracks the progress message ts per channel so we can update instead of re-post
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
        _progressMessageTs = new();

    private static readonly Dictionary<string, string> StepEmojis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fetching ticket"] = ":ticket:",
        ["Checking out source"] = ":file_folder:",
        ["Loading coding principles"] = ":books:",
        ["Analyzing codebase"] = ":mag:",
        ["Generating plan"] = ":bulb:",
        ["Awaiting approval"] = ":white_check_mark:",
        ["Executing plan"] = ":zap:",
        ["Generating tests"] = ":test_tube:",
        ["Running tests"] = ":rotating_light:",
        ["Generating docs"] = ":memo:",
        ["Creating pull request"] = ":rocket:",
    };

    public string Platform => "slack";

    public async Task SendMessageAsync(string channelId, string text,
        CancellationToken cancellationToken = default)
    {
        var payload = new { channel = channelId, text };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken = default)
    {
        var emoji = StepEmojis.GetValueOrDefault(commandName, ":gear:");
        var bar = BuildProgressBar(step, total);
        var text = $"{emoji} *[{step}/{total}]* {commandName}\n{bar}";

        if (_progressMessageTs.TryGetValue(channelId, out var existingTs))
        {
            // Update the existing progress message instead of flooding the channel
            var updatePayload = new { channel = channelId, ts = existingTs, text };
            var updateResponse = await PostAsync("chat.update", updatePayload, cancellationToken);

            // If update fails for any reason, fall back to a new message
            var ok = updateResponse?["ok"]?.GetValue<bool>() ?? false;
            if (ok) return;
        }

        // First step or fallback: post a new message and remember its ts
        var postPayload = new { channel = channelId, text };
        var response = await PostAsync("chat.postMessage", postPayload, cancellationToken);
        var ts = ExtractTimestamp(response);
        if (ts is not null)
            _progressMessageTs[channelId] = ts;
    }

    public async Task<string> AskQuestionAsync(string channelId, string questionId, string text,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            channel = channelId,
            text = $":thought_balloon: *Question:* {text}",
            blocks = new object[]
            {
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $":thought_balloon: *{text}*" }
                },
                new
                {
                    type = "actions",
                    block_id = questionId,
                    elements = new object[]
                    {
                        new
                        {
                            type = "button",
                            text = new { type = "plain_text", text = "Yes :white_check_mark:" },
                            style = "primary",
                            value = "yes",
                            action_id = $"{questionId}:yes"
                        },
                        new
                        {
                            type = "button",
                            text = new { type = "plain_text", text = "No :x:" },
                            style = "danger",
                            value = "no",
                            action_id = $"{questionId}:no"
                        }
                    }
                }
            }
        };

        var response = await PostAsync("chat.postMessage", payload, cancellationToken);
        return ExtractTimestamp(response) ?? string.Empty;
    }

    public async Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken = default)
    {
        // Remove progress message tracking for this channel
        _progressMessageTs.TryRemove(channelId, out _);

        var text = string.IsNullOrWhiteSpace(prUrl)
            ? $":rocket: *Done!* {summary}"
            : $":rocket: *Done!* {summary}\n:link: <{prUrl}|View Pull Request>";

        var payload = new { channel = channelId, text };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendErrorAsync(string channelId, string text,
        CancellationToken cancellationToken = default)
    {
        // Remove progress message tracking for this channel
        _progressMessageTs.TryRemove(channelId, out _);

        var payload = new
        {
            channel = channelId,
            text = $":x: *Agent Smith encountered an error:*\n```{text}```"
        };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken = default)
    {
        var emoji = answer.Equals("yes", StringComparison.OrdinalIgnoreCase)
            ? ":white_check_mark:"
            : ":x:";

        var payload = new
        {
            channel = channelId,
            ts = messageId,
            text = $":thought_balloon: *{questionText}*\n{emoji} Answered: *{answer}*",
            blocks = new object[]
            {
                new
                {
                    type = "section",
                    text = new
                    {
                        type = "mrkdwn",
                        text = $":thought_balloon: *{questionText}*\n{emoji} Answered: *{answer}*"
                    }
                }
            }
        };

        await PostAsync("chat.update", payload, cancellationToken);
    }

    // --- Internal HTTP helpers ---

    private async Task<JsonNode?> PostAsync(string method, object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://slack.com/api/{method}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.BotToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Slack API call failed: {Method}", method);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonNode? node = null;
        try
        {
            node = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to parse Slack response for {Method}", method);
            return null;
        }

        var ok = node?["ok"]?.GetValue<bool>() ?? false;
        if (!ok)
        {
            var error = node?["error"]?.GetValue<string>() ?? "unknown";
            logger.LogWarning("Slack API {Method} returned ok=false: {Error}", method, error);
        }

        return node;
    }

    private static string? ExtractTimestamp(JsonNode? response) =>
        response?["ts"]?.GetValue<string>();

    private static string BuildProgressBar(int step, int total)
    {
        const int barLength = 10;
        var filled = (int)Math.Round((double)step / total * barLength);
        var empty = barLength - filled;
        return $"`[{"█".PadRight(filled + empty - empty, '█').Substring(0, filled)}{"░".PadRight(empty, '░')}]` {step}/{total}";
    }
}

/// <summary>
/// Configuration for SlackAdapter, bound from environment variables or appsettings.
/// </summary>
public sealed class SlackAdapterOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
}

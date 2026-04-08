using AgentSmith.Contracts.Dialogue;
using AgentSmith.Dispatcher.Contracts;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services.Adapters;

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
    private readonly ConcurrentDictionary<string, string>
        _progressMessageTs = new();

    // Pending typed question completions, keyed by questionId
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogAnswer?>>
        _pendingTypedQuestions = new();

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
        CancellationToken cancellationToken)
    {
        var payload = new { channel = channelId, text };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken)
    {
        var bar = BuildProgressBar(step, total);
        var text = $"*[{step}/{total}]* {commandName}\n{bar}";

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
        CancellationToken cancellationToken)
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
        CancellationToken cancellationToken)
    {
        // Remove progress message tracking for this channel
        _progressMessageTs.TryRemove(channelId, out _);

        var text = string.IsNullOrWhiteSpace(prUrl)
            ? $":rocket: *Done!* {summary}"
            : $":rocket: *Done!* {summary}\n:link: <{prUrl}|View Pull Request>";

        var payload = new { channel = channelId, text };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendErrorAsync(string channelId, ErrorContext errorContext,
        CancellationToken cancellationToken)
    {
        _progressMessageTs.TryRemove(channelId, out _);

        var (fallbackText, blocks) = SlackErrorBlockBuilder.Build(errorContext);
        var payload = new { channel = channelId, text = fallbackText, blocks };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken)
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

    public async Task SendDetailAsync(string channelId, string text,
        CancellationToken cancellationToken)
    {
        if (!_progressMessageTs.TryGetValue(channelId, out var threadTs))
        {
            logger.LogDebug("No progress message to thread detail under for {Channel}", channelId);
            return;
        }

        var payload = new { channel = channelId, text, thread_ts = threadTs };
        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            channel = channelId,
            text = $":thinking_face: Did you mean: *{suggestion}*?",
            blocks = new object[]
            {
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $":thinking_face: Did you mean: *{suggestion}*?" }
                },
                new
                {
                    type = "actions",
                    block_id = "clarification",
                    elements = new object[]
                    {
                        new
                        {
                            type = "button",
                            text = new { type = "plain_text", text = "Yes, do it" },
                            style = "primary",
                            value = "confirm",
                            action_id = "clarification:confirm"
                        },
                        new
                        {
                            type = "button",
                            text = new { type = "plain_text", text = "Show help" },
                            value = "help",
                            action_id = "clarification:help"
                        }
                    }
                }
            }
        };

        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    public async Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId,
        DialogQuestion question,
        CancellationToken cancellationToken)
    {
        if (question.Type == QuestionType.Info)
        {
            await SendInfoAsync(channelId, question.Text, question.Context ?? "", cancellationToken);
            return null;
        }

        var blocks = BuildTypedQuestionBlocks(question);
        var fallbackText = $":thought_balloon: *Question:* {question.Text}";

        var payload = new { channel = channelId, text = fallbackText, blocks };
        await PostAsync("chat.postMessage", payload, cancellationToken);

        // Register a TCS and wait for the interaction handler to complete it
        var tcs = new TaskCompletionSource<DialogAnswer?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTypedQuestions[question.QuestionId] = tcs;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(question.Timeout);

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Typed question {QuestionId} timed out after {Timeout}",
                question.QuestionId, question.Timeout);
            return null;
        }
        finally
        {
            _pendingTypedQuestions.TryRemove(question.QuestionId, out _);
        }
    }

    public async Task SendInfoAsync(string channelId, string title, string text,
        CancellationToken cancellationToken)
    {
        var blocks = new object[]
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":information_source: *{title}*" }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text }
            }
        };

        var payload = new
        {
            channel = channelId,
            text = $":information_source: {title}: {text}",
            blocks
        };

        await PostAsync("chat.postMessage", payload, cancellationToken);
    }

    /// <summary>
    /// Completes a pending typed question with the given answer.
    /// Called by <see cref="SlackInteractionHandler"/> when a button click arrives.
    /// </summary>
    internal bool TryCompleteTypedQuestion(string questionId, DialogAnswer answer)
    {
        if (_pendingTypedQuestions.TryRemove(questionId, out var tcs))
        {
            tcs.TrySetResult(answer);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a typed question is currently pending for the given questionId.
    /// </summary>
    internal bool HasPendingTypedQuestion(string questionId) =>
        _pendingTypedQuestions.ContainsKey(questionId);

    // --- Block Kit builders for typed questions ---

    internal static object[] BuildTypedQuestionBlocks(DialogQuestion question)
    {
        return question.Type switch
        {
            QuestionType.Confirmation => BuildConfirmationBlocks(question),
            QuestionType.Choice => BuildChoiceBlocks(question),
            QuestionType.Approval => BuildApprovalBlocks(question),
            QuestionType.FreeText => BuildFreeTextBlocks(question),
            _ => BuildConfirmationBlocks(question)
        };
    }

    private static object[] BuildConfirmationBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":thought_balloon: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        blocks.Add(new
        {
            type = "actions",
            block_id = question.QuestionId,
            elements = new object[]
            {
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Yes \u2705" },
                    style = "primary",
                    value = "yes",
                    action_id = $"{question.QuestionId}:yes"
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "No \u274c" },
                    style = "danger",
                    value = "no",
                    action_id = $"{question.QuestionId}:no"
                }
            }
        });

        return blocks.ToArray();
    }

    private static object[] BuildChoiceBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":thought_balloon: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        var buttons = new List<object>();
        if (question.Choices is not null)
        {
            for (var i = 0; i < question.Choices.Count; i++)
            {
                buttons.Add(new
                {
                    type = "button",
                    text = new { type = "plain_text", text = question.Choices[i] },
                    value = question.Choices[i],
                    action_id = $"{question.QuestionId}:{i}"
                });
            }
        }

        blocks.Add(new
        {
            type = "actions",
            block_id = question.QuestionId,
            elements = buttons.ToArray()
        });

        return blocks.ToArray();
    }

    private static object[] BuildApprovalBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":clipboard: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        blocks.Add(new
        {
            type = "actions",
            block_id = question.QuestionId,
            elements = new object[]
            {
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Approve \u2705" },
                    style = "primary",
                    value = "approve",
                    action_id = $"{question.QuestionId}:approve"
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Reject \u274c" },
                    style = "danger",
                    value = "reject",
                    action_id = $"{question.QuestionId}:reject"
                }
            }
        });

        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = "_You can reply with an optional comment as the next message._" }
            }
        });

        return blocks.ToArray();
    }

    private static object[] BuildFreeTextBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":pencil: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = "_Please type your answer as the next message in this channel._" }
            }
        });

        return blocks.ToArray();
    }

    private static void AddContextBlock(List<object> blocks, string? context)
    {
        if (string.IsNullOrWhiteSpace(context)) return;

        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = context }
            }
        });
    }

    // --- Modal helpers ---

    internal async Task<JsonNode?> OpenViewAsync(
        string triggerId, object view, CancellationToken ct)
    {
        var payload = new { trigger_id = triggerId, view };
        return await PostAsync("views.open", payload, ct);
    }

    internal async Task<JsonNode?> UpdateViewAsync(
        string viewId, object view, CancellationToken ct)
    {
        var payload = new { view_id = viewId, view };
        return await PostAsync("views.update", payload, ct);
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
        return $"`[{new string('█', filled)}{new string('░', empty)}]` {step}/{total}";
    }
}

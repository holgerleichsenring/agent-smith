using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// p0374: history caching for the Claude path. The M.E.AI ↔ Anthropic adapter's
/// <c>AutomaticToolsAndSystem</c> mode stamps <c>cache_control</c> on the last
/// system block + last tool ONLY — never on a message. A read-heavy master loop
/// (100+ file-read tool-results accumulated in the conversation) therefore
/// re-pays full input price for that whole history every turn (measured live:
/// ~80k input, only ~29k cached).
///
/// Anthropic caches everything up to AND INCLUDING a <c>cache_control</c>
/// breakpoint, so marking the LAST message caches the entire growing prefix
/// (system + tools + all prior messages) from the next turn on. The adapter
/// exposes no per-message hook, so we place the breakpoint at the wire: this
/// DelegatingHandler edits the outgoing <c>/v1/messages</c> JSON to add an
/// ephemeral <c>cache_control</c> to the last content block of the last message.
///
/// Bounded by Anthropic's 4-breakpoint limit: system(1) + last-tool(1) +
/// last-message(1) = 3, so we skip if the body already carries ≥4. Idempotent
/// (skips a block already marked). Streaming and non-streaming bodies are the
/// same shape, so both are handled. Response parsing is untouched — the SDK
/// still reads cache-read/write usage.
///
/// Known limit: Anthropic's breakpoint walks back at most 20 content blocks, so
/// a single pass that appends &gt;20 blocks can miss the prior cache on the next
/// turn. v1 marks only the tail; a multi-anchor placement is a follow-up.
/// </summary>
internal sealed class ClaudeHistoryCacheHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private const int MaxBreakpoints = 4;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // A caching optimisation must NEVER be a failure mode: any exception while
        // reading or rewriting the body is swallowed and the ORIGINAL request goes
        // to the wire untouched. Only the network send below (base.SendAsync) may
        // surface an error, exactly as if this handler weren't in the chain.
        if (request.Content is not null
            && request.RequestUri?.AbsolutePath.EndsWith("/v1/messages", StringComparison.Ordinal) == true)
        {
            try
            {
                var json = await request.Content.ReadAsStringAsync(cancellationToken);
                if (TryMarkLastMessage(json, out var patched))
                {
                    request.Content = new StringContent(patched, Encoding.UTF8);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Leave request.Content as-is; the send proceeds with the original body.
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    // Adds an ephemeral cache_control to the last content block of the last
    // message, unless the body already has >=4 breakpoints or the block is
    // already marked. Returns false (leaving the body untouched) on any shape it
    // doesn't recognise — the request is never broken by a caching optimisation.
    internal static bool TryMarkLastMessage(string json, out string patched)
    {
        patched = json;
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return false; }

        if (root is not JsonObject obj
            || obj["messages"] is not JsonArray messages || messages.Count == 0)
            return false;

        if (CountBreakpoints(root) >= MaxBreakpoints) return false;

        if (messages[^1] is not JsonObject lastMessage
            || lastMessage["content"] is not JsonArray content || content.Count == 0
            || content[^1] is not JsonObject lastBlock)
            return false;

        if (lastBlock["cache_control"] is not null) return false;

        lastBlock["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        patched = root.ToJsonString();
        return true;
    }

    private static int CountBreakpoints(JsonNode? node) => node switch
    {
        JsonObject o => (o["cache_control"] is not null ? 1 : 0) + o.Sum(kv => CountBreakpoints(kv.Value)),
        JsonArray a => a.Sum(CountBreakpoints),
        _ => 0,
    };
}

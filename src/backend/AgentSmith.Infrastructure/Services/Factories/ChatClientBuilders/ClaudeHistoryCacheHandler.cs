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
/// Bounded by Anthropic's 4-breakpoint limit: the adapter's system(1) +
/// last-tool(1) leave two message slots, which we use for a TWO-anchor scheme
/// (tail + trailing). Idempotent (skips a block already marked). Streaming and
/// non-streaming bodies are the same shape, so both are handled. Response
/// parsing is untouched — the SDK still reads cache-read/write usage.
///
/// p0376: Anthropic's breakpoint walks back at most 20 content blocks to find a
/// prior cache entry, so a single read-heavy turn that appends &gt;20 blocks
/// (parallel file reads = N tool_use + N tool_result) pushed the previous tail
/// breakpoint out of lookback → total cache MISS + a re-write of the whole
/// growing history (measured live: 29% hit rate, cache-WRITE dominating cost).
/// The fix is a second "trailing anchor" placed <see cref="TrailingAnchorOffset"/>
/// content-blocks before the tail: consecutive requests then keep a breakpoint
/// within the 20-block lookback for turns up to ~(20 + offset) blocks, bounding
/// each turn's re-write to the recent delta instead of the entire prefix.
/// </summary>
internal sealed class ClaudeHistoryCacheHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private const int MaxBreakpoints = 4;

    // Blocks-before-tail for the trailing anchor. With a 20-block lookback this
    // keeps the prior tail reachable for turns up to ~(20 + this) blocks — enough
    // for a parallel read of ~19 files (each = tool_use + tool_result = 2 blocks).
    private const int TrailingAnchorOffset = 18;

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

    // Adds ephemeral cache_control to the tail content block AND a trailing anchor
    // TrailingAnchorOffset blocks before it (flattened across messages), within the
    // remaining breakpoint budget. Returns false (leaving the body untouched) on any
    // shape it doesn't recognise or when nothing new could be marked — the request
    // is never broken by a caching optimisation.
    internal static bool TryMarkLastMessage(string json, out string patched)
    {
        patched = json;
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return false; }

        if (root is not JsonObject obj
            || obj["messages"] is not JsonArray messages || messages.Count == 0)
            return false;

        var slots = MaxBreakpoints - CountBreakpoints(root);
        if (slots <= 0) return false;

        // Flatten message content blocks in order so the trailing anchor is placed by
        // absolute distance-from-tail, not per-message. p0376: thinking / redacted_thinking
        // blocks are EXCLUDED as anchor candidates — Anthropic rejects cache_control on them
        // ("redacted_thinking.cache_control: Extra inputs are not permitted", a 400 that
        // killed a run when the tail block happened to be a thinking block under extended
        // thinking). They still occupy positions in the request, just never carry a breakpoint.
        var blocks = new List<JsonObject>();
        foreach (var message in messages)
            if (message is JsonObject mo && mo["content"] is JsonArray ca)
                foreach (var block in ca)
                    if (block is JsonObject bo && !IsThinking(bo)) blocks.Add(bo);
        if (blocks.Count == 0) return false;

        var marked = 0;
        if (MarkBlock(blocks[^1])) marked++;                                   // tail anchor
        if (marked < slots && blocks.Count > TrailingAnchorOffset
            && MarkBlock(blocks[^(TrailingAnchorOffset + 1)])) marked++;        // trailing anchor

        if (marked == 0) return false;
        patched = root.ToJsonString();
        return true;
    }

    private static bool MarkBlock(JsonObject block)
    {
        if (block["cache_control"] is not null) return false;
        block["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        return true;
    }

    // Anthropic forbids cache_control on (redacted_)thinking blocks — a hard 400.
    private static bool IsThinking(JsonObject block)
        => block["type"] is JsonValue v && v.TryGetValue<string>(out var type)
            && type is "thinking" or "redacted_thinking";

    private static int CountBreakpoints(JsonNode? node) => node switch
    {
        JsonObject o => (o["cache_control"] is not null ? 1 : 0) + o.Sum(kv => CountBreakpoints(kv.Value)),
        JsonArray a => a.Sum(CountBreakpoints),
        _ => 0,
    };
}

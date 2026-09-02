using AgentSmith.Application.Services.Loop;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0341f: the ONE conversation a master's passes belong to.
/// <para>
/// A master run is not a single call. It opens with a pass, and the handler may drive it
/// again three ways — apply (it planned but edited nothing), verdict (it edited but said
/// nothing), and the open loop's re-engagement. Each of those built a fresh request, and
/// <see cref="Loop.AgenticLoopRunner"/> opens every request with exactly two messages, so
/// each drive met the model as a stranger while telling it to continue. The only way to
/// obey was to re-derive: on run 98b9 that was 34 passes re-reading one file and re-running
/// four greps, at full input price every time.
/// </para>
/// <para>
/// p0341d preserved the thread WITHIN a pass (compaction middleware) and its criterion
/// "re-engagement now fires only on genuine breaks" was signed off by a test that built the
/// conversation by hand. This type is the boundary that criterion was actually about.
/// </para>
/// <para>
/// It only ever APPENDS. That is not tidiness: the provider's cache keys on the message
/// prefix, so a transcript that grows at the end is read at cache price while one rewritten
/// in the middle is paid for again. Shrinking it when it gets long is the compaction
/// middleware's job, below this.
/// </para>
/// </summary>
public sealed class MasterConversation
{
    private readonly List<ChatMessage> _messages = [];

    /// <summary>
    /// The first pass: the master's own turn — composed exactly as the runner puts it on
    /// the wire, so ticket images survive into the thread — and everything it answered with.
    /// </summary>
    public void Opened(AgenticLoopRequest request, ChatResponse? response)
    {
        _messages.Add(LoopUserMessage.Compose(request.UserPrompt, request.UserImageParts));
        Append(response);
    }

    /// <summary>A later pass: the nudge that drove it and everything it answered with.</summary>
    public void Continued(string nudge, ChatResponse? response)
    {
        _messages.Add(new ChatMessage(ChatRole.User, nudge));
        Append(response);
    }

    /// <summary>
    /// What the next pass continues. A copy, so a request already in flight is never
    /// mutated underneath the provider by the pass that follows it.
    /// </summary>
    public IReadOnlyList<ChatMessage> Thread() => [.. _messages];

    private void Append(ChatResponse? response)
    {
        if (response?.Messages is { Count: > 0 } messages) _messages.AddRange(messages);
    }
}

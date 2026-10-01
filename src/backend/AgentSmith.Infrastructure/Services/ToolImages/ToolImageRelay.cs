using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: <see cref="IToolImageRelay"/>. A withheld image is noted INSIDE the tool
/// result of the call that deposited it, not in a user message of its own: a Copilot
/// continuation must consist of tool messages only, and the note is owed there too.
/// </summary>
public sealed class ToolImageRelay(ToolImageLoopFrames frames, ToolImageMessageComposer composer)
    : IToolImageRelay
{
    public IDisposable OpenLoop() => frames.Open();

    public IList<ChatMessage> Follow(IList<ChatMessage> toolMessages, ToolImageDelivery delivery)
    {
        var frame = frames.Current;
        var pending = frame?.TakePending() ?? [];
        if (frame is null || pending.Count == 0) return toolMessages;
        var shown = delivery.IsDelivered ? frame.ReserveShowings(pending.Count) : 0;
        var reason = delivery.IsDelivered ? ToolImageMessageComposer.CeilingReason : delivery.Reason;
        foreach (var withheld in pending.Skip(shown))
            AppendToResult(toolMessages, withheld.CallId, composer.NotShown(withheld, reason));
        return shown == 0
            ? toolMessages
            : [.. toolMessages, composer.Showing([.. pending.Take(shown)])];
    }

    private static void AppendToResult(IList<ChatMessage> toolMessages, string callId, string note)
    {
        var result = toolMessages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .FirstOrDefault(r => r.CallId == callId);
        if (result is not null)
            result.Result = $"{result.Result}\n\n{note}";
    }
}

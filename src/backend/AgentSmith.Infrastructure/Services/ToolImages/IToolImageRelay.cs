using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: what a tool loop does with deposited images — opens the loop's frame and,
/// after each tool round, places what was deposited behind that round's tool messages.
/// </summary>
public interface IToolImageRelay
{
    /// <summary>Opens a fresh frame for one tool loop on the current async flow.</summary>
    IDisposable OpenLoop();

    /// <summary>
    /// The messages a tool round adds: the tool messages, each withheld image noted in the
    /// result of the call that deposited it, and — when images are shown — ONE user message
    /// carrying them, right after the tool messages.
    /// </summary>
    IList<ChatMessage> Follow(IList<ChatMessage> toolMessages, ToolImageDelivery delivery);
}

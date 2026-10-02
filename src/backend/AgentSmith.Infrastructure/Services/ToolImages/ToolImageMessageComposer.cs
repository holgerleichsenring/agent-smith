using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: the words around a tool's image — the user message that shows images,
/// each preceded by a caption naming the tool and the call, and the note that says an image
/// exists and was not shown. Never base64 in text: the bytes travel only as DataContent.
/// </summary>
public sealed class ToolImageMessageComposer
{
    public static readonly string CeilingReason =
        $"this tool loop has already shown {ToolImageLoopFrame.MaxPerLoop} images, the most one loop shows";

    public ChatMessage Showing(IReadOnlyList<DepositedToolImage> shown)
    {
        var contents = new List<AIContent>();
        foreach (var item in shown)
        {
            contents.Add(new TextContent(
                $"[image from {item.ToolName}, call {item.CallId}: {OneLine(item.Image.Caption)}]"));
            contents.Add(new DataContent(item.Image.Bytes, item.Image.MediaType));
        }
        return new ChatMessage(ChatRole.User, contents);
    }

    public string NotShown(DepositedToolImage item, string reason) =>
        $"[an image exists and was not shown ({reason}): {item.Image.MediaType}, "
        + $"{item.Image.Bytes.Length} bytes — {OneLine(item.Image.Caption)}]";

    private static string OneLine(string caption) =>
        string.Join(' ', caption.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
}

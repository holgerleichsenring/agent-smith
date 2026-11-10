using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Prompts;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// 2026-11-09-0620a: turns one provider request into the blocks it is stored as — the tool list as one block,
/// then each message as one block — rendered as a reader needs them: the text, every tool call with its
/// arguments, every tool result as the model received it, and an image by what identifies it, never its bytes.
/// <para>
/// The rendering is deterministic, because a block is found again by its hash: the same message on the next
/// iteration of a tool loop must render to the same text.
/// </para>
/// </summary>
public static class PromptBlocks
{
    public static IReadOnlyList<PromptBlock> From(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var blocks = new List<PromptBlock>();
        var tools = Tools(options);
        if (tools.Count > 0) blocks.Add(new PromptBlock(PromptBlockKind.Tools, string.Empty, RenderTools(tools)));
        foreach (var message in messages)
            blocks.Add(new PromptBlock(PromptBlockKind.Message, message.Role.Value, Render(message)));
        return blocks;
    }

    public static int ToolCount(ChatOptions? options) => Tools(options).Count;

    private static List<AITool> Tools(ChatOptions? options) => options?.Tools?.ToList() ?? [];

    private static string RenderTools(IEnumerable<AITool> tools)
    {
        var sb = new StringBuilder();
        foreach (var tool in tools)
        {
            sb.Append("## ").AppendLine(tool.Name);
            if (!string.IsNullOrWhiteSpace(tool.Description)) sb.AppendLine(tool.Description.Trim());
            if (tool is AIFunction function)
                sb.AppendLine(function.JsonSchema.GetRawText());
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Render(ChatMessage message)
    {
        var sb = new StringBuilder();
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent text:
                    sb.AppendLine(text.Text);
                    break;
                case FunctionCallContent call:
                    sb.Append("[tool call ").Append(call.Name).Append(" #").Append(call.CallId).AppendLine("]");
                    sb.AppendLine(JsonSerializer.Serialize(call.Arguments));
                    break;
                case FunctionResultContent result:
                    sb.Append("[tool result #").Append(result.CallId).AppendLine("]");
                    sb.AppendLine(ResultText(result.Result));
                    break;
                case DataContent data:
                    sb.AppendLine(DataLine(data));
                    break;
                case UriContent uri:
                    sb.Append("[").Append(uri.MediaType).Append(' ').Append(uri.Uri).AppendLine("]");
                    break;
            }
        }
        return sb.ToString();
    }

    private static string ResultText(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText(),
        _ => JsonSerializer.Serialize(result),
    };

    private static string DataLine(DataContent data) =>
        $"[{data.MediaType}, {data.Data.Length} bytes, sha256 {Convert.ToHexStringLower(SHA256.HashData(data.Data.Span))}]";
}

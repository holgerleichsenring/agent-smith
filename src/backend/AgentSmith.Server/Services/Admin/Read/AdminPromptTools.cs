using System.ComponentModel;
using System.Text;
using AgentSmith.Infrastructure.Persistence.Services.Prompts;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: one stored prompt as the model received it, page by page — a prompt is often longer than one
/// result may be, so a page names the offset the next one starts at.
/// </summary>
public sealed class AdminPromptTools(PromptRecordReader prompts, AdminReadSettings settings)
{
    [Description("Reads a stored prompt (the prompt id comes from read_llm_calls) as its blocks — system, user, tool "
        + "results — in order. Long prompts come in pages: pass the nextOffset a page returns as offset.")]
    public async Task<string> ReadPrompt(string runId, string promptId, int offset = 0, CancellationToken cancellationToken = default)
    {
        var record = await prompts.ReadAsync(runId, promptId, cancellationToken);
        if (record is null) return $"No prompt '{promptId}' is stored for run '{runId}'.";
        var text = new StringBuilder();
        foreach (var block in record.Blocks)
            text.Append("--- ").Append(block.Kind).Append(' ').Append(block.Role).Append('\n').Append(block.Content).Append('\n');
        var start = Math.Clamp(offset, 0, text.Length);
        var length = Math.Min(settings.Chars / 2, text.Length - start);
        var next = start + length < text.Length ? start + length : (int?)null;
        return AdminToolJson.Render(new { record.Id, record.Complete, Length = text.Length, Offset = start, NextOffset = next,
            Text = text.ToString(start, length) });
    }
}

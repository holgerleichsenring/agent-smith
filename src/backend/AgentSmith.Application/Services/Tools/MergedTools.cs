using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-18-9c1cd: the tools that do one job each where two or more tools did it before. Each delegates to the
/// functions it replaces, so their checks and answers stay theirs, and it is offered whether or not a part can act
/// this turn: a part that cannot act answers why, so the master's tool set never changes during a run or a
/// conversation. Dialog-only merges live in <see cref="MergedDialogTools"/>.
/// </summary>
public static partial class MergedTools
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    /// <summary>Invokes <paramref name="tool"/> with the named arguments that carry a value, or answers <paramref name="refusal"/>.</summary>
    public static async Task<object?> Call(AIFunction? tool, string refusal, CancellationToken ct, params (string Name, object? Value)[] arguments)
    {
        if (tool is null) return refusal;
        var args = new AIFunctionArguments();
        foreach (var (name, value) in arguments)
            if (value is not null) args[name] = value;
        return await tool.InvokeAsync(args, ct);
    }

    /// <summary>The function of that name in <paramref name="tools"/>, or null.</summary>
    public static AIFunction? Named(IEnumerable<AITool> tools, string name) =>
        tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == name);

    /// <summary>Whether a path names an image file, which read_file shows as a picture when an upload holds it.</summary>
    public static bool IsImage(string? path) => ImageExtensions.Contains(Path.GetExtension(path ?? string.Empty));


    /// <summary>read_file with view_reference_image (and on a design turn read_mock and the notes) folded in.</summary>
    public static AIFunction ReadFile(
        FilesystemToolHost fs, AIFunction? image, AIFunction? readMock = null, ConversationNotesToolHost? notes = null,
        bool design = false) =>
        AIFunctionFactory.Create(
            async ([Description("Path of the file, from the repository root." + DesignPaths)] string path,
                   [Description("Optional 1-based starting line.")] int? start_line = null,
                   [Description("Optional count of lines from start_line.")] int? line_count = null,
                   [Description("When true (default), each line is prefixed with its number and a tab.")] bool with_line_numbers = true,
                   CancellationToken ct = default) =>
            {
                var trimmed = (path ?? string.Empty).Trim();
                if (design && trimmed.StartsWith(NotesPrefix, StringComparison.Ordinal))
                    return notes is null ? "Error: this conversation keeps no notes here." : await notes.ReadNotes(ct);
                if (design && trimmed.StartsWith(MockPrefix, StringComparison.Ordinal))
                    return await ReadMockAsync(readMock, trimmed, ct);
                if (IsImage(trimmed) && image is not null && IsUploadPath(trimmed))
                    return await Call(image, string.Empty, ct, ("path", trimmed));
                return await fs.ReadFile(path!, start_line, line_count, with_line_numbers, ct);
            },
            "read_file",
            "Reads a file; start_line and line_count select a slice. Lines are numbered so you can cite file:line. An "
            + "image file (.png, .jpg, .gif, .webp) inside an upload is shown to you as a picture — your vision is the OCR."
            + (design ? " Also reads draft:<path> (your draft; directory_tree draft: lists it), mock: (every mock version) or mock:<spec_id>[/v<n>] (a mock's HTML, newest by default), and notes: (this conversation's notes)." : string.Empty));

    private const string DesignPaths = " On a design conversation also draft:<path>, mock:, mock:<spec_id>[/v<n>] or notes:.";

    /// <summary>The notes address on a design turn.</summary>
    public const string NotesPrefix = "notes:";

    /// <summary>The mock address on a design turn.</summary>
    public const string MockPrefix = "mock:";

    private static bool IsUploadPath(string path) =>
        path.StartsWith(Specs.ReferenceScopeName.Prefix, StringComparison.Ordinal)
        || path.Contains(".agentsmith/reference/", StringComparison.Ordinal);

    private static async Task<object?> ReadMockAsync(AIFunction? readMock, string path, CancellationToken ct)
    {
        var (spec, version) = MockAddress(path);
        return await Call(readMock, "Error: this conversation keeps no mocks.", ct, ("spec_id", spec), ("version", version));
    }

    /// <summary>mock: → (null, null); mock:&lt;spec&gt; → (spec, null); mock:&lt;spec&gt;/v&lt;n&gt; → (spec, n).</summary>
    public static (string? Spec, int? Version) MockAddress(string path)
    {
        var rest = path.Trim()[MockPrefix.Length..].Trim('/');
        if (rest.Length == 0) return (null, null);
        var slash = rest.IndexOf('/');
        if (slash < 0) return (rest, null);
        var tail = rest[(slash + 1)..].TrimStart('v', 'V');
        return (rest[..slash], int.TryParse(tail, out var n) ? n : null);
    }
}

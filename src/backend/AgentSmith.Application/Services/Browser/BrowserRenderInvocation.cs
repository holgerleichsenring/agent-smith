using System.Text.Json;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Constants;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: one run of the baked render script in a browser sandbox. node is handed one
/// server-shaped path — the request file — and never a URL or a selector on its command line; the
/// result and each screenshot come back as files. A screenshot is written base64-encoded because
/// a sandbox read returns text, and the script keeps the JPEG under 700 KB so its encoding fits
/// one read.
/// </summary>
public sealed class BrowserRenderInvocation(ISandboxFileReaderFactory files)
{
    internal const string RequestFile = "request.json";
    internal const string ResultFile = "result.json";
    internal const int RenderTimeoutSeconds = 240;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The script's report and the screenshot bytes, or why there is no report.</summary>
    public async Task<(BrowserRenderResult? Result, IReadOnlyList<byte[]> Shots, string? Failure)> RunAsync(
        ISandbox sandbox, BrowserRenderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(request);
        var io = files.Create(sandbox);
        var requestPath = $"{request.OutDir}/{RequestFile}";
        await io.WriteAsync(requestPath, JsonSerializer.Serialize(request, Json), ct);
        var run = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "node", Args: [BrowserImageDefaults.RenderScriptPath, requestPath],
            WorkingDirectory: request.OutDir, TimeoutSeconds: RenderTimeoutSeconds), null, ct);
        var text = await io.TryReadAsync($"{request.OutDir}/{ResultFile}", ct);
        if (text is null)
            return (null, [], run.TimedOut
                ? $"the render did not finish within {RenderTimeoutSeconds}s"
                : $"the render script wrote no result (exit {run.ExitCode}): {run.ErrorMessage ?? run.OutputContent}");
        var result = JsonSerializer.Deserialize<BrowserRenderResult>(text, Json) ?? new BrowserRenderResult();
        var shots = new List<byte[]>();
        foreach (var shot in result.Shots)
            shots.Add(Convert.FromBase64String((await io.ReadRequiredAsync($"{request.OutDir}/{Path.GetFileName(shot.File)}", ct)).Trim()));
        return (result, shots, null);
    }
}

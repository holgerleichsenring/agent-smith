using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.References;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283de: a browser sandbox with a file system and a render script that answers: a
/// `node` step reads the request file it was handed, records it, and writes a result naming the
/// requested selectors plus one desktop screenshot, base64-encoded like the real script writes it.
/// </summary>
internal sealed class BrowserFakeSandbox : IHoldableSandbox
{
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private readonly InMemoryFileSandbox _files = new();

    public Dictionary<string, byte[]> Files => _files.Files;

    public List<JsonElement> Requests { get; } = [];

    public List<Step> Runs { get; } = [];

    public string JobId => _files.JobId;

    public async Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (step.Kind != StepKind.Run || step.Command != "node")
            return await _files.RunStepAsync(step, progress, cancellationToken);
        Runs.Add(step);
        var request = JsonDocument.Parse(Files[step.Args![1]]).RootElement.Clone();
        Requests.Add(request);
        var outDir = request.GetProperty("outDir").GetString()!;
        Files[$"{outDir}/desktop.jpg.b64"] = Encoding.UTF8.GetBytes(Convert.ToBase64String(Jpeg));
        Files[$"{outDir}/result.json"] = Encoding.UTF8.GetBytes(Result(request));
        return new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, string.Empty);
    }

    private static string Result(JsonElement request) => JsonSerializer.Serialize(new
    {
        url = request.GetProperty("url").GetString() ?? "http://127.0.0.1:41234/index.html",
        title = "Fixture",
        styles = request.GetProperty("selectors").EnumerateArray().Select(s => new
        {
            selector = s.GetString(),
            count = s.GetString() == "button" ? 1 : 0,
            values = s.GetString() == "button" ? new Dictionary<string, string> { ["background-color"] = "rgb(192, 255, 238)" } : null,
        }),
        consoleErrors = new[] { "Uncaught TypeError: x is undefined" },
        failedRequests = Array.Empty<object>(),
        refused = new[] { new { url = "http://127.0.0.1:6379/", reason = "127.0.0.1 is loopback" } },
        shots = new[] { new { viewport = "desktop", file = "desktop.jpg.b64", width = 1440, height = 900, pageHeight = 900, capturedHeight = 900 } },
    });

    public Task ForceRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

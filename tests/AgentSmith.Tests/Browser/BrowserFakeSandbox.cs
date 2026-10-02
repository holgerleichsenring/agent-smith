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
        Files[$"{outDir}/result.json"] = Encoding.UTF8.GetBytes(
            request.TryGetProperty("compare", out var job) && job.ValueKind == JsonValueKind.Object ? Compared(job) : Result(request));
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

    // 2026-10-01-283di: a comparison — h1 computes 16px on the reference and 15px on the candidate,
    // a selector named "missing" matches nothing, the pages are 900 and 1400 px tall.
    private string Compared(JsonElement job)
    {
        Compares.Add(job);
        object Rows(string side) => job.GetProperty("pairs").EnumerateArray().Select(p => p.GetProperty(side).GetString()!).Select(selector => new
        {
            selector, count = selector == "missing" ? 0 : 1,
            values = selector == "missing" ? null : new Dictionary<string, string>
            {
                ["font-size"] = selector == "h1" && side == "reference" ? "16px" : "15px", ["color"] = "rgb(1, 2, 3)",
            },
        }).ToList();
        var viewports = job.GetProperty("viewports").EnumerateArray().Select(v => v.GetString()!).ToList();
        return JsonSerializer.Serialize(new
        {
            url = "http://127.0.0.1:41235/", title = (string?)null, styles = Array.Empty<object>(),
            consoleErrors = Array.Empty<string>(), failedRequests = Array.Empty<object>(), refused = Array.Empty<object>(),
            shots = viewports.Select(v => new { viewport = v, file = "desktop.jpg.b64", width = 1440, height = 1400, pageHeight = 1400, capturedHeight = 1400 }),
            compare = new
            {
                referenceUrl = "http://127.0.0.1:41234/", candidateUrl = "http://127.0.0.1:41235/",
                viewports = viewports.Select(v => new
                {
                    viewport = v, reference = Rows("reference"), candidate = Rows("candidate"),
                    referenceHeight = 900, candidateHeight = 1400, paddedHeight = 1400, mismatchedPixels = 720000, mismatchRatio = 0.357,
                }),
            },
        });
    }

    public List<JsonElement> Compares { get; } = [];

    public Task ForceRemoveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

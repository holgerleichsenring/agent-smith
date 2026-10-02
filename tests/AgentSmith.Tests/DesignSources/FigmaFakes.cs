using System.Net;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7ab: Figma's REST API as its documentation describes it — no live token exists in
/// this repository, so every response here is built from the documented shapes, and what these
/// tests prove is proven against that contract only.
/// </summary>
internal static class FigmaFakes
{
    public const string SecretName = "figma-token";
    public const string Token = "figd_SECRET-TOKEN-7f7ab";
    public const string Link = "https://www.figma.com/design/AbCdEf123456/Checkout?node-id=1-2&t=xyz";

    public const string Nodes = """
        {"name":"Checkout","lastModified":"2026-09-30T10:00:00Z","thumbnailUrl":"https://example.test/t.png",
         "version":"4242","role":"viewer","editorType":"figma",
         "nodes":{"1:2":{"document":{"id":"1:2","name":"Pay row","type":"FRAME",
           "absoluteBoundingBox":{"x":0,"y":0,"width":360,"height":56},
           "layoutMode":"HORIZONTAL","itemSpacing":8,"paddingLeft":16,"paddingRight":16,"paddingTop":12,
           "paddingBottom":12,"primaryAxisAlignItems":"SPACE_BETWEEN","counterAxisAlignItems":"CENTER",
           "fills":[{"type":"SOLID","color":{"r":1,"g":1,"b":1,"a":1}}],"cornerRadius":8,
           "boundVariables":{"itemSpacing":{"type":"VARIABLE_ALIAS","id":"VariableID:1:5"}},
           "children":[
             {"id":"1:3","name":"Label","type":"TEXT","characters":"Pay now",
              "style":{"fontFamily":"Inter","fontWeight":600,"fontSize":16,"lineHeightPx":24},
              "fills":[{"type":"SOLID","color":{"r":0,"g":0.333,"b":1,"a":1}}],"styles":{"text":"S:1"}},
             {"id":"1:4","name":"Button","type":"INSTANCE","componentId":"C:9",
              "strokes":[{"type":"SOLID","color":{"r":0,"g":0,"b":0,"a":0.5}}],"strokeWeight":1},
             {"id":"1:5","name":"Hidden","type":"RECTANGLE","visible":false}]},
           "components":{"C:9":{"key":"k","name":"Button/Primary","description":""}},
           "styles":{"S:1":{"key":"s","name":"Heading/H3","styleType":"TEXT"}}}}}
        """;

    public const string Variables = """
        {"status":200,"error":false,"meta":{
          "variables":{
            "VariableID:1:5":{"id":"VariableID:1:5","name":"space/sm","variableCollectionId":"VC:1",
              "resolvedType":"FLOAT","valuesByMode":{"M:1":8,"M:2":8}},
            "VariableID:1:6":{"id":"VariableID:1:6","name":"color/brand","variableCollectionId":"VC:1",
              "resolvedType":"COLOR","valuesByMode":{"M:1":{"r":0,"g":0.333,"b":1,"a":1},
              "M:2":{"type":"VARIABLE_ALIAS","id":"VariableID:1:5"}}}},
          "variableCollections":{"VC:1":{"id":"VC:1","name":"Tokens",
            "modes":[{"modeId":"M:1","name":"Light"},{"modeId":"M:2","name":"Dark"}],"defaultModeId":"M:1"}}}}
        """;

    public static FigmaClient Client(FakeFigmaHandler handler, TimeProvider? clock = null, string? token = Token) =>
        new(new HttpClient(handler, disposeHandler: false) { BaseAddress = FigmaClient.ApiHost },
            new StubSecrets(token), clock ?? TimeProvider.System, Downloader(handler), NullLogger<FigmaClient>.Instance);

    /// <summary>2026-10-01-7f7ac: the token-less download client over the same recording handler.</summary>
    public static FigmaImageDownloader Downloader(FakeFigmaHandler handler) =>
        new(new HttpClient(handler, disposeHandler: false), NullLogger<FigmaImageDownloader>.Instance);

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status, string body = "{\"status\":0,\"err\":\"FIGMA-BODY-MARKER\"}")
        => Json(body, status);

    public static HttpResponseMessage RateLimited(int seconds)
    {
        var response = Status(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", seconds.ToString());
        return response;
    }

    private sealed class StubSecrets(string? token) : ISecretValues
    {
        public string? Resolve(string name) => name == SecretName ? token : null;

        public IReadOnlyList<string> All() => token is null ? [] : [token];
    }
}

/// <summary>Answers by path — nodes and variables each from their own queue — and records every request.</summary>
internal sealed class FakeFigmaHandler : HttpMessageHandler
{
    public Queue<Func<HttpResponseMessage>> NodeResponses { get; } = new();

    public Queue<Func<HttpResponseMessage>> VariableResponses { get; } = new();

    /// <summary>2026-10-01-7f7ac: answers to GET /v1/images/:key.</summary>
    public Queue<Func<HttpResponseMessage>> ImageResponses { get; } = new();

    /// <summary>2026-10-01-7f7ac: answers to any host but the API's — the export download.</summary>
    public Queue<Func<HttpResponseMessage>> DownloadResponses { get; } = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public static FakeFigmaHandler Answering(HttpStatusCode variables = HttpStatusCode.OK)
    {
        var handler = new FakeFigmaHandler();
        handler.NodeResponses.Enqueue(() => FigmaFakes.Json(FigmaFakes.Nodes));
        handler.VariableResponses.Enqueue(() => variables == HttpStatusCode.OK
            ? FigmaFakes.Json(FigmaFakes.Variables) : FigmaFakes.Status(variables));
        return handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        var uri = request.RequestUri!;
        var queue = uri.Host != FigmaClient.ApiHost.Host ? DownloadResponses
            : uri.AbsolutePath.StartsWith("/v1/images/") ? ImageResponses
            : uri.AbsolutePath.EndsWith("/variables/local") ? VariableResponses : NodeResponses;
        return Task.FromResult(queue.Count > 0 ? queue.Dequeue()() : FigmaFakes.Status(HttpStatusCode.InternalServerError));
    }
}

/// <summary>A clock whose timers fire at once, recording the wait each was asked for.</summary>
internal sealed class InstantClock : TimeProvider
{
    public List<TimeSpan> Waits { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Waits.Add(dueTime);
        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new Stopped();
    }

    private sealed class Stopped : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

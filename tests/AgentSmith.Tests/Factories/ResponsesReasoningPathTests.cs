using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-11-25-9992k: a catalog model with <c>api: responses</c> is called on the OpenAI Responses API — on Azure at the
/// resource's v1 path by deployment — statelessly, with its reasoning items carried across a tool loop; a model without
/// it keeps the Chat Completions request it sent before. Observed on the wire through the builder's test transport, with
/// response payloads shaped like the documented Responses JSON
/// (https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/reasoning).
/// </summary>
public sealed class ResponsesReasoningPathTests
{
    private const string Endpoint = "https://res.openai.azure.com/";
    private static readonly AgentConfig Azure = new() { Type = "azure_openai", Endpoint = Endpoint };

    [Fact]
    public async Task ChatCompletions_ApiUnset_SendsTheRequestItSentBefore()
    {
        var wire = new ScriptedWire(ChatCompletion);
        var client = Build(wire, new ModelAssignment { Model = "gpt-5.6", Deployment = "terra" });

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { MaxOutputTokens = 100 });

        wire.Uris.Single().Should().StartWith($"{Endpoint}openai/deployments/terra/chat/completions");
        wire.Bodies.Single().Should().Be("""{"messages":[{"role":"user","content":"hi"}],"model":"terra","max_completion_tokens":100}""");
    }

    [Fact]
    public void ReasoningEffort_OnChatCompletions_IsRefusedNamingTheKeys()
    {
        var build = () => Build(new ScriptedWire(ChatCompletion),
            new ModelAssignment { Model = "gpt-5.6", Deployment = "terra", ReasoningEffort = "high" });

        build.Should().Throw<ConfigurationException>().WithMessage("*reasoning_effort 'high'*api: responses*");
    }

    [Fact]
    public void UnknownApi_IsRefusedByName()
    {
        var build = () => Build(new ScriptedWire(ChatCompletion), new ModelAssignment { Model = "m", Api = "assistants" });

        build.Should().Throw<ConfigurationException>().WithMessage("*api 'assistants'*chat_completions, responses*");
    }

    [Fact]
    public async Task Responses_OnAzure_PostsStatelessReasoningToTheV1Path()
    {
        var wire = new ScriptedWire(FinalAnswer);
        var client = Build(wire, Gpt6("high"));

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { MaxOutputTokens = 4096 });

        wire.Uris.Single().Should().Be($"{Endpoint}openai/v1/responses");
        wire.Authorizations.Single().Should().Be("Bearer az-key");
        var body = JsonNode.Parse(wire.Bodies.Single())!;
        body["model"]!.GetValue<string>().Should().Be("sol-deployment");
        body["store"]!.GetValue<bool>().Should().BeFalse();
        body["include"]!.AsArray().Select(i => i!.GetValue<string>()).Should().Contain("reasoning.encrypted_content");
        body["reasoning"]!["effort"]!.GetValue<string>().Should().Be("high");
        body["max_output_tokens"]!.GetValue<int>().Should().Be(4096);
        body["temperature"].Should().BeNull();
        body["top_p"].Should().BeNull();
    }

    [Fact]
    public async Task Responses_EffortTheSdkHasNoNameFor_IsSentAsWritten()
    {
        var wire = new ScriptedWire(FinalAnswer);

        await Build(wire, Gpt6("xhigh")).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        JsonNode.Parse(wire.Bodies.Single())!["reasoning"]!["effort"]!.GetValue<string>().Should().Be("xhigh");
    }

    [Fact]
    public async Task Responses_WithoutEffort_SendsNoReasoningOptions()
    {
        var wire = new ScriptedWire(FinalAnswer);

        await Build(wire, Gpt6(null)).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        JsonNode.Parse(wire.Bodies.Single())!["reasoning"].Should().BeNull();
    }

    [Fact]
    public async Task Responses_A429_IsTriedOnce()
    {
        var wire = new ScriptedWire(FinalAnswer) { Status = HttpStatusCode.TooManyRequests };

        var call = () => Build(wire, Gpt6("low")).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        await call.Should().ThrowAsync<Exception>();
        wire.Bodies.Should().ContainSingle("the SDK's own retries are off; our layer above decides");
    }

    [Fact]
    public async Task Responses_ToolLoop_SendsTheReasoningItemBackBeforeTheToolOutput()
    {
        var wire = new ScriptedWire(ReasoningThenToolCall, FinalAnswer);
        var lookup = AIFunctionFactory.Create((string q) => $"found {q}", "lookup");
        var client = new ChatClientBuilder(Build(wire, Gpt6("medium"))).UseFunctionInvocation().Build();

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "find x")], new ChatOptions { Tools = [lookup] });

        response.Text.Should().Be("done");
        var input = JsonNode.Parse(wire.Bodies[1])!["input"]!.AsArray().Select(i => i!).ToList();
        var reasoning = input.FindIndex(i => i["type"]?.GetValue<string>() == "reasoning");
        var output = input.FindIndex(i => i["type"]?.GetValue<string>() == "function_call_output");
        reasoning.Should().BeGreaterThan(-1, "the stateless loop must hand the reasoning back");
        input[reasoning]["id"]!.GetValue<string>().Should().Be("rs_1");
        input[reasoning]["encrypted_content"]!.GetValue<string>().Should().Be("ENC-1");
        output.Should().BeGreaterThan(reasoning);
        input[output]["call_id"]!.GetValue<string>().Should().Be("call_1");
    }

    [Fact]
    public async Task Responses_ThreadReloadedThroughTheDialogCodec_IsSentWithoutReasoningItems()
    {
        var first = new ScriptedWire(ReasoningThenToolCall, FinalAnswer);
        var lookup = AIFunctionFactory.Create((string q) => $"found {q}", "lookup");
        var loop = new ChatClientBuilder(Build(first, Gpt6("medium"))).UseFunctionInvocation().Build();
        List<ChatMessage> thread = [new ChatMessage(ChatRole.User, "find x")];
        thread.AddRange((await loop.GetResponseAsync(thread, new ChatOptions { Tools = [lookup] })).Messages);
        var codec = new DialogThreadCodec(new SecretMasker(new AgentSmith.Contracts.Services.LoadedSecretValues(
            new AgentSmithConfig())));
        var reloaded = codec.Decode(codec.Encode(thread)).Append(new ChatMessage(ChatRole.User, "and y?")).ToList();

        var second = new ScriptedWire(FinalAnswer);
        await Build(second, Gpt6("medium")).GetResponseAsync(reloaded);

        var input = JsonNode.Parse(second.Bodies.Single())!["input"]!.AsArray();
        input.Should().NotContain(i => i!["type"] != null && i["type"]!.GetValue<string>() == "reasoning");
        input.Should().Contain(i => i!["type"] != null && i["type"]!.GetValue<string>() == "function_call_output");
    }

    [Fact]
    public async Task Responses_Usage_CarriesCachedAndReasoningTokens()
    {
        var response = await Build(new ScriptedWire(FinalAnswer), Gpt6("low"))
            .GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Usage!.InputTokenCount.Should().Be(1200);
        response.Usage.CachedInputTokenCount.Should().Be(1024);
        response.Usage.OutputTokenCount.Should().Be(300);
        response.Usage.ReasoningTokenCount.Should().Be(256, "reasoning tokens are inside the output count");
    }

    [Fact]
    public async Task ChatCompletions_Usage_CarriesReasoningTokensToo()
    {
        var response = await Build(new ScriptedWire(ChatCompletion), new ModelAssignment { Model = "gpt-5.6", Deployment = "terra" })
            .GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Usage!.ReasoningTokenCount.Should().Be(7);
        response.Usage.CachedInputTokenCount.Should().Be(3);
    }

    [Theory]
    [InlineData("https://res.openai.azure.com", "https://res.openai.azure.com/openai/v1/")]
    [InlineData("https://res.openai.azure.com/", "https://res.openai.azure.com/openai/v1/")]
    [InlineData("https://res.openai.azure.com/openai", "https://res.openai.azure.com/openai/v1/")]
    [InlineData("https://res.openai.azure.com/openai/v1/", "https://res.openai.azure.com/openai/v1/")]
    public void AzureV1_NormalisesTheResourceEndpoint(string endpoint, string expected) =>
        ResponsesReasoningCall.AzureV1(endpoint).ToString().Should().Be(expected);

    private static ModelAssignment Gpt6(string? effort) => new()
    {
        Model = "gpt-6-sol", Deployment = "sol-deployment", Api = ModelApi.Responses, ReasoningEffort = effort,
    };

    private static IChatClient Build(ScriptedWire wire, ModelAssignment assignment) =>
        new OpenAiChatClientBuilder(
                TestCredentials.Isolated(new Dictionary<string, string> { ["AZURE_OPENAI_API_KEY"] = "az-key" }), wire)
            .Build(Azure, assignment);

    private const string ChatCompletion = """
        {"id":"c","object":"chat.completion","created":1,"model":"gpt-5.6",
         "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":10,"completion_tokens":9,"total_tokens":19,
                  "prompt_tokens_details":{"cached_tokens":3},"completion_tokens_details":{"reasoning_tokens":7}}}
        """;

    private const string ReasoningThenToolCall = """
        {"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"gpt-6-sol",
         "output":[
           {"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"ENC-1"},
           {"type":"function_call","id":"fc_1","call_id":"call_1","name":"lookup","arguments":"{\"q\":\"x\"}","status":"completed"}],
         "usage":{"input_tokens":100,"input_tokens_details":{"cached_tokens":0},"output_tokens":40,
                  "output_tokens_details":{"reasoning_tokens":32},"total_tokens":140}}
        """;

    private const string FinalAnswer = """
        {"id":"resp_2","object":"response","created_at":2,"status":"completed","model":"gpt-6-sol",
         "output":[
           {"type":"message","id":"msg_1","role":"assistant","status":"completed",
            "content":[{"type":"output_text","text":"done","annotations":[]}]}],
         "usage":{"input_tokens":1200,"input_tokens_details":{"cached_tokens":1024},"output_tokens":300,
                  "output_tokens_details":{"reasoning_tokens":256},"total_tokens":1500}}
        """;

    /// <summary>Answers each request with the next scripted body (the last one repeats) and keeps what was sent.</summary>
    private sealed class ScriptedWire(params string[] responses) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public List<string> Uris { get; } = [];
        public List<string> Bodies { get; } = [];
        public List<string?> Authorizations { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uris.Add(request.RequestUri!.ToString());
            Authorizations.Add(request.Headers.Authorization?.ToString());
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            var body = responses[Math.Min(Bodies.Count - 1, responses.Length - 1)];
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Status == HttpStatusCode.OK ? body : """{"error":{"message":"rate"}}""",
                    Encoding.UTF8, "application/json"),
            };
        }
    }
}

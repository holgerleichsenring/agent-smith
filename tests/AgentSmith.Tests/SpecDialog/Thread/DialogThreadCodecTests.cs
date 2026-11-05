using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class DialogThreadCodecTests
{
    private const string Secret = "s3cr3t-token-value";

    [Fact]
    public void DialogThreadCodec_RoundTrip_KeepsPairsMasksWithPlaceholderDropsImagesAndOrphans()
    {
        var codec = DialogThreadStores.Codec(Secret);
        IReadOnlyList<ChatMessage> thread =
        [
            new(ChatRole.User, [new TextContent($"use {Secret}"), new DataContent(new byte[] { 1, 2 }, "image/png")]),
            new(ChatRole.Assistant, [new TextReasoningContent("thinking")]),
            new(ChatRole.Assistant, [
                new FunctionCallContent("c1", "read_file", new Dictionary<string, object?> { ["path"] = "a.cs" }),
                new FunctionCallContent("c2", "read_file")]),
            new(ChatRole.Tool, [new FunctionResultContent("c1", $"class A {{ {Secret} }}"), new FunctionResultContent("c9", "orphan")]),
            new(ChatRole.Assistant, [new FunctionCallContent("c3", "grep")]),
            new(ChatRole.Tool, [new FunctionResultContent("c3", new { hits = 2 })]),
        ];

        var back = codec.Decode(codec.Encode(thread));

        back.Select(m => m.Role).Should().Equal(ChatRole.User, ChatRole.Assistant, ChatRole.Tool, ChatRole.Assistant, ChatRole.Tool);
        back[0].Contents.OfType<TextContent>().Select(t => t.Text).Should().Equal(
            $"use {SecretMasker.ProviderPlaceholder}", DialogThreadCodec.ImageMarker);
        var call = back[1].Contents.Should().ContainSingle().Which.Should().BeOfType<FunctionCallContent>().Subject;
        (call.CallId, call.Name, call.Arguments!["path"]!.ToString()).Should().Be(("c1", "read_file", "a.cs"));
        back[2].Contents.Should().ContainSingle().Which.Should().BeOfType<FunctionResultContent>()
            .Which.Result.Should().Be($"class A {{ {SecretMasker.ProviderPlaceholder} }}");
        back[4].Contents.OfType<FunctionResultContent>().Single().Result.Should().Be("""{"hits":2}""");
    }
}

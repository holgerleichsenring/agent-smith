using AgentSmith.Contracts.Prompts;
using AgentSmith.Infrastructure.Persistence.Services.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-11-18-e06bd: a call in two lines — what it was fed (its last message, a tool result named by the call that asked
/// for it) and what it decided (the answer its successor in the tool loop carries).
/// </summary>
public sealed class PromptDigestTests : IDisposable
{
    private const string Run = "2026-11-18T10-00-00-aaaa";
    private readonly PromptStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public void Fed_ToolResult_NamesTheToolThatAskedForIt()
    {
        var fed = PromptDigestLines.Fed("tool", "[tool result #c1]\n\nnamespace Shop;\nclass A {}",
            ["[tool call ReadFile #c1]\n{\"path\":\"src/A.cs\"}"]);

        fed.Should().Be("ReadFile result: namespace Shop;");
    }

    [Fact]
    public void Fed_SeveralResults_SaysHowManyMore()
    {
        PromptDigestLines.Fed("tool", "[tool result #c1]\nok\n[tool result #c2]\nok", [])
            .Should().Be("tool result: ok (+1 more)");
    }

    [Fact]
    public void Fed_UserMessage_RoleAndFirstLine()
    {
        PromptDigestLines.Fed("user", "\nImplement the spec.\nMore text.", []).Should().Be("user: Implement the spec.");
    }

    [Fact]
    public void Decided_ToolCalls_NameAndArgumentValues()
    {
        PromptDigestLines.Decided("Reading both.\n[tool call ReadFile #c1]\n{\"path\":\"src/A.cs\"}\n[tool call RunCommand #c2]\n{\"command\":\"dotnet test\",\"timeout\":60}\n")
            .Should().Be("ReadFile src/A.cs; RunCommand dotnet test 60");
    }

    [Fact]
    public void Decided_PlainAnswer_FirstLine_CutAtTheLimit()
    {
        PromptDigestLines.Decided("Done: the build passes.\nDetails follow.").Should().Be("Done: the build passes.");
        var cut = PromptDigestLines.Decided(new string('x', 400))!;
        cut.Should().HaveLength(PromptDigestLines.MaxChars).And.EndWith("…");
    }

    [Fact]
    public async Task PromptDigestReader_FedFromTheLastBlock_DecidedFromTheSuccessor()
    {
        var first = await RecordAsync(Tools, User("Implement the spec."));
        var second = await RecordAsync(Tools, User("Implement the spec."),
            Assistant("[tool call ReadFile #c1]\n{\"path\":\"src/A.cs\"}"), ToolResult("[tool result #c1]\nclass A {}"));
        var unknown = "nothing-like-this";

        using var db = _store.Db();
        var digest = await new PromptDigestReader(db).ReadAsync(Run, [first!, second!, unknown], default);

        digest.Should().Equal(
            new PromptDigestView(first!, "user: Implement the spec.", "ReadFile src/A.cs"),
            new PromptDigestView(second!, "ReadFile result: class A {}", null));
    }

    private async Task<string?> RecordAsync(params PromptBlock[] blocks)
    {
        var id = await _store.Recorder.RecordAsync(Run, blocks, 1, default);
        await _store.Recorder.DrainAsync();
        return id;
    }

    private static readonly PromptBlock Tools = new(PromptBlockKind.Tools, string.Empty, "tools");
    private static PromptBlock User(string text) => new(PromptBlockKind.Message, "user", text);
    private static PromptBlock Assistant(string text) => new(PromptBlockKind.Message, "assistant", text);
    private static PromptBlock ToolResult(string text) => new(PromptBlockKind.Message, "tool", text);
}

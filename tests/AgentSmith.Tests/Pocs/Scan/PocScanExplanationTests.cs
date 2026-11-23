using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Pocs.Scan;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: one call for a blocked verdict over masked findings only — never a raw value or a file
/// body; none for a clean verdict; a failed call leaves no text and says why.</summary>
public sealed class PocScanExplanationTests
{
    private readonly ExplainChats _chats = new();

    [Fact]
    public async Task Explain_ABlockedVerdict_PromptsWithTheMaskedSnippetOnly()
    {
        var verdict = new PocScanVerdict([new PocScanFinding("sec:1", PocScanFindingKinds.Secret, "aws-access-key", "src/config.py", 14,
            "AWS Access Key ID", "critical", "KEY = \"<secret at src/config.py:14>\"", "Remove it", [])], []);

        var (text, why) = await ExplainAsync(verdict);

        (text, why).Should().Be((_chats.Answer, null));
        _chats.Prompts.Should().ContainSingle().Which.Should().Contain("<secret at src/config.py:14>").And.Contain("cannot change it")
            .And.NotContain("AKIA");
    }

    [Fact]
    public async Task Explain_ACleanVerdict_MakesNoCall_AFailingCallSaysWhy()
    {
        (await ExplainAsync(new PocScanVerdict([], []))).Should().Be(((string?)null, (string?)null));
        _chats.Prompts.Should().BeEmpty();

        _chats.Fails = true;
        var blocked = new PocScanVerdict([new PocScanFinding("osv:x", PocScanFindingKinds.Vulnerability, "x", "f", null, "t", "d", null, null, [])], []);
        (await ExplainAsync(blocked)).Failure.Should().Contain("unreachable");
    }

    private Task<(string? Text, string? Failure)> ExplainAsync(PocScanVerdict verdict) =>
        new PocScanExplanation(_chats, new PocScanPrompt(), new SecretMasker(new LoadedSecretValues(new AgentSmithConfig())),
                Mock.Of<IRunContextAccessor>(), NullLogger<PocScanExplanation>.Instance)
            .ExplainAsync("todo", verdict, new AgentConfig { Type = "claude" }, PipelineCostTracker.GetOrCreate(new PipelineContext()), default);
}

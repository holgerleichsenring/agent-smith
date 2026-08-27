using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-08-25-c9c7: a context that describes a stack names the image that stack runs in.
/// <para>
/// The rule shipped with p0265 and the write path did check it — but only once a stack
/// block existed, so a model that omitted the block entirely walked straight past it and
/// the sandbox fell back to the language convention table. p0504's domain exemption is
/// the one hole that stays open on purpose: that context's profile brings the image.
/// </para>
/// <para>
/// The document is then judged against the shipped context.schema.json, and every refusal
/// is spent from a bounded budget — a schema error returns as a tool result and the model
/// re-emits, which is exactly how a rejection becomes a loop.
/// </para>
/// </summary>
public sealed class ContextImageRuleTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly ContextYamlSerializer _serializer = new(new ContextYamlBuilders());

    public ContextImageRuleTests() =>
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));

    [Fact]
    public async Task Write_AContextWithNoStackBlock_IsRejected()
    {
        var result = await WriteAsync("""{ "meta": { "workdir": "." } }""");

        result.Should().StartWith("Error:");
        result.Should().Contain("/stack:", "the refusal points at the block that is missing");
        result.Should().Contain("stack block is required");
        NothingWasWritten();
    }

    [Fact]
    public async Task Write_AContextWithAStackAndNoImage_IsRejected()
    {
        var result = await WriteAsync("""
            { "meta": { "workdir": "." }, "stack": { "lang": "C#", "runtime": ".NET 8" } }
            """);

        result.Should().Contain("/stack/image:");
        result.Should().Contain("stack.image is required");
        NothingWasWritten();
    }

    [Fact]
    public async Task Write_AContextDeclaringADomainAndNoImage_IsAccepted()
    {
        // p0504: the domain's catalog profile supplies the toolchain image and the
        // verification commands. A blanket "reject a missing image" would revert it.
        var result = await WriteAsync("""
            { "meta": { "workdir": ".", "domain": "data-warehouse" } }
            """);

        result.Should().StartWith("context.yaml written:");
    }

    [Fact]
    public async Task Write_AnInvalidContext_TheRejectionNamesTheDefect()
    {
        // quality.lang is a DECIDED vocabulary and stays closed: a language policy has
        // three states and picking one is a decision. The model never sees the schema,
        // so a refusal that only says "should match one of the enum values" is an
        // invitation to guess — and guessing is what turns one refusal into a loop.
        // 2026-08-26-167c: this used to send `"type": ["microservice"]`, which the open
        // meta.type now accepts — an honest archetype is no longer a defect.
        var result = await WriteAsync("""
            {
              "meta": { "workdir": "." },
              "stack": { "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0" },
              "quality": { "lang": "denglisch" }
            }
            """);

        result.Should().StartWith("Error:");
        result.Should().Contain("/quality/lang", "a JSON Pointer to the offending node");
        result.Should().Contain("english-only", "the allowed values are quoted out of the schema");
        NothingWasWritten();
    }

    [Fact]
    public async Task Write_AnHonestValueTheListNeverHad_IsAccepted()
    {
        // 2026-08-26-167c: the descriptive vocabularies suggest. A model refused for
        // naming its architecture honestly spends its budget being told to lie.
        var result = await WriteAsync("""
            {
              "meta": { "workdir": ".", "type": ["data-platform"] },
              "stack": { "lang": "Python", "image": "python:3.12-bookworm" },
              "arch": { "style": "Medallion", "patterns": ["Dependency Injection"] }
            }
            """);

        result.Should().StartWith("context.yaml written:");
    }

    [Fact]
    public async Task Write_AnIntegerLimit_IsJudgedAsANumber()
    {
        // The judged JSON is the TYPED document projected back to JSON, so an integer
        // rule sees an integer. Routing the emitted YAML through the shared YAML-to-JSON
        // bridge instead would stringify every scalar and fail this correct document —
        // that defect is cut as 2026-08-25-2c7c and deliberately not fixed here.
        var result = await WriteAsync("""
            {
              "meta": { "workdir": "." },
              "stack": { "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0" },
              "quality": { "limits": { "class-lines": 120 } }
            }
            """);

        result.Should().StartWith("context.yaml written:");
    }

    [Fact]
    public async Task Write_APartialResourcesBlock_IsRejected()
    {
        // stack.resources is all four quantities or none — the schema has always said so.
        var result = await WriteAsync("""
            {
              "meta": { "workdir": "." },
              "stack": {
                "lang": "C#", "image": "mcr.microsoft.com/dotnet/sdk:8.0",
                "resources": { "cpu_limit": "2" }
              }
            }
            """);

        result.Should().StartWith("Error:");
        result.Should().Contain("/stack/resources");
        NothingWasWritten();
    }

    [Fact]
    public async Task Write_AContextThatCannotBeMadeValid_FailsVisiblyWithoutSpinning()
    {
        const string hopeless = """{ "meta": { "workdir": "." } }""";
        var host = BuildHost();
        var document = JsonDocument.Parse(hopeless).RootElement;

        var refusals = new List<string>();
        for (var attempt = 0; attempt <= ContextWriteRejectionBudget.Limit; attempt++)
            refusals.Add(await host.WriteContextYaml("client", "default", document));

        refusals.Take(ContextWriteRejectionBudget.Limit).Should().AllSatisfy(refusal =>
            refusal.Should().Contain($"of {ContextWriteRejectionBudget.Limit} for context",
                "inside the budget the model is asked to fix and retry"));
        refusals[^1].Should().Contain("will not accept another attempt in this round");
        refusals[^1].Should().Contain("Stop calling it for this context");
        NothingWasWritten();
    }

    [Fact]
    public async Task Write_AnAcceptedContext_ClearsItsRefusalTally()
    {
        var host = BuildHost();
        var invalid = JsonDocument.Parse("""{ "meta": { "workdir": "." } }""").RootElement;
        var valid = JsonDocument.Parse("""
            { "meta": { "workdir": "." }, "stack": { "image": "node:20-bookworm" } }
            """).RootElement;

        await host.WriteContextYaml("client", "default", invalid);
        (await host.WriteContextYaml("client", "default", valid))
            .Should().StartWith("context.yaml written:");
        (await host.WriteContextYaml("client", "default", invalid))
            .Should().Contain($"refusal 1 of {ContextWriteRejectionBudget.Limit}",
                "a later legitimate rewrite starts from a full budget");
    }

    private async Task<string> WriteAsync(string json) =>
        await BuildHost().WriteContextYaml(
            repo: "client", context_name: "default", JsonDocument.Parse(json).RootElement);

    private WriteContextYamlToolHost BuildHost() =>
        new(new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["client"] = _sandboxMock.Object },
            defaultRepo: "client", _serializer, ContextGates.Build(), ContextGates.Writer());

    private void NothingWasWritten() =>
        _sandboxMock.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
}

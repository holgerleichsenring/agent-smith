using System.Diagnostics;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0421: the delivery path over a REAL git repository, LLM-free and offline.
/// <para>
/// Twenty-three live runs and twelve hours of wall clock went into finding seven defects
/// on this path — OutputContent never returned, /root untranslated, a BOM'd config, a
/// synthetic HOME that did not exist, criteria asked of the wrong repository, mechanical
/// evidence never reaching the account. Every one of them was deterministic and local,
/// and every one of them was hunted with the slowest tool available: a whole run against
/// a remote tracker with a real model, thirty-two minutes per bit of information.
/// </para>
/// <para>
/// This case is the answer to that. Real git, real diff, real sandbox, real accounting —
/// only the model is scripted. It runs in seconds, so the next defect on this path costs
/// seconds to find.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DeliveryAccountingTests : IAsyncLifetime
{
    private readonly string _repo = Path.Combine(
        Path.GetTempPath(), $"agentsmith-delivery-{Guid.NewGuid():N}");

    [Fact]
    public async Task TheAccountIsTakenAgainstTheRealDiff_AndACitationInItSatisfiesTheCriterion()
    {
        await GivenARepositoryWithACommittedChangeAsync();
        var scripted = new ScriptedChatClient().EnqueueText(
            """[{"criterion":"the greeting is localised","satisfied":true,"citation":"src/Program.cs"}]""");

        var account = await AccountAsync(scripted, ["the greeting is localised"]);

        account.Delivered.Should().BeTrue();
        account.Criteria.Single().Citation.Should().Be("src/Program.cs");
        scripted.LastMessages.Single().Text.Should().Contain("Hallo",
            "the account is taken against what the branch really changed, not a summary of it");
    }

    [Fact]
    public async Task ACitationTheDiffDoesNotContain_LeavesTheCriterionOutstanding()
    {
        await GivenARepositoryWithACommittedChangeAsync();
        var scripted = new ScriptedChatClient().EnqueueText(
            """[{"criterion":"the greeting is localised","satisfied":true,"citation":"src/Invented.cs"}]""");

        var account = await AccountAsync(scripted, ["the greeting is localised"]);

        account.Delivered.Should().BeFalse("a file the branch never touched cannot satisfy anything");
        RunDeliveryGate.Evaluate(RunAccounts.Empty.With("p1", [account]), ratifiedCriteria: 1)
            .Satisfied.Should().BeFalse();
    }

    /// <summary>
    /// The false negative that cost run 14: criteria belong to the PHASE, and a two-repo
    /// ticket whose criteria name one repository must not make the other outstanding.
    /// </summary>
    [Fact]
    public async Task CriteriaAboutOneRepository_AreNotAskedOfAnother()
    {
        await GivenARepositoryWithACommittedChangeAsync();
        var second = Path.Combine(Path.GetTempPath(), $"agentsmith-delivery-2-{Guid.NewGuid():N}");
        await GivenARepositoryWithACommittedChangeAsync(second, changed: false);
        try
        {
            var scripted = new ScriptedChatClient().EnqueueText(
                """[{"criterion":"the greeting is localised","satisfied":true,"citation":"src/Program.cs"}]""");

            var account = await AccountAsync(
                scripted, ["the greeting is localised"], extraRepo: second);

            account.Delivered.Should().BeTrue(
                "one account is taken for the phase over every repository's diff at once");
            scripted.InvocationCount.Should().Be(1, "and it costs one model call, not one per repo");
        }
        finally
        {
            Delete(second);
        }
    }

    /// <summary>
    /// p0422, found in run 16: two repositories' diffs were concatenated and then cut at
    /// the character budget, so the SECOND repository vanished entirely and every
    /// criterion about its files came back outstanding — "no inventory.md for the
    /// BackgroundWorker" over a file that was there. What changed is cheap to state; how
    /// it changed is not, so the file list is complete even when the body is not.
    /// </summary>
    [Fact]
    public async Task TheFileListIsCompleteEvenWhenTheDiffBodyIsTruncated()
    {
        await GivenARepositoryWithACommittedChangeAsync();
        await GivenALargeChangeAsync();
        var scripted = new ScriptedChatClient().EnqueueText(
            """[{"criterion":"the inventory exists","satisfied":true,"citation":"docs/inventory.md"}]""");

        await AccountAsync(scripted, ["the inventory exists"]);

        var prompt = scripted.LastMessages.Single().Text!;
        prompt.Should().Contain("EVERY FILE THIS BRANCH CHANGED");
        prompt.Should().Contain("docs/inventory.md",
            "a file past the body's budget is still named in the list");
    }

    private async Task<SpecAccount> AccountAsync(
        ScriptedChatClient scripted, IReadOnlyList<string> criteria, string? extraRepo = null)
    {
        var accounting = new PhaseAccounting(
            new DeliveryDiff(new AgentSmith.Application.Services.Sandbox.SandboxBaseBranch(NullLogger<AgentSmith.Application.Services.Sandbox.SandboxBaseBranch>.Instance), NullLogger<DeliveryDiff>.Instance),
            new SpecAccountant(
                ScriptedChatClientFactoryAdapter.Untraced(scripted),
                new SpecAccountCall(ScriptedChatClientFactoryAdapter.Untraced(scripted), new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(), NullLogger<SpecAccountCall>.Instance),
                NullLogger<SpecAccountant>.Instance),
            new AgentSmith.Application.Services.Handlers.SandboxTargets(),
            NullLogger<PhaseAccounting>.Instance);

        var sandboxes = new Dictionary<string, ISandbox>
        {
            ["primary"] = Sandbox(_repo),
        };
        if (extraRepo is not null) sandboxes["secondary"] = Sandbox(extraRepo);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "code", new AgentConfig(), SkillsPath: string.Empty, CodingPrinciplesPath: null));
        // The criteria come from the RATIFIED spec, never from the caller — the accounting
        // measures what was agreed before the work, which is the whole point.
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft(
            "p1", "localise the greeting", "goal: localise the greeting", [])
            { Done = criteria });
        var accounts = await accounting.TakeAsync(
            pipeline, sandboxes, ["primary: build 'dotnet build' exited 0"], CancellationToken.None);
        return accounts.Single();
    }

    private static ISandbox Sandbox(string path) =>
        new InProcessSandbox("harness", path, ownsWorkDir: false, NullLogger<InProcessSandbox>.Instance);

    /// <summary>A change large enough to push later files past the prompt's diff budget.</summary>
    private async Task GivenALargeChangeAsync()
    {
        var filler = Path.Combine(_repo, "src", "Filler.cs");
        await File.WriteAllTextAsync(filler, string.Concat(
            Enumerable.Range(0, 4000).Select(i => $"// padding line {i}\n")));
        Directory.CreateDirectory(Path.Combine(_repo, "docs"));
        await File.WriteAllTextAsync(Path.Combine(_repo, "docs", "inventory.md"), "# inventory\n");
        Git(_repo, "add", ".");
    }

    private Task GivenARepositoryWithACommittedChangeAsync() =>
        GivenARepositoryWithACommittedChangeAsync(_repo, changed: true);

    private static async Task GivenARepositoryWithACommittedChangeAsync(string path, bool changed)
    {
        Directory.CreateDirectory(Path.Combine(path, "src"));
        var file = Path.Combine(path, "src", "Program.cs");
        await File.WriteAllTextAsync(file, "Console.WriteLine(\"Hello\");\n");
        Git(path, "init", "-q", "-b", "develop");
        Git(path, "config", "user.email", "harness@example.test");
        Git(path, "config", "user.name", "harness");
        Git(path, "add", ".");
        Git(path, "commit", "-q", "-m", "base");
        if (!changed) return;
        await File.WriteAllTextAsync(file, "Console.WriteLine(\"Hallo\");\n");
    }

    private static void Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        process.WaitForExit();
    }

    private static void Delete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch (IOException) { }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        Delete(_repo);
        return Task.CompletedTask;
    }
}

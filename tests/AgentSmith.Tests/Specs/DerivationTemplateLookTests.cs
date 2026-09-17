using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-13-84c0: a declared template is one more name the derivation may look into — on
/// its own allowance, in a copy of the map, and owned by the look.
/// </summary>
public sealed class DerivationTemplateLookTests
{
    private const string Target = "Sample.Server";
    private const string Template = "template:server";

    [Fact]
    public void Look_DeclaredTemplate_IsListedApartFromTheTargets()
    {
        var look = Build(out _);

        look.Repositories.Should().Equal(Target);
        look.Templates.Keys.Should().Equal(Template);
    }

    [Fact]
    public void Look_NoTemplateDeclared_RepositoriesUnchanged()
    {
        var look = new DerivationLook(
            Targets(), new DerivationTestLooks.FixedReaderFactory(new InMemorySandboxFileReader()),
            new PackageEcosystemDetector(), NullLogger.Instance);

        look.Repositories.Should().Equal(Target);
        look.Templates.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Look_TemplateRead_DoesNotSpendTheTargetBudget()
    {
        var look = Build(out _);

        for (var i = 0; i < DerivationLookTerms.DerivationAllowance; i++)
            look.TryOpen(Template, out _, out _).Should().BeTrue();

        look.TryOpen(Target, out _, out var refusal).Should().BeTrue(
            "the target's twelve are untouched by a template that spent its own");
        refusal.Should().BeEmpty();
    }

    [Fact]
    public void Look_TemplateBudgetExhausted_RecordsARefusalTheDeriverCanFailOn()
    {
        var look = Build(out _);

        for (var i = 0; i < DerivationLookTerms.DerivationAllowance; i++) look.TryOpen(Template, out _, out _);
        look.TryOpen(Template, out _, out var refusal).Should().BeFalse();

        refusal.Should().Contain("No look left");
        look.TemplateRefusal.Should().Contain(Template,
            "a refusal sentence alone would let the cut claim a provenance it does not have");
    }

    [Fact]
    public void Look_UnknownRepository_SpendsNoLook()
    {
        var look = Build(out _);

        look.TryOpen("nope", out _, out var refusal).Should().BeFalse();

        refusal.Should().Contain("No repository named 'nope'");
        for (var i = 0; i < DerivationLookTerms.DerivationAllowance; i++)
            look.TryOpen(Target, out _, out _).Should().BeTrue(
                "resolution now happens before the charge, so a guess costs nothing");
    }

    [Fact]
    public async Task Look_Disposed_TearsDownTheTemplateScopeItOwns()
    {
        var look = Build(out var scope);

        await look.DisposeAsync();

        scope.Disposed.Should().BeTrue();
    }

    private static DerivationLook Build(out RecordingScope scope)
    {
        scope = new RecordingScope();
        return new DerivationLook(
            Targets(), new DerivationTestLooks.FixedReaderFactory(new InMemorySandboxFileReader()),
            new PackageEcosystemDetector(), NullLogger.Instance,
            new Dictionary<string, ISourceScopeSandbox> { [Template] = scope });
    }

    private static Dictionary<string, ISandbox> Targets() =>
        new(StringComparer.Ordinal) { [Target] = new DerivationTestLooks.CountingSandbox(0) };

    private sealed class RecordingScope : ISourceScopeSandbox
    {
        public bool Disposed { get; private set; }
        public string RepoName => "reference";
        public bool IsMaterialized => false;
        public string? ResolvedSha => null;
        public string JobId => "template-scope";

        public Task<string> MaterializeAsync(CancellationToken ct) => Task.FromResult("sha");

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? p, CancellationToken ct) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, string.Empty));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

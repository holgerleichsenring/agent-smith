using System.Text;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// 2026-09-20-2789: a refusal names what decided the failure. A branch that PASSED —
/// the losing half of a matched <c>oneOf</c>, the <c>if</c> of a pair that did not apply —
/// contributed nothing to the verdict and so contributes no clause; an unknown property is
/// refused in English rather than as a false subschema; and a report that hit the ten-clause
/// bound says so. Driven through the shipped validators, which is the path the operator sees.
/// </summary>
public sealed class SchemaValidatorTests
{
    private static string Refusal(string yaml)
    {
        var outcome = new SpecDraftValidator(new PhaseSpecSchemaProvider()).ValidateYaml(yaml);
        return outcome.Should().BeOfType<SpecDraftInvalid>().Subject.Error;
    }

    /// <summary>A draft exercising all three phase-spec oneOf sites, plus one real error.</summary>
    private static string DraftWith(int sites, bool arrayActions = false)
    {
        var yaml = new StringBuilder("phase: p9999\ngoal: \"g\"\ntests: \"not-an-array\"\nrequires:\n");
        for (var i = 0; i < sites; i++) yaml.Append($"  - \"p{i:0000}\"\n");
        yaml.Append("decisions:\n");
        for (var i = 0; i < sites; i++) yaml.Append($"  - key: \"d{i} - why\"\n");
        yaml.Append("steps:\n");
        for (var i = 0; i < sites; i++)
            yaml.Append(arrayActions && i % 2 == 1
                ? $"  - id: s{i}\n    action:\n      - \"do {i}\"\n"
                : $"  - id: s{i}\n    action: \"do {i}\"\n");
        return yaml.ToString();
    }

    private const string TheRealError = "phase-spec/tests: Value is \"string\" but should be \"array\"";

    [Fact]
    public void SchemaValidator_APassingOneOf_ContributesNoneOfItsLosingBranches() =>
        Refusal(DraftWith(sites: 3)).Should().NotContain("requires").And.NotContain("decisions");

    [Fact]
    public void SchemaValidator_AStringActionAndAnArrayAction_BothReportNothing() =>
        Refusal(DraftWith(sites: 4, arrayActions: true)).Should().NotContain("action");

    [Fact]
    public void SchemaValidator_AOneOfMatchingNeitherBranch_StillReports() =>
        Refusal("phase: p9999\ngoal: \"g\"\nrequires: 42\n").Should()
            .Contain("phase-spec/requires: Value is \"integer\" but should be \"array\"")
            .And.Contain("phase-spec/requires: Value is \"integer\" but should be \"string\"");

    // 2026-09-24-3907: naming only the dead end is unsatisfiable for a reader told to "fix
    // exactly what the error names" — three live design turns answered it by guessing another
    // key (scope/constraints, then scope/exclusions, then scope/repositories). The refusal now
    // names the way out.
    [Fact]
    public void SchemaValidator_AnUnknownKey_IsReportedByNameAndAsNotAllowed() =>
        Refusal("phase: p9999\ngoal: \"g\"\nscope:\n  in: \"x\"\n  invented_key: \"y\"\n").Should()
            .Be("phase-spec/scope/invented_key: this property is not allowed here — it allows in, out");

    [Fact]
    public void SchemaValidator_ASchemaValuedAdditionalProperties_StillReportsItsTypeError() =>
        Refusal("phase: p9999\ngoal: \"g\"\ndep-graph:\n  a: \"not-an-array\"\n").Should()
            .Be("phase-spec/dep-graph/a: Value is \"string\" but should be \"array\"");

    [Fact]
    public void SchemaValidator_ADraftWithOneRealError_ReportsExactlyThatOne() =>
        Refusal(DraftWith(sites: 8)).Should().Be(TheRealError);

    [Fact]
    public void SchemaValidator_AReportCutAtTheBound_SaysItWasCut()
    {
        var yaml = new StringBuilder("phase: p9999\ngoal: \"g\"\nscope:\n");
        for (var i = 0; i < 14; i++) yaml.Append($"  k{i}: \"v\"\n");

        Refusal(yaml.ToString()).Should().EndWith("(report cut at 10 problems, 4 more not shown)");
    }

    /// <summary>
    /// All three branching skill schemas carry an allOf of if/then pairs keyed on a status, and a
    /// failing `if` used to leak `Expected "complete"` into a refusal about something else.
    /// </summary>
    [Fact]
    public void SchemaValidator_AFailingIfBranch_ContributesNoError()
    {
        var loader = new JsonSchemaLoader();
        var refusals = new[]
        {
            new PlanOutputValidator(loader).Validate(
                """{"status":"needs_user_input","summary":"","steps":[],"open_questions":["q"]}"""),
            new BootstrapOutputValidator(loader).Validate(
                """{"status":"needs_user_input","files_written":[],"open_questions":["q"]}"""),
            new DiscoveryOutputValidator(loader).Validate(
                """{"status":"ambiguous","components":[],"ambiguity":{"message":""}}"""),
        };

        refusals.Should().OnlyContain(r => !r.IsValid);
        refusals.Select(r => r.ErrorMessage).Should().OnlyContain(m => !m!.Contains("complete"));
        refusals[0].ErrorMessage.Should().Contain("plan/summary: Value should be at least 1 characters");
    }

    [Fact]
    public void SchemaValidator_ASchemaWithNoBranching_IsUnchanged()
    {
        var diff = """{"changes":[{"file":"a"}],"tests_added":[],"tests_modified":[],"build_status":"ok","test_status":"ok"}""";

        var result = new DiffOutputValidator(new JsonSchemaLoader()).Validate(diff);

        result.ErrorMessage.Should()
            .Be("diff/changes/0: Required properties [\"operation\",\"summary\",\"patch\"] are not present");
    }

    /// <summary>
    /// 2026-09-24-3907: the operator's own draft, through the shipped validator. A design turn
    /// about three repositories with exclusions put exactly that content into `scope`, twice over,
    /// and got back two dead ends and no way forward — so the next attempt invented a third key.
    /// This is the message that turn produces now.
    /// </summary>
    [Fact]
    public void SchemaValidator_TheDraftThatKeptFailing_NowNamesTheWayOut()
    {
        var refusal = Refusal(
            """
            phase: 2026-09-24-a7c3
            goal: "Update all direct dependencies to their newest compatible minor or patch releases"
            scope:
              repositories:
                - Sample.Server
                - Sample.Client
              exclusions: "no major-version changes"
            """);

        refusal.Should().Contain("phase-spec/scope/repositories: this property is not allowed here");
        refusal.Should().Contain("phase-spec/scope/exclusions: this property is not allowed here");
        refusal.Should().Contain("it allows in, out",
            "a reader told to fix exactly what the error names must be given something to aim at");
    }
}

using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

/// <summary>
/// 2026-08-25-2c7c: the one bridge every draft validator crosses keeps a scalar's type,
/// so a schema rule about a number is enforced instead of silently unenforceable.
/// <para>
/// The bridge it replaced stringified every plain scalar, so <c>method-lines: 20</c> —
/// this repository's own context — was reported as failing <c>type: integer</c>, while a
/// rule that would have caught a genuinely wrong value never ran. The failure mode was a
/// false rejection of correct input, which is why nobody reported it, and why the
/// rejection tests below matter as much as the acceptance ones.
/// </para>
/// </summary>
public sealed class YamlAsJsonTests
{
    private static readonly ContextSchemaProvider ContextSchema = new();

    private static string ContextWith(string methodLines) => $"""
        meta:
          workdir: "."
        quality:
          limits:
            method-lines: {methodLines}
        """;

    [Fact]
    public void Bridge_AnInteger_IsJudgedAsAnInteger()
    {
        var node = YamlAsJson.Convert("limit: 20");

        node!["limit"]!.GetValueKind().Should().Be(JsonValueKind.Number);
        node["limit"]!.GetValue<long>().Should().Be(20);
    }

    [Fact]
    public void Bridge_ABoolean_IsJudgedAsABoolean()
    {
        var node = YamlAsJson.Convert("enabled: true\ndisabled: FALSE");

        node!["enabled"]!.GetValueKind().Should().Be(JsonValueKind.True);
        node["disabled"]!.GetValueKind().Should().Be(JsonValueKind.False);
    }

    [Fact]
    public void Bridge_ANull_IsJudgedAsNull()
    {
        var node = YamlAsJson.Convert("absent:\ntilde: ~");

        node!.AsObject().ContainsKey("absent").Should().BeTrue("the key was written");
        node["absent"].Should().BeNull();
        node["tilde"].Should().BeNull();
    }

    [Fact]
    public void Bridge_AQuotedNumber_StaysAString()
    {
        var node = YamlAsJson.Convert("""
            quoted: "20"
            single: '20'
            """);

        node!["quoted"]!.GetValueKind().Should().Be(JsonValueKind.String);
        node["single"]!.GetValueKind().Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void Validator_ADraftWithANumericLimit_IsAccepted() =>
        SchemaValidator.Validate(YamlAsJson.Convert(ContextWith("20")), ContextSchema.Schema, "context")
            .IsValid.Should().BeTrue("method-lines: 20 is the integer the rule asks for");

    [Fact]
    public void Validator_ADraftWithAWrongTypeWhereANumberIsRequired_IsRejected()
    {
        var result = SchemaValidator.Validate(
            YamlAsJson.Convert(ContextWith("\"twenty\"")), ContextSchema.Schema, "context");

        result.IsValid.Should().BeFalse("a string is not the integer the rule asks for");
        result.ErrorMessage.Should().Contain("method-lines");
    }

    [Fact]
    public void Validator_APhaseDraftWithANumberWhereAStringIsRequired_IsRejected()
    {
        var draft = new SpecDraftValidator(new PhaseSpecSchemaProvider()).ValidateYaml("""
            phase: p9999
            goal: 42
            steps:
              - id: impl
                action: "Do the thing"
            done:
              - "it works"
            """);

        draft.Should().BeOfType<SpecDraftInvalid>("a bare 42 is a number, and goal is a string")
            .Which.Error.Should().Contain("goal");
    }
}

using AgentSmith.Application.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-09-20-4b0af, moved 2026-11-04-96a0a: one line of prose is admitted; anything else names its rule.</summary>
public sealed class ConversationSubjectAdmissionTests
{
    private const string Name = "Das Widget, das das Hauptbuch liest";

    [Theory]
    [InlineData("A widget that reads the ledger.\nAnd pages the results.")]
    [InlineData("```\nA widget that reads the ledger\n```")]
    [InlineData("**A widget that reads the ledger**")]
    [InlineData("- A widget that reads the ledger")]
    [InlineData("\"A widget that reads the ledger\"")]
    [InlineData("1. A widget that reads the ledger")]
    public void ConversationSubjectAdmission_AParagraphOrAFencedAnswer_IsDiscarded(string answer)
    {
        var verdict = ConversationSubjectAdmission.Of(answer);

        verdict.Subject.Should().BeNull();
        verdict.Refusal.Should().NotBeNullOrWhiteSpace("the model is told which rule it broke");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t  ")]
    public void ConversationSubjectAdmission_AnEmptyAnswer_IsDiscarded(string? answer) =>
        ConversationSubjectAdmission.Of(answer).Subject.Should().BeNull();

    [Fact]
    public void ConversationSubjectAdmission_AnOverlongAnswer_IsDiscarded()
    {
        ConversationSubjectAdmission.Of(new string('a', 121)).Refusal.Should().Contain("120",
            "a subject longer than the column is refused, not truncated — it is never revised");
        ConversationSubjectAdmission.Of(new string('a', 120)).Subject.Should().Be(new string('a', 120));
    }

    [Fact]
    public void ConversationSubjectAdmission_OneLineOfProse_IsAdmittedAsWritten() =>
        ConversationSubjectAdmission.Of($"  {Name}  ").Should().Be(ConversationSubjectVerdict.Admitted(Name));
}

using AgentSmith.Application.Services.Specs.Review;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-11-13-b3a2: an attack reviewer's finding may carry the decision only the owner can make.</summary>
public sealed class SpecAttackAnswerTests
{
    [Fact]
    public void Finding_WithDecision_Parsed()
    {
        var answer = SpecAttackAnswerReader.Read("""
            {"verdict": "REFUSE", "cuts": [], "findings": [
              {"text": "the policy is only proposed", "repository": "r", "path": "p.md", "line": 3, "cites": "A1",
               "decision": "  Is the policy approved?  "},
              {"text": "plain", "repository": "r", "path": "p.md", "line": 3, "cites": "A1", "decision": " "}]}
            """)!;

        answer.Findings.Select(f => f.Decision).Should().Equal("Is the policy approved?", null);
    }
}

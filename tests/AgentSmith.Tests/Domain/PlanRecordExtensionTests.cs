using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Domain;

public sealed class PlanRecordExtensionTests
{
    [Fact]
    public void Plan_NewFieldsDefaulted_BackwardCompatible()
    {
        var plan = new Plan("summary", Array.Empty<PlanStep>(), "raw");

        plan.Scope.Should().Be(PlanScope.Empty);
        plan.OpenQuestions.Should().BeEmpty();
        plan.TestImpact.Should().BeNull();
        plan.ConsumerImpact.Should().BeNull();
        plan.Status.Should().Be(PlanStatus.Complete);
    }

    [Fact]
    public void Plan_AllNewFieldsSet_PreservedThroughInitializer()
    {
        var plan = new Plan("summary", Array.Empty<PlanStep>(), "raw")
        {
            Scope = new PlanScope(new[] { "src/Foo.cs" }, new[] { "Auth" }),
            OpenQuestions = new[]
            {
                new PlanOpenQuestion("q1", "Which framework?", new[] { "a", "b" })
            },
            TestImpact = "no behaviour change",
            ConsumerImpact = "internal only",
            Status = PlanStatus.NeedsUserInput
        };

        plan.Scope.Files.Should().ContainSingle().Which.Should().Be("src/Foo.cs");
        plan.Scope.Modules.Should().ContainSingle().Which.Should().Be("Auth");
        plan.OpenQuestions.Should().HaveCount(1);
        plan.TestImpact.Should().Be("no behaviour change");
        plan.ConsumerImpact.Should().Be("internal only");
        plan.Status.Should().Be(PlanStatus.NeedsUserInput);
    }
}

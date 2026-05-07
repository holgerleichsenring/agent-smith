using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class TokenBudgetBatcherTests
{
    [Fact]
    public void Split_EmptyInput_ReturnsEmpty()
    {
        var result = TokenBudgetBatcher.Split([], 8192);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_SingleObservationFits_OneBatch()
    {
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("HIGH", "src/a.cs", 1, "short title", "", 80)
        };

        var result = TokenBudgetBatcher.Split(observations, 8192);

        result.Should().HaveCount(1);
        result[0].Should().HaveCount(1);
    }

    [Fact]
    public void Split_MultipleSmallObservations_OneBatch()
    {
        var observations = Enumerable.Range(0, 5)
            .Select(i => ObservationFactory.Make("HIGH", $"src/a{i}.cs", i, $"short {i}", "", 80))
            .ToList();

        var result = TokenBudgetBatcher.Split(observations, 8192);

        result.Should().HaveCount(1);
        result[0].Should().HaveCount(5);
    }

    [Fact]
    public void Split_LargeObservations_MultipleBatches()
    {
        // 100 observations × ~700 chars JSON each = 70k chars. Budget at maxTokens=8192 is
        // 8192 × 4 × 0.85 = 27852 chars. So we need ≥3 batches.
        var bigDescription = new string('x', 480); // close to 500-char cap
        var observations = Enumerable.Range(0, 100)
            .Select(i => ObservationFactory.Make("HIGH", $"src/a{i}.cs", i, bigDescription, "", 80))
            .ToList();

        var result = TokenBudgetBatcher.Split(observations, 8192);

        result.Count.Should().BeGreaterThanOrEqualTo(3);
        // Every batch (except possibly the last) is at-or-under budget
        var budgetChars = (int)(8192 * 4 * 0.85);
        foreach (var batch in result)
        {
            var batchChars = batch.Sum(o => JsonSerializer.Serialize(o).Length);
            batchChars.Should().BeLessThanOrEqualTo(budgetChars + 1500,
                "single observation may push the last batch slightly over; acceptable");
        }
        result.SelectMany(b => b).Should().HaveCount(100);
    }

    [Fact]
    public void Split_DeterministicGivenCappedFields()
    {
        // With caps, identical inputs produce identical batch counts run-to-run.
        var observations = Enumerable.Range(0, 50)
            .Select(i => ObservationFactory.Make("HIGH", $"src/a{i}.cs", i, $"finding {i}", "", 80))
            .ToList();

        var first = TokenBudgetBatcher.Split(observations, 8192);
        var second = TokenBudgetBatcher.Split(observations, 8192);

        first.Count.Should().Be(second.Count);
        for (var i = 0; i < first.Count; i++)
            first[i].Count.Should().Be(second[i].Count);
    }
}

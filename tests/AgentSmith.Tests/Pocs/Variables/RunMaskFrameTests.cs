using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: a PoC project's values are masked inside its run's frame only — in the trace's form and in
/// the provider placeholder form — never outside it nor on a parallel flow, and a nested frame restores the enclosing one.</summary>
public sealed class RunMaskFrameTests
{
    private const string PocValue = "poc-secret-value-1";
    private readonly RunMaskFrame _frame = new();
    private readonly SecretMasker _masker;

    public RunMaskFrameTests() =>
        _masker = new SecretMasker(new LoadedSecretValues(new AgentSmithConfig { Secrets = new Dictionary<string, string> { ["x"] = "installation-token-9" } }), _frame);

    [Fact]
    public void Apply_InsideAFrame_MasksThePocValue_InBothForms()
    {
        using var _ = _frame.Begin([PocValue]);

        _masker.Apply($"key={PocValue} and installation-token-9").Should().Be("key=*** and ***");
        _masker.Apply($"key={PocValue}", SecretMasker.ProviderPlaceholder).Should().Be($"key={SecretMasker.ProviderPlaceholder}");
    }

    [Fact]
    public async Task Apply_OutsideTheFrame_AndOnAParallelFlow_LeavesThePocValue()
    {
        var inside = new TaskCompletionSource();
        var other = Task.Run(async () =>
        {
            await inside.Task;
            return _masker.Apply(PocValue);
        });
        using (_frame.Begin([PocValue]))
        {
            inside.SetResult();
            (await other).Should().Be(PocValue, "another project's flow never sees this run's frame");
        }

        _masker.Apply(PocValue).Should().Be(PocValue);
    }

    [Fact]
    public void Begin_Nested_RestoresTheEnclosingFrame()
    {
        using (_frame.Begin(["outer-value-1"]))
        {
            using (_frame.Begin(["inner-value-1"])) _masker.Apply("outer-value-1 inner-value-1").Should().Be("outer-value-1 ***");
            _masker.Apply("outer-value-1 inner-value-1").Should().Be("*** inner-value-1");
        }

        _frame.Current.Should().BeEmpty();
    }

    [Fact]
    public void Begin_AShortValue_IsNotMasked_AndLongestGoesFirst()
    {
        using var _ = _frame.Begin(["short", "installation-token-9-and-more"]);

        _frame.Current.Should().Equal("installation-token-9-and-more");
        _masker.Apply("installation-token-9-and-more").Should().Be("***", "the longer frame value is replaced before the shorter installation one");
    }
}

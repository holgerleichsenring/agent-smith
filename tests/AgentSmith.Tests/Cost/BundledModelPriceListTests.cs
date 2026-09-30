using System.Text.RegularExpressions;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Pricing;
using FluentAssertions;

namespace AgentSmith.Tests.Cost;

/// <summary>
/// 2026-09-30-62baa: the bundled price list is the base every price lookup starts from.
/// Its facts are asserted as invariants over the committed snapshot, so a refresh by
/// tools/update-model-prices.py does not have to touch this file.
/// </summary>
public sealed class BundledModelPriceListTests
{
    private static readonly BundledModelPriceList Prices = new();

    [Fact]
    public void Snapshot_Parses_WithSourceDateAndModels()
    {
        Prices.Source.Should().Contain("litellm");
        Prices.FetchedAt.Should().BeAfter(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Prices.Models.Should().HaveCountGreaterThan(1000)
            .And.NotContain(m => m.Id == "sample_spec");
    }

    [Fact]
    public void ExampleConfig_EveryModelId_IsFound()
    {
        var example = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Configuration", "TestData", "agentsmith.example.yml"));
        var ids = Regex.Matches(example, @"^\s*model:\s*(\S+)\s*$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).Distinct().ToList();

        ids.Should().NotBeEmpty();
        ids.Should().OnlyContain(id => Prices.Find(id) != null);
    }

    [Fact]
    public void Supplement_PricesIdsTheListLacks()
    {
        var sonnet4 = Prices.Find("claude-sonnet-4-20250514")!;
        sonnet4.InputPerMillion.Should().Be(3.0m);
        sonnet4.OutputPerMillion.Should().Be(15.0m);
        sonnet4.CacheReadPerMillion.Should().Be(0.30m);
        Prices.Find("llama-3.3-70b-versatile")!.InputPerMillion.Should().Be(0m);
    }

    [Fact]
    public void Find_BareName_ResolvesToItsProviderPrefixedEntry()
    {
        var bare = Prices.PricingByName.Keys.First(k => !k.Contains('/') && !IsListedId(k));

        Prices.Find(bare)!.Id.Should().EndWithEquivalentOf("/" + bare);
    }

    [Fact]
    public void Find_BareName_IsIndexedOnlyWhenEveryPrefixedEntryAgrees()
    {
        var groups = Prices.Models.Where(m => m.Id.Contains('/'))
            .GroupBy(m => m.Id[(m.Id.LastIndexOf('/') + 1)..], StringComparer.OrdinalIgnoreCase)
            .Where(g => !IsListedId(g.Key))
            .ToList();
        var ambiguous = groups.Where(g => g.Select(Price).Distinct().Count() > 1).ToList();

        ambiguous.Should().NotBeEmpty("the list prices some model differently per provider");
        ambiguous.Should().OnlyContain(g => Prices.Find(g.Key) == null);
        groups.Except(ambiguous).Should().OnlyContain(g => Prices.Find(g.Key) != null);
    }

    [Fact]
    public void Find_ATypoStartingWithARealId_IsNotFound_ButTheRuntimePricesItByPrefix()
    {
        Prices.Find("gpt-5.6-nonesuch").Should().BeNull();
        new ModelPricingResolver(Prices).Resolve("gpt-5.6-nonesuch")!.InputPerMillion.Should().Be(4.0m);
    }

    [Fact]
    public void Resolve_ListedModel_UsesTheListPrice()
    {
        var sonnet5 = new ModelPricingResolver(Prices).Resolve("claude-sonnet-5")!;

        sonnet5.InputPerMillion.Should().Be(2.0m);
        sonnet5.OutputPerMillion.Should().Be(10.0m);
        sonnet5.CacheReadPerMillion.Should().Be(0.20m);
    }

    [Fact]
    public void ToPricing_MissingCacheReadRate_PricesCacheReadsAtZero()
    {
        Prices.Find("llama-3.3-70b-versatile")!.ToPricing().CacheReadPerMillion.Should().Be(0m);
    }

    private static bool IsListedId(string name) =>
        Prices.Models.Any(m => !m.Id.Contains('/') && m.Id.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static (decimal, decimal, decimal?) Price(Contracts.Models.Configuration.BundledModelPrice m) =>
        (m.InputPerMillion, m.OutputPerMillion, m.CacheReadPerMillion);
}

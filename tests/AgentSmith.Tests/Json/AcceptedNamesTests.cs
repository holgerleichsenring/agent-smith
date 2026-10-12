using System.Text.Json;
using AgentSmith.Contracts.Json;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Json;

/// <summary>
/// 2026-10-12-967aa: an alias is read beside the declared name and never written — the expand
/// half of a rename across replicas that share one store.
/// </summary>
public sealed class AcceptedNamesTests
{
    public sealed record Positional([property: AcceptedName("SpecId")] string PhaseId, int Ordinal);

    public sealed class Mutable
    {
        [AcceptedName("Specs")]
        public List<string>? Phases { get; set; }
    }

    public enum Kind
    {
        Answer = 0,
        [AcceptedName("SpecProposal")] PhaseProposal = 2,
    }

    private static readonly JsonSerializerOptions Camel =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, TypeInfoResolver = AcceptedNames.Resolver };

    [Fact]
    public void Read_AliasOnAPositionalRecord_BindsTheDeclaredMember()
    {
        JsonSerializer.Deserialize<Positional>("""{"specId":"s1","ordinal":2}""", Camel)
            .Should().Be(new Positional("s1", 2));
    }

    [Fact]
    public void Read_DeclaredName_StillReads()
    {
        JsonSerializer.Deserialize<Positional>("""{"phaseId":"p1","ordinal":1}""", Camel)
            .Should().Be(new Positional("p1", 1));
    }

    [Fact]
    public void Read_AliasOnASettableProperty_UnderDefaultCasing_Binds()
    {
        JsonSerializer.Deserialize<Mutable>("""{"Specs":["a"]}""", AcceptedNames.DefaultOptions)!.Phases
            .Should().Equal("a");
    }

    [Fact]
    public void Write_NeverEmitsTheAlias()
    {
        JsonSerializer.Serialize(new Positional("p1", 1), Camel)
            .Should().Be("""{"phaseId":"p1","ordinal":1}""");
    }

    [Fact]
    public void TryParse_AcceptedEnumName_ResolvesTheMember()
    {
        AcceptedNames.TryParse<Kind>("specProposal", ignoreCase: true, out var kind).Should().BeTrue();
        kind.Should().Be(Kind.PhaseProposal);
    }

    [Fact]
    public void TryParse_AnUnknownName_IsRefused()
    {
        AcceptedNames.TryParse<Kind>("EpicProposal", ignoreCase: true, out _).Should().BeFalse();
    }
}

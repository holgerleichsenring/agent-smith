using AgentSmith.Application.Services.Sandbox.Registry;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox.Registry;

/// <summary>2026-10-12-50bdb: the closest published tags, by shared runs — no ecosystem's shape.</summary>
public sealed class NearestTagRankerTests
{
    // An excerpt of sbtscala/scala-sbt's tag list as Docker Hub served it on 2026-10-12.
    private static readonly string[] Observed =
    [
        "11.0.14.1-oraclelinux8_1.6.2_2.12.15",
        "eclipse-temurin-21.0.5_11_1.10.7_2.13.15",
        "eclipse-temurin-25.0.1_8_1.11.7_2.12.20",
        "eclipse-temurin-25.0.1_8_1.11.7_2.13.17",
        "eclipse-temurin-25.0.1_8_1.11.7_2.13.18",
        "eclipse-temurin-25.0.1_8_1.11.7_3.7.4",
        "eclipse-temurin-25.0.1_8_1.12.0_2.13.18",
        "eclipse-temurin-25.0.4_7_1.13.0_2.13.18",
        "graalvm-community-25.0.1_1.11.7_3.7.4",
    ];

    [Fact]
    public void Nearest_ObservedSbtTag_PutsTheSameVersionPairFirst()
    {
        var nearest = new NearestTagRanker().Nearest("eclipse-temurin-25_1.11.7_2.13.18", Observed);

        nearest[0].Should().Be("eclipse-temurin-25.0.1_8_1.11.7_2.13.18");
        nearest.Should().HaveCountLessThanOrEqualTo(NearestTagRanker.MaxCandidates);
    }

    [Fact]
    public void Nearest_NothingShared_IsEmpty()
    {
        new NearestTagRanker().Nearest("zzz", ["alpha", "beta"]).Should().BeEmpty();
    }

    [Fact]
    public void Nearest_ManyTags_IsBounded()
    {
        var tags = Enumerable.Range(0, 50).Select(i => $"20.{i}-bookworm");

        new NearestTagRanker().Nearest("20-bookworm-x", tags).Should().HaveCount(NearestTagRanker.MaxCandidates);
    }
}

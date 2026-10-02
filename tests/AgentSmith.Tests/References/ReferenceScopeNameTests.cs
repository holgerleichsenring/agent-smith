using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-01-283dc: an uploaded set's name becomes one plain, unique address segment.</summary>
public sealed class ReferenceScopeNameTests
{
    [Fact]
    public void ReferenceScopeName_NameWithSpacesAndSlashes_IsSanitised()
    {
        ReferenceScopeName.Sanitised("My Site / Landing_v2!").Should().Be("my-site-landing-v2");
        ReferenceScopeName.Sanitised("../../etc").Should().Be("etc");
    }

    [Fact]
    public void ReferenceScopeName_NothingSurvives_IsSite()
    {
        ReferenceScopeName.Sanitised("ÄÖÜ ///").Should().Be("site");
        ReferenceScopeName.Sanitised(null).Should().Be("site");
    }

    [Fact]
    public void ReferenceScopeName_ALongName_IsCutToFortyCharacters()
    {
        ReferenceScopeName.Sanitised(new string('a', 39) + "-" + new string('b', 10)).Should().Be(new string('a', 39));
    }

    [Fact]
    public void ReferenceScopeName_For_NumbersARepeat()
    {
        ReferenceScopeName.For(["Site", "site", "other", "SITE"]).Should()
            .Equal("reference:site", "reference:site-2", "reference:other", "reference:site-3");
    }
}

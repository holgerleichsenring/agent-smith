using AgentSmith.Application.Services.Archives;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.References;
using FluentAssertions;

namespace AgentSmith.Tests.Archives;

/// <summary>2026-11-23-933db: the shared path rule takes its bound from the caller — the reference upload its column
/// width, the PoC rules pocs.archive.max_path_chars — and refuses one character over, naming it.</summary>
public sealed class ReferencePathRuleBoundTests
{
    private readonly ReferencePathRule _rule = new();

    [Fact]
    public void RefusalOf_TheReferenceBound_RefusesOneCharacterOverNamingIt()
    {
        var max = ReferenceUploadLimits.MaxPathChars;

        Refusal(new string('a', max), max).Should().BeNull();
        Refusal(new string('a', max + 1), max).Should().Be($"'{new string('a', max + 1)}' is longer than the 240-character path limit.");
    }

    [Fact]
    public void RefusalOf_APocBoundOf100_RefusesOneCharacterOverNamingTheKey()
    {
        Refusal(new string('a', 100), 100, PocArchiveLimits.MaxPathCharsKey).Should().BeNull();

        Refusal(new string('a', 101), 100, PocArchiveLimits.MaxPathCharsKey).Should()
            .Contain("is 101 characters long").And.Contain("over pocs.archive.max_path_chars 100");
    }

    private string? Refusal(string path, int max, string? key = null) =>
        _rule.RefusalOf(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase), max, key);
}

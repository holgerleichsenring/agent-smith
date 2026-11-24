using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>2026-11-23-933de: the one digest-reference type — parses tag and digest, refuses a tag alone where a digest
/// is required.</summary>
public sealed class ImageReferenceTests
{
    private const string Digest = "sha256:4444444444444444444444444444444444444444444444444444444444444444";

    [Fact]
    public void ImageReference_Parse_TagOnlyWhereDigestRequired_Refused()
    {
        var parse = () => ImageReference.Parse("pocregistry.azurecr.io/pocs/shop:abc123", digestRequired: true);

        parse.Should().Throw<FormatException>().WithMessage("*names no sha256 digest*");
        ImageReference.Parse("pocregistry.azurecr.io/pocs/shop:abc123").Tag.Should().Be("abc123");
    }

    [Fact]
    public void ImageReference_Parse_TagAndDigest_RoundTrip()
    {
        var text = $"pocregistry.azurecr.io/pocs/shop:abc123@{Digest}";

        var reference = ImageReference.Parse(text, digestRequired: true);

        (reference.Registry, reference.Repository, reference.Tag, reference.Digest)
            .Should().Be(("pocregistry.azurecr.io", "pocs/shop", "abc123", Digest));
        reference.ToString().Should().Be(text);
    }

    [Theory]
    [InlineData("shop")]
    [InlineData("pocregistry.azurecr.io/pocs/shop@sha256:short")]
    [InlineData("pocregistry.azurecr.io/Pocs/shop:abc")]
    public void ImageReference_Parse_NotAReference_Refused(string text) =>
        FluentActions.Invoking(() => ImageReference.Parse(text)).Should().Throw<FormatException>();

    [Fact]
    public void ImageReference_WithDigest_RefusesANonDigest() =>
        FluentActions.Invoking(() => new ImageReference("r.io", "pocs/shop", "t").WithDigest("latest"))
            .Should().Throw<FormatException>();
}

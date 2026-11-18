using AgentSmith.Application.Services.Deployment;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863c: one scalar of a manifest is read and replaced; every other byte stays.</summary>
public sealed class ManifestVersionKeyTests
{
    private const string Kustomization = """
        # test overlay
        apiVersion: kustomize.config.k8s.io/v1beta1
        kind: Kustomization
        resources:
          - ../../base   # the shared base
        images:
          - name: registry.example.com/app
            newTag: v1.0.0  # bumped by agent-smith
        """;

    private readonly ManifestVersionKey _keys = new();

    [Fact]
    public void Read_NestedSequenceKey_AnswersTheValue() =>
        _keys.Read(Kustomization, "images.0.newTag").Value.Should().Be("v1.0.0");

    [Fact]
    public void Replace_PlainScalar_ChangesOnlyTheValue()
    {
        var (changed, error) = _keys.Replace(Kustomization, "images.0.newTag", "v1.1.0");

        error.Should().BeNull();
        changed.Should().Be(Kustomization.Replace("newTag: v1.0.0  #", "newTag: v1.1.0  #"));
    }

    [Theory]
    [InlineData("tag: \"1.0\"\n", "tag: \"2.0\"\n")]
    [InlineData("tag: '1.0'\n", "tag: '2.0'\n")]
    [InlineData("tag: abc\n", "tag: \"2.0\"\n")]
    public void Replace_KeepsQuotingAndQuotesANumberLikeValue(string manifest, string expected) =>
        _keys.Replace(manifest, "tag", "2.0").Manifest.Should().Be(expected);

    [Fact]
    public void Replace_SecondDocument_IsFound() =>
        _keys.Replace("kind: A\n---\nimage:\n  tag: old\n", "image.tag", "new").Manifest
            .Should().Be("kind: A\n---\nimage:\n  tag: new\n");

    [Fact]
    public void Read_MissingKey_SaysSo() =>
        _keys.Read(Kustomization, "image.tag").Error.Should().Contain("has no 'image.tag'");

    [Fact]
    public void Read_NonScalar_IsRefused() =>
        _keys.Read(Kustomization, "images.0").Error.Should().Contain("not a single value");
}

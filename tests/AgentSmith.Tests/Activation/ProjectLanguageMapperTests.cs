using AgentSmith.Application.Services.Activation;
using FluentAssertions;

namespace AgentSmith.Tests.Activation;

public sealed class ProjectLanguageMapperTests
{
    [Theory]
    [InlineData("C#")]
    [InlineData("c#")]
    [InlineData("CSHARP")]
    [InlineData("csharp")]
    [InlineData(".NET")]
    [InlineData("dotnet")]
    public void Map_CsharpSynonyms_ReturnsCsharp(string input) =>
        ProjectLanguageMapper.Map(input).Should().Be("csharp");

    [Theory]
    [InlineData("TypeScript")]
    [InlineData("JavaScript")]
    [InlineData("Node.js")]
    [InlineData("nodejs")]
    [InlineData("node")]
    [InlineData("ts")]
    [InlineData("js")]
    public void Map_NodeSynonyms_ReturnsNode(string input) =>
        ProjectLanguageMapper.Map(input).Should().Be("node");

    [Theory]
    [InlineData("Python")]
    [InlineData("python")]
    [InlineData("py")]
    public void Map_PythonSynonyms_ReturnsPython(string input) =>
        ProjectLanguageMapper.Map(input).Should().Be("python");

    [Theory]
    [InlineData("Go")]
    [InlineData("Java")]
    [InlineData("Rust")]
    [InlineData("Kotlin")]
    [InlineData("Ruby")]
    [InlineData("UnknownLang42")]
    public void Map_UnrecognisedLanguage_ReturnsGeneric(string input) =>
        ProjectLanguageMapper.Map(input).Should().Be("generic");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Map_NullOrWhitespace_ReturnsGeneric(string? input) =>
        ProjectLanguageMapper.Map(input).Should().Be("generic");

    [Fact]
    public void Map_SurroundingWhitespace_TrimmedBeforeLookup() =>
        ProjectLanguageMapper.Map("  csharp  ").Should().Be("csharp");
}

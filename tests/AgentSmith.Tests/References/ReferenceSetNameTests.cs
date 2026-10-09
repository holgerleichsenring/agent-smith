using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-09-86e1: an upload is named by its files — never "site".</summary>
public sealed class ReferenceSetNameTests
{
    [Fact]
    public void ReferenceSetName_SharedFolder_IsTheFolder()
    {
        ReferenceSetName.Of(["site/index.html", "site/a.css"]).Should().Be("site");
    }

    [Fact]
    public void ReferenceSetName_SingleFile_IsTheFileName()
    {
        ReferenceSetName.Of(["brief.md"]).Should().Be("brief.md");
    }

    [Fact]
    public void ReferenceSetName_NoSharedFolder_FirstPlusCount()
    {
        ReferenceSetName.Of(["two.css", "one.css", "notes/a.md"]).Should().Be("a.md + 2 more");
    }

    [Fact]
    public void ReferenceScopeName_SingleFileSet_AddressIsNotSite()
    {
        ReferenceScopeName.For([ReferenceSetName.Of(["Brief.md"])]).Should().Equal("reference:brief-md");
    }
}

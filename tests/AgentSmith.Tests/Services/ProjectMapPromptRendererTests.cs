using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMapPromptRendererTests
{
    [Fact]
    public void RenderExistingTests_NullMap_ReturnsEmpty()
    {
        ProjectMapPromptRenderer.RenderExistingTests(null).Should().BeEmpty();
    }

    [Fact]
    public void RenderExistingTests_NoTestProjects_StatesAbsenceExplicitly()
    {
        var map = new ProjectMap("C#", [], [], [], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));

        ProjectMapPromptRenderer.RenderExistingTests(map)
            .Should().Be("No test projects discovered in this repository.");
    }

    [Fact]
    public void RenderExistingTests_AuthPortShape_IncludesPathFrameworkFileCountAndSample()
    {
        var map = new ProjectMap(
            "C#", [".NET 8"], [], [
                new TestProject(
                    "RHS.AuthPort.Tests.Integration",
                    "xUnit",
                    117,
                    "RHS.AuthPort.Tests.Integration/AuthTests.cs")
            ], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));

        var rendered = ProjectMapPromptRenderer.RenderExistingTests(map);

        rendered.Should().Contain("RHS.AuthPort.Tests.Integration");
        rendered.Should().Contain("xUnit");
        rendered.Should().Contain("117 test file(s)");
        rendered.Should().Contain("AuthTests.cs");
    }
}

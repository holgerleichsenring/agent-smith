using AgentSmith.Application.Services.Handlers;
using FluentAssertions;

namespace AgentSmith.Tests.Verify;

public sealed class ChangedPathGlobTests
{
    [Theory]
    [InlineData("src/dashboard/**", "src/dashboard/package.json", true)]
    [InlineData("src/dashboard/**", "src/dashboard/src/app/page.tsx", true)]
    [InlineData("src/**/Events/*.cs", "src/Events/RunStarted.cs", true)]
    [InlineData("src/**/Events/*.cs", "src/backend/Contracts/Events/RunStarted.cs", true)]
    [InlineData("**", "anything/at/all.txt", true)]
    [InlineData("src/dashboard/**", "src/dashboard", true)]
    [InlineData("src/dashboard/**", "src/backend/Program.cs", false)]
    [InlineData("src/dashboard/**", "src/dashboardx/a.ts", false)]
    public void ChangedPathGlob_DoubleStar_MatchesAnyDepth_IncludingZero(string glob, string path, bool expected) =>
        ChangedPathGlob.IsMatch(glob, path).Should().Be(expected);

    [Theory]
    [InlineData("web/*.ts", "web/app.ts", true)]
    [InlineData("web/*.ts", "web/lib/app.ts", false)]
    [InlineData("pnpm-*.yaml", "pnpm-lock.yaml", true)]
    [InlineData("*", "README.md", true)]
    [InlineData("*", "docs/README.md", false)]
    [InlineData("Web/*.ts", "web/app.ts", false)]
    public void ChangedPathGlob_SingleStar_StaysInOneSegment(string glob, string path, bool expected) =>
        ChangedPathGlob.IsMatch(glob, path).Should().Be(expected);

    [Theory]
    [InlineData("./web/**", "web/app.ts")]
    [InlineData("/web/**", "web/app.ts")]
    [InlineData("web/**", "./web/app.ts")]
    public void ChangedPathGlob_LeadingDotSlash_IsIgnored(string glob, string path) =>
        ChangedPathGlob.IsMatch(glob, path).Should().BeTrue("both sides are relative to the repository root");
}

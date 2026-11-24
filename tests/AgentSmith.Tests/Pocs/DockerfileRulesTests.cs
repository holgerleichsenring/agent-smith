using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using static AgentSmith.Tests.Pocs.PocZips;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: what a PoC's Dockerfile may not ask of the builder, refused naming its line also across a
/// line continuation, and the facts it gives — the one TCP port of the final stage, ENTRYPOINT/CMD and the base
/// image.</summary>
public sealed class DockerfileRulesTests
{
    private readonly DockerfileRules _rules = new(new DockerfileStages(), new DockerfileBlocklist(), new DockerfileUser(),
        new DockerfileExpose());

    [Fact]
    public void Check_OneExpose8000_PassesWithItsFacts()
    {
        var check = Check("FROM node:22 AS build\nRUN npm ci\nFROM nginx:1.27\nEXPOSE 8000\nENTRYPOINT [\"nginx\"]\n"
            + "CMD [\"-g\", \"daemon off;\"]\n");

        check.Refusal.Should().BeNull();
        check.Dockerfile.Should().Be(new PocDockerfile(8000, "ENTRYPOINT [\"nginx\"] CMD [\"-g\", \"daemon off;\"]", "nginx:1.27"));
    }

    [Theory]
    [InlineData("# syntax=docker/dockerfile:1\nFROM alpine\nEXPOSE 80\n", "Dockerfile line 1: '# syntax=docker/dockerfile:1' is refused")]
    [InlineData("FROM alpine\nRUN --security=insecure ls\nEXPOSE 80\n", "Dockerfile line 2: RUN --security=insecure is refused")]
    [InlineData("FROM alpine\nRUN --network=host ls\nEXPOSE 80\n", "Dockerfile line 2: RUN --network=host is refused")]
    [InlineData("FROM alpine\nRUN --mount=type=ssh ls\nEXPOSE 80\n", "Dockerfile line 2: RUN --mount=type=ssh is refused")]
    [InlineData("FROM alpine\nRUN --mount=id=t,type=secret cat\nEXPOSE 80\n", "Dockerfile line 2: RUN --mount=id=t,type=secret is refused")]
    [InlineData("FROM alpine\nRUN \\\n  --network=host \\\n  ls\nEXPOSE 80\n", "Dockerfile line 2: RUN --network=host is refused")]
    [InlineData("FROM alpine\nRUN echo a && \\\n# a comment\n  echo b\nRUN \\\n --mount=type=secret,id=x ls\nEXPOSE 80\n", "Dockerfile line 5: RUN --mount=type=secret,id=x is refused")]
    [InlineData("# escape=`\nFROM alpine\nRUN `\n  --network=host ls\nEXPOSE 80\n", "Dockerfile line 3: RUN --network=host is refused")]
    [InlineData("FROM alpine\nADD http://x.example/a.tgz /a\nEXPOSE 80\n", "Dockerfile line 2: ADD from 'http://x.example/a.tgz' is refused")]
    [InlineData("FROM alpine\nADD https://x.example/a.tgz /a\nEXPOSE 80\n", "Dockerfile line 2: ADD from 'https://x.example/a.tgz' is refused")]
    [InlineData("FROM alpine\nADD git@github.com:o/r.git /r\nEXPOSE 80\n", "Dockerfile line 2: ADD from 'git@github.com:o/r.git' is refused")]
    [InlineData("FROM alpine\nADD git://x.example/r.git /r\nEXPOSE 80\n", "Dockerfile line 2: ADD from 'git://x.example/r.git' is refused")]
    [InlineData("FROM alpine\nADD --chmod=644 \\\n  ssh://x.example/r /r\nEXPOSE 80\n", "Dockerfile line 2: ADD from 'ssh://x.example/r' is refused")]
    [InlineData("FROM alpine\nADD [\"https://x.example/a\", \"/a\"]\nEXPOSE 80\n", "Dockerfile line 2: ADD from 'https://x.example/a' is refused")]
    [InlineData("FROM alpine\nEXPOSE 80\nUSER app\n", "Dockerfile line 3: USER app is refused")]
    public void Check_ABlockedInstruction_IsRefusedNamingTheLine(string dockerfile, string refusal)
    {
        var check = Check(dockerfile);

        check.Refusal!.Check.Should().Be(PocArchiveChecks.Dockerfile);
        check.Refusal.Reason.Should().StartWith(refusal);
    }

    [Theory]
    [InlineData("FROM alpine\nEXPOSE 80\nEXPOSE 8080\n", "the Dockerfile's final stage exposes 2 TCP ports (80 on line 2, 8080 on line 3)")]
    [InlineData("FROM alpine\nEXPOSE 53/udp\n", "the Dockerfile's final stage exposes 0 TCP ports")]
    [InlineData("FROM alpine\nEXPOSE $PORT\n", "Dockerfile line 2: EXPOSE $PORT is not one port number")]
    [InlineData("FROM alpine AS build\nEXPOSE 9000\nFROM alpine\n", "the Dockerfile's final stage exposes 0 TCP ports")]
    public void Check_NotExactlyOneTcpExposeInTheFinalStage_IsRefused(string dockerfile, string refusal)
    {
        var check = Check(dockerfile);

        check.Refusal!.Check.Should().Be(PocArchiveChecks.Expose);
        check.Refusal.Reason.Should().StartWith(refusal).And.EndWith("because that is the port the PoC's address is routed to.");
    }

    [Theory]
    [InlineData("FROM alpine AS build\nEXPOSE 9000\nUSER app\nFROM alpine\nEXPOSE 8000/tcp 8000/udp\nUSER 10001\n", 8000)]
    [InlineData("FROM alpine\nUSER 10001:10001\nexpose 3000\n", 3000)]
    [InlineData("# syntax is not a directive here\nFROM alpine\nEXPOSE 80\n", 80)]
    public void Check_ABuilderStageAndANumericUser_Pass(string dockerfile, int port)
    {
        Check(dockerfile).Dockerfile!.Port.Should().Be(port);
    }

    [Fact]
    public void Check_NoFromOrOverMaxDockerfileKb_IsRefused()
    {
        Check("EXPOSE 80\n").Refusal!.Reason.Should().StartWith("the Dockerfile has no FROM");
        _rules.Check(Text("FROM alpine\nEXPOSE 80\n" + new string('#', 2048)), new PocArchiveLimits { MaxDockerfileKb = 1 })
            .Refusal!.Reason.Should().StartWith("the Dockerfile is 3 KB, over pocs.archive.max_dockerfile_kb 1");
    }

    [Fact]
    public void Check_NeitherEntrypointNorCmd_HasNoEntrypoint()
    {
        Check("FROM alpine\nEXPOSE 80\n").Dockerfile.Should().Be(new PocDockerfile(80, null, "alpine"));
    }

    [Fact]
    public void Parse_AFromWithPlatform_NamesTheImage()
    {
        new DockerfileStages().Parse("FROM --platform=linux/amd64 python:3.12 AS app\n").BaseImage.Should().Be("python:3.12");
    }

    private PocDockerfileCheck Check(string dockerfile) => _rules.Check(Text(dockerfile), new PocArchiveLimits());
}

using AgentSmith.Application.Services.Pocs.Scan;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>2026-11-23-933dc: only exact pins reach the scanned copy; every other line is named and never followed.</summary>
public sealed class PocRequirementsFilterTests
{
    [Fact]
    public void Filter_KeepsExactPins_LeavesOutUnpinnedLinesAndOptions()
    {
        var (pinned, leftOut) = new PocRequirementsFilter().Filter(
            "# app\nflask\nrequests>=2.0\ndjango==3.2.0  # pinned\n-r other.txt\n--index-url http://user:pw@x.example/simple\n"
            + "numpy[extra]==1.26.4 ; python_version >= \"3.9\" \\\n    --hash=sha256:abc\n");

        pinned.Should().Be("django==3.2.0\nnumpy[extra]==1.26.4 ; python_version >= \"3.9\"      --hash=sha256:abc\n");
        leftOut.Should().Equal("flask", "requests>=2.0", "-r other.txt", "--index-url http://<credentials>@x.example/simple");
    }

    [Fact]
    public void Filter_AFileOfCommentsOnly_PinsNothing()
    {
        var (pinned, leftOut) = new PocRequirementsFilter().Filter("# nothing\n\n");

        pinned.Should().BeEmpty();
        leftOut.Should().BeEmpty();
    }
}

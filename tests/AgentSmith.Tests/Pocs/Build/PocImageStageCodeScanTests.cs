using AgentSmith.Contracts.Pocs;
using AgentSmith.Tests.Pocs.Scan;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: a revision built, or blocked at the image stage, passed the code stage — the code scan of
/// a retried or overruling run says so and scans nothing, so an image overrule never lands under 'code'.</summary>
public sealed class PocImageStageCodeScanTests : IAsyncDisposable
{
    private readonly PocScanRig _rig = new();

    [Theory]
    [InlineData(PocRevisionStates.Blocked, PocScanStages.Image, true)]
    [InlineData(PocRevisionStates.Built, null, false)]
    public async Task Scan_PassedEarlier_IsSettled(string state, string? stage, bool overrule)
    {
        _rig.Revision(1, PocZips.App(("app/src/main.py", PocZips.Text("print(1)\n"))));
        (_rig.Store.Rows[1].State, _rig.Store.Rows[1].BlockedStage) = (state, stage);

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1, overrule), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("passed the code scan earlier");
        _rig.Store.Rows[1].OverruledIdsJson.Should().BeNull();
    }

    public ValueTask DisposeAsync() => _rig.DisposeAsync();
}

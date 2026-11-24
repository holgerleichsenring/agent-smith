using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: what reaches the image gate's ignore list, and the overrule of an image-stage block.</summary>
public sealed class PocBuildOverruleTests
{
    private readonly PocBuildRig _rig = new();

    [Fact]
    public async Task Execute_ACodeStageOverrule_ReachesTrivyIgnoreWithItsOsvIdAndCveAlias()
    {
        var osv = new PocScanFinding("osv:GHSA-aaaa-bbbb-cccc|lodash|4.0.0", PocScanFindingKinds.Vulnerability, "GHSA-aaaa-bbbb-cccc",
            "package-lock.json", null, "t", "d", null, null, ["CVE-2024-1234"]);
        var row = _rig.Store.Rows[1];
        (row.State, row.VerdictJson) = (PocRevisionStates.Overruled, new PocScanVerdict([osv], []).ToJson());
        row.OverruledIdsJson = PocOverruledIds.Empty.Append(PocScanStages.Code, [osv.Id]).ToJson();
        _rig.States("run-1", CiStates.Red);

        await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        _rig.Starts.Single().Inputs["trivy_ignore"].Split(',').Should().BeEquivalentTo(["GHSA-aaaa-bbbb-cccc", "CVE-2024-1234"]);
    }

    [Fact]
    public async Task Execute_AnImageOverrule_AppendsUnderImage_ClearsTheRun_AndBuildsWithBothStagesIgnored()
    {
        await BlockAsync(PocBuildRig.Vulnerability("CVE-2024-9", 9.8));
        var blocked = PocScanVerdict.FromJson(_rig.Store.Rows[1].ImageVerdictJson)!.Blocks.Select(b => b.Id).ToArray();
        _rig.States("run-2", CiStates.Green);
        _rig.Report("run-2", "0", PocBuildRig.Vulnerability("CVE-2024-9", 9.8));
        _rig.Artifacts["run-2/digest.txt"] = PocBuildRig.Digest;
        _rig.Runner.Setup(r => r.ReadAsync("run-2", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { _rig.Tagged = PocBuildRig.Digest; return new Contracts.Providers.PipelineRunState(CiStates.Green, "u", []); });

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(overrule: true, blocked), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Starts[^1].Inputs["trivy_ignore"].Should().Be("CVE-2024-9");
        _rig.Store.StoredRuns.Should().Equal("run-1", null, "run-2");
        PocOverruledIds.FromJson(_rig.Store.Rows[1].OverruledIdsJson).Of(PocScanStages.Image).Should().Equal(blocked);
        var notes = PocScanVerdict.FromJson(_rig.Store.Rows[1].ImageVerdictJson)!.Notes;
        notes.Should().ContainSingle().Which.Detail.Should().Contain("Lifted by an overrule");
        _rig.Events.OfType<Contracts.Events.PocOverruledEvent>().Should().ContainSingle(e => e.Stage == PocScanStages.Image);
    }

    [Fact]
    public async Task Execute_AnImageBlockHoldingASecret_IsNotOverruled()
    {
        await BlockAsync(PocBuildRig.Secret("private-key"));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(overrule: true, "x"), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("cannot be overruled");
        _rig.Starts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_AnImageBlockWithoutOverrule_StaysBlocked()
    {
        await BlockAsync(PocBuildRig.Vulnerability("CVE-2024-9", 9.8));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.Message.Should().Contain("blocked by the image scan earlier");
    }

    private async Task BlockAsync(string entry)
    {
        _rig.States("run-1", CiStates.Red);
        _rig.Report("run-1", "1", entry);
        (await _rig.Handler().ExecuteAsync(_rig.Context(), default)).Message.Should().Contain("blocked by the image scan");
    }
}

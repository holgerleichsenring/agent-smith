using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the gate step. A blocking finding stores the verdict and the explanation, blocks a PoC that never
/// reached ready and ends the run blocked; a clean revision passes; a blocked PoC whose next revision passes goes back to
/// importing; a scanned revision keeps its verdict unless the run overrules it, and an overrule passes only when every
/// block is a lifted vulnerability.
/// </summary>
public sealed class PocCodeScanHandlerTests : IAsyncDisposable
{
    private const string Key = "AKIAABCDEFGHIJKLMNOP";
    private readonly PocScanRig _rig = new();

    [Fact]
    public async Task Scan_AnAwsKey_BlocksTheRevisionAndANeverReadyPoc_WithTheExplanation()
    {
        _rig.Revision(1, PocZips.App(("app/src/config.py", PocZips.Text($"KEY = \"{Key}\"\n"))));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("blocked by 1 finding(s)").And.Contain("nothing was committed");
        var row = _rig.Store.Rows[1];
        row.State.Should().Be(PocRevisionStates.Blocked);
        row.Explanation.Should().Be(_rig.Chats.Answer);
        PocScanVerdict.FromJson(row.VerdictJson)!.Blocks.Should().ContainSingle().Which.MaskedSnippet.Should().Contain("<secret at src/config.py:1>");
        _rig.Store.PocState.Should().Be(PocStates.Blocked);
        _rig.Chats.Prompts.Should().ContainSingle().Which.Should().Contain("<secret at src/config.py:1>").And.NotContain(Key);
    }

    [Fact]
    public async Task Scan_AReadyPoc_KeepsServing_WhenANewRevisionIsBlocked()
    {
        _rig.Store.PocState = PocStates.Ready;
        _rig.Revision(1, PocZips.App(("app/src/config.py", PocZips.Text($"KEY = \"{Key}\"\n"))));

        await _rig.Handler().ExecuteAsync(_rig.Context(1), default);

        _rig.Store.PocState.Should().Be(PocStates.Ready);
    }

    [Fact]
    public async Task Scan_ACleanRevision_Passes_NoExplanation_AndABlockedPocGoesBackToImporting()
    {
        _rig.Store.PocState = PocStates.Blocked;
        _rig.Revision(2, PocZips.App(("app/settings.py", PocZips.Text("password = \"CHANGE_ME_PLEASE\"\n"))));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(2), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Store.Rows[2].State.Should().Be(PocRevisionStates.Validated);
        PocScanVerdict.FromJson(_rig.Store.Rows[2].VerdictJson)!.Notes.Should().NotBeEmpty();
        _rig.Store.PocState.Should().Be(PocStates.Importing);
        _rig.Chats.Prompts.Should().BeEmpty();
    }

    [Fact]
    public async Task Scan_AFailingExplanation_LeavesTheVerdictBlocked()
    {
        _rig.Chats.Fails = true;
        _rig.Revision(1, PocZips.App(("app/a.py", PocZips.Text(Key))));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1), default);

        result.Message.Should().Contain("No explanation was written");
        (_rig.Store.Rows[1].State, _rig.Store.Rows[1].Explanation).Should().Be((PocRevisionStates.Blocked, null));
    }

    [Fact]
    public async Task Scan_ABlockedRevision_IsNotScannedAgain_WithoutAnOverrule()
    {
        _rig.Revision(1, PocZips.App(), PocRevisionStates.Blocked);

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1), default);

        result.IsSuccess.Should().BeFalse();
        _rig.Store.Verdicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Overrule_ALiftedVulnerability_Passes_AndPublishesOverruled()
    {
        _rig.Osv.Report = Report("9.8");
        _rig.Revision(1, PocZips.App(("app/requirements.txt", PocZips.Text("django==3.2.0\n"))));
        await _rig.Handler().ExecuteAsync(_rig.Context(1), default);
        _rig.Store.Rows[1].OverruledIdsJson = """{"image":["trivy:x"]}""";

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1, overrule: true, "accepted for the demo", ["osv:PYSEC-1|django|3.2.0"]), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Store.Rows[1].State.Should().Be(PocRevisionStates.Overruled);
        var ids = PocOverruledIds.FromJson(_rig.Store.Rows[1].OverruledIdsJson);
        (ids.Of("image"), ids.Of("code")).Should().BeEquivalentTo((new[] { "trivy:x" }, new[] { "osv:PYSEC-1|django|3.2.0" }));
        var e = _rig.Events.OfType<PocOverruledEvent>().Should().ContainSingle().Subject;
        (e.Stage, e.Actor, e.Reason).Should().Be(("code", "ann-sub", "accepted for the demo"));
        e.FindingIds.Should().Equal("osv:PYSEC-1|django|3.2.0");
    }

    [Fact]
    public async Task Overrule_ANewSecretOrVulnerability_StoresTheNewVerdict_AndStaysBlocked()
    {
        _rig.Osv.Report = Report("9.8");
        _rig.Revision(1, PocZips.App(("app/requirements.txt", PocZips.Text("django==3.2.0\n")), ("app/a.py", PocZips.Text(Key))),
            PocRevisionStates.Blocked);

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1, overrule: true, "go", ["osv:PYSEC-1|django|3.2.0"]), default);

        result.IsSuccess.Should().BeFalse();
        _rig.Store.Rows[1].State.Should().Be(PocRevisionStates.Blocked);
        PocScanVerdict.FromJson(_rig.Store.Rows[1].VerdictJson)!.Blocks.Should().Contain(b => b.Kind == PocScanFindingKinds.Secret);
        _rig.Events.OfType<PocOverruledEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Overrule_TheCodeRescan_IgnoresIdsUnderImage()
    {
        _rig.Osv.Report = Report("9.8");
        _rig.Revision(1, PocZips.App(("app/requirements.txt", PocZips.Text("django==3.2.0\n"))), PocRevisionStates.Blocked);
        _rig.Store.Rows[1].OverruledIdsJson = """{"image":["osv:PYSEC-1|django|3.2.0"]}""";

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(1, overrule: true, "go", []), default);

        result.IsSuccess.Should().BeFalse("only ids under 'code' lift a code block");
    }

    private static string Report(string severity) => $$"""
        {"results": [{"source": {"path": "/work/lockfiles/requirements.txt", "type": "lockfile"},
          "packages": [{"package": {"name": "django", "version": "3.2.0", "ecosystem": "PyPI"},
            "vulnerabilities": [{"id": "PYSEC-1", "summary": "SQL injection", "affected": []}],
            "groups": [{"ids": ["PYSEC-1"], "aliases": ["CVE-1"], "max_severity": "{{severity}}"}]}]}]}
        """;

    public ValueTask DisposeAsync() => _rig.DisposeAsync();
}

using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Security;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: the scoreboard's own mechanics, proved with NO model and NO credentials.
/// <para>
/// This is the half that has to be trustworthy before any number is worth reading: the
/// corpus materialises, the catalog the scan reads really carries pattern definitions, the
/// two rates count over their own populations, and the absence of an agent CLI is loud. A
/// scoreboard that quietly scores nothing is worse than no scoreboard.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SecurityCorpusMechanicsTests(ITestOutputHelper output)
{
    [Fact]
    public void SecurityCorpus_AFlawedFileWithNoFinding_CountsAsAMiss()
    {
        var corpus = SecurityCorpusFixtures.OneOfEach();

        var report = SecurityCorpusFixtures.ReportOf(corpus);

        report.Misses.Should().Be(1, "a declared weakness nothing named is exactly a miss");
        report.MissedFiles.Should().ContainSingle()
            .Which.Should().Be(SecurityCorpusFixtures.FlawedPath,
                "a rate alone does not tell anyone what to go and look at");
        report.FalseAlarms.Should().Be(0, "the clean file was not named either");
    }

    [Fact]
    public void SecurityCorpus_ACleanTrapWithAFinding_CountsAsAFalseAlarm()
    {
        var corpus = SecurityCorpusFixtures.OneOfEach();

        var report = SecurityCorpusFixtures.ReportOf(
            corpus, SecurityCorpusFixtures.FindingOn(SecurityCorpusFixtures.CleanPath, 15));

        report.FalseAlarms.Should().Be(1, "the file is sound and shaped to look otherwise");
        report.Misses.Should().Be(1, "and the real weakness was still not found");
    }

    [Fact]
    public void SecurityCorpus_EachRate_UsesItsOwnDenominator()
    {
        // Four flawed, one clean: a combined score over five would read 20% either way and
        // hide which direction the scan is wrong in.
        var corpus = new SecurityCorpus
        {
            Id = "denominators",
            Files =
            [
                Flawed("a.ts"), Flawed("b.ts"), Flawed("c.ts"), Flawed("d.ts"), Clean("e.ts"),
            ],
        };

        var report = SecurityCorpusFixtures.ReportOf(
            corpus,
            SecurityCorpusFixtures.FindingOn("a.ts", 1),
            SecurityCorpusFixtures.FindingOn("e.ts", 1));

        report.FlawedPopulation.Should().Be(4);
        report.CleanPopulation.Should().Be(1);
        report.Misses.Should().Be(3);
        report.FalseAlarms.Should().Be(1);
        report.MissRate.Should().BeApproximately(0.75, 0.0001, "three of four flawed files");
        report.FalseAlarmRate.Should().BeApproximately(1.0, 0.0001, "the one clean file");
    }

    [Fact]
    public void SecurityCorpus_AFindingOnTheWrongLineOfAFlawedFile_StillCountsAsDetection()
    {
        var corpus = SecurityCorpusFixtures.OneOfEach();

        var report = SecurityCorpusFixtures.ReportOf(
            corpus,
            SecurityCorpusFixtures.FindingOn(
                SecurityCorpusFixtures.FlawedPath, SecurityCorpusFixtures.FlawedLine + 4));

        report.Misses.Should().Be(0,
            "a finding that cited the call rather than the sink has still detected it");
        report.Detections.Should().Be(1);
        report.LineAccurateDetections.Should().Be(0,
            "the citation is a sub-metric and says so — it never gates the detection");
    }

    /// <summary>
    /// The path a finding carries is the sandbox's, and the corpus declares a repo-relative
    /// one. They must still be one file — and <c>a.ts</c> must not become <c>ba.ts</c>.
    /// </summary>
    [Fact]
    public void SecurityCorpus_AFindingUnderTheSandboxPrefix_ScoresAgainstTheDeclaredPath()
    {
        var corpus = SecurityCorpusFixtures.OneOfEach();

        var report = SecurityCorpusFixtures.ReportOf(
            corpus,
            SecurityCorpusFixtures.FindingOn(
                "default/" + SecurityCorpusFixtures.FlawedPath, SecurityCorpusFixtures.FlawedLine));

        report.Detections.Should().Be(1, "the workdir prefix is not a different file");
        report.LineAccurateDetections.Should().Be(1);
    }

    [Fact]
    public void SecurityCorpus_NoAgentCliConfigured_SkipsAndSaysSo()
    {
        const string absent = "agentsmith-no-such-agent-cli";

        AgentCliProbe.Resolve(absent).Should().BeNull("nothing answers to that name");
        EvalChatClientEnv.TryBuildWorker(new ServiceCollection().BuildServiceProvider(), absent)
            .Should().BeNull("no CLI, no client — and no attempt to build one");
        AgentCliProbe.SkipReason(absent).Should()
            .Contain(absent, "the reason names the binary it looked for").And
            .Contain("NOTHING WAS MEASURED",
                "a silent skip is indistinguishable from a pass, which is the failure this "
                + "whole suite exists to prevent");
    }

    [Fact]
    public void SecurityCorpus_TheReport_StatesThatAPublicCorpusCannotGradeTheScan()
    {
        var report = SecurityCorpusFixtures.ReportOf(SecurityCorpusFixtures.OneOfEach());

        var markdown = SecurityCorpusReportWriter.RenderMarkdown(report);

        markdown.Should().Contain(SecurityCorpusReport.CannotGradeSentence,
            "a number printed without that sentence will be read as a grade");
        markdown.IndexOf(SecurityCorpusReport.CannotGradeSentence, StringComparison.Ordinal)
            .Should().BeLessThan(markdown.IndexOf("Misses:", StringComparison.Ordinal),
                "it leads the report; a caveat below the number is a caveat nobody reads");
    }

    [Fact]
    public async Task SecurityCorpus_TheCommittedCorpus_MaterialisesIntoAWorkingTree()
    {
        var corpora = SecurityCorpusLoader.LoadAll(SecurityCorpusLoader.DefaultDirectory);
        corpora.Should().NotBeEmpty("the corpus is what makes this a measurement");

        foreach (var corpus in corpora)
        {
            corpus.Flawed.Should().NotBeEmpty($"{corpus.Id} has no weakness to miss");
            corpus.Clean.Should().NotBeEmpty($"{corpus.Id} has no sound file to false-alarm on");
            await using var tree = await SecurityCorpusTree.MaterialiseAsync(corpus);
            tree.WrittenPaths().Should().BeEquivalentTo(corpus.Files.Select(f => f.Path),
                "every declared file is on disk, and nothing else is");
            Directory.Exists(Path.Combine(tree.Root, ".git")).Should().BeTrue(
                "one of the scan's steps walks the history");
            var first = corpus.Files[0];
            (await File.ReadAllTextAsync(Path.Combine(tree.Root, first.Path)))
                .Should().Be(first.Content, "the tree is the fixture, byte for byte");
        }
    }

    /// <summary>
    /// The guard that stops this whole phase from measuring nothing. The harness's own two
    /// catalog roots carry no <c>patterns/</c> directory at all, so a scan composed with
    /// either would apply ZERO patterns and score 0/N by construction, with nothing saying
    /// so. The eval composition reads the embedded release instead — and this proves it.
    /// </summary>
    [Fact]
    public async Task SecurityCorpus_TheEvalCatalog_CarriesThePatternDefinitionsTheScanApplies()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), SandboxBackend.Stub, session: null,
            SkillsBackend.Stub, ScanEvalComposition.DrivenByAgentCli());

        var directory = harness.Services.GetRequiredService<PatternsDirectoryResolver>().Resolve();
        var definitions = harness.Services.GetRequiredService<PatternDefinitionLoader>()
            .LoadFromDirectory(directory);

        output.WriteLine($"patterns: {definitions.Count} definitions from {directory}");
        definitions.Should().NotBeEmpty(
            "a static pattern scanner with no definitions and a clean repository produce the "
            + "same empty result — which is how a detection score becomes 0/N by construction");
        harness.Services.GetRequiredService<AgentSmith.Contracts.Services.IPromptCatalog>()
            .Get("security-master").Should().NotBeNullOrWhiteSpace(
                "the master under test is the packaged one, not a stub template");
    }

    private static SecurityCorpusFile Flawed(string path) => new()
    {
        Path = path, Verdict = SecurityCorpus.Verdicts.Flawed, Class = "sql-injection",
        Line = 1, Content = "// flawed",
    };

    private static SecurityCorpusFile Clean(string path) => new()
    {
        Path = path, Verdict = SecurityCorpus.Verdicts.Clean, Class = "sql-injection",
        Line = 1, Content = "// sound",
    };
}

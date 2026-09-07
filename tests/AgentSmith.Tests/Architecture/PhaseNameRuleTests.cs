using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521 / 2026-09-07-4e6a: a phase is named by a topic label and stated in one sentence.
/// <para>
/// The drift was measured, not felt: mean slug length ran 31 characters at p00xx and 54
/// at p05xx, and the median goal reached 251 characters with a longest of 3631. p0521
/// capped the slug at fifty characters and put a four-word floor under it, so a name was
/// a short sentence. 4e6a reversed the floor: the claim is already in the goal, and a
/// fifty-character slug is the worse place to keep it. A label of two to five words,
/// area-first, identifies and groups; the goal states.
/// </para>
/// <para>
/// Scope is a NAMESPACE, not an ordering. The counter namespace closed, so the two id
/// shapes are alternatives and a date-minted id sorts BELOW every counter id as text — a
/// cutoff phrased "at or above this id" would exempt every phase minted from now on,
/// permanently, while its own exemption test passed. The one-off relabelling of p0400
/// upward was a migration boundary, not this rule's scope.
/// </para>
/// </summary>
public sealed class PhaseNameRuleTests
{
    /// <summary>
    /// Fifty, because that is what the product already mints. A repo rule tighter than
    /// <see cref="PhaseIdFactory.Slug"/> would ship a product breaking its own rule in
    /// every customer repository.
    /// </summary>
    private const int MaxSlugChars = 50;

    /// <summary>
    /// A label of at most five words. The ceiling is what separates a label from a
    /// sentence: "the-account-sees-what-the-agent-ran" is seven words and a claim;
    /// "account-sees-agent-commands" is four and a place to look. The floor is two —
    /// a bare noun names an area and nothing in it — and is not enforced: relabelling
    /// is judgement applied in the migration, not something a count proves.
    /// </summary>
    private const int MaxSlugWords = 5;

    /// <summary>
    /// One sentence. Stated HERE and not in the embedded schema, which the deployed
    /// server evaluates on every model-authored draft — see
    /// <see cref="PhaseSpecFileSchemaTests"/>.
    /// </summary>
    private const int MaxGoalChars = 200;

    [Fact]
    public void PhaseSlug_ADateMintedPhase_FitsTheLengthBound() =>
        Offenders(PhaseNameBaseline.SlugLength,
                file => file.Slug.Length > MaxSlugChars, file => file.Slug.Length)
            .Should().BeEmpty(
                $"a phase name fits a line — at most {MaxSlugChars} characters, which is "
                + "what PhaseIdFactory.Slug already mints. Do not add a baseline row.");

    /// <summary>
    /// No exemption exists for this one, and none is needed: every phase in the open
    /// namespace was relabelled in 4e6a, so the ceiling pins a convention that holds.
    /// </summary>
    [Fact]
    public void PhaseSlug_ADateMintedPhase_FitsTheWordCeiling() =>
        Offenders(rule: null, file => file.SlugWords > MaxSlugWords, file => file.SlugWords)
            .Should().BeEmpty(
                $"a phase name is a topic label — at most {MaxSlugWords} words, area-first. "
                + "The claim belongs in goal:, which already states it.");

    /// <summary>
    /// A label may repeat: the id is the identity and the goal says which of two phases
    /// on one topic a file is. The closed namespace holds names minted twice and keeps
    /// them; so may the open one.
    /// </summary>
    [Fact]
    public void PhaseSlug_ACounterNamespaceDuplicate_IsKept() =>
        PhaseSpecFile.All()
            .Where(file => !file.IsDateMinted && file.Slug.Length > 0)
            .GroupBy(file => file.Slug, StringComparer.Ordinal)
            .Where(group => group.Select(file => file.PhaseId)
                .Distinct(StringComparer.Ordinal).Count() > 1)
            .Should().NotBeEmpty(
                "the closed namespace holds names minted twice and none is rewritten");

    [Fact]
    public void PhaseGoal_ADateMintedPhase_FitsOneSentence() =>
        Offenders(PhaseNameBaseline.GoalLength,
                file => file.GoalLength > MaxGoalChars, file => file.GoalLength)
            .Should().BeEmpty(
                $"a goal is one sentence, at most {MaxGoalChars} characters. The reasoning "
                + "belongs in decisions: — do not add a baseline row.");

    /// <summary>
    /// The exemption is real, not vacuous: the closed namespace holds names past the word
    /// ceiling — below p0400, where the one-off relabelling did not reach — and the rule
    /// above is green anyway. Asserting the violations EXIST is what stops the scoping
    /// being silently correct for the wrong reason: an ordering cutoff would also leave
    /// these green, while exempting the open namespace too.
    /// </summary>
    [Fact]
    public void PhaseSlug_ACounterNamespacePhase_IsNotJudged()
    {
        var closed = PhaseSpecFile.All().Where(file => !file.IsDateMinted).ToList();

        closed.Should().Contain(file => file.SlugWords > MaxSlugWords,
            "the closed namespace holds sentence names below p0400 — "
            + "p0189-context-yaml-parse-errors-and-stream-in-tree among them — and keeps them");
    }

    [Fact]
    public void PhaseGoal_ACounterNamespacePhase_IsNotJudged() =>
        PhaseSpecFile.All()
            .Where(file => !file.IsDateMinted && file.GoalLength > MaxGoalChars)
            .Should().NotBeEmpty(
                "the closed namespace holds goals well past the bound — 424 of them — and "
                + "none is ever rewritten, so the rule must not reach them");

    /// <summary>
    /// One number in two places. A rule tighter than the generator would refuse names the
    /// product itself mints; a rule looser would let the generator define the convention.
    /// The word ceiling has no such pin yet — the generator still mints a sentence from the
    /// goal, and bounding it is 2026-09-07-e9a2.
    /// </summary>
    [Fact]
    public void SlugGenerator_AndTheRule_AgreeOnOneNumber()
    {
        PhaseIdFactory.MaxSlugLength.Should().Be(MaxSlugChars);
        PhaseIdFactory.Slug(new string('x', 400) + " and then some")
            .Length.Should().BeLessThanOrEqualTo(MaxSlugChars,
                "whatever the generator mints has to pass the rule that judges it");
    }

    /// <summary>
    /// The bound is also stated where a phase is AUTHORED. The methodology skill states it
    /// too, in another repository on its own release cycle — deliberately not read here,
    /// the same ruling <see cref="PhaseIdSchemaTests"/> records for the id pattern.
    /// </summary>
    [Fact]
    public void Guidance_AndTheRule_StateTheSameNumber()
    {
        var guidance = File.ReadAllText(
            Path.Combine(ArchitectureSources.RepositoryRoot, "CLAUDE.md"));

        guidance.Should().Contain($"{MaxSlugChars} characters");
        guidance.Should().Contain($"{MaxSlugWords} words");
        guidance.Should().Contain($"{MaxGoalChars} characters");
    }

    [Fact]
    public void PhaseName_ABaselinedPhase_DidNotGrow()
    {
        var now = Measured();
        var grown = PhaseNameBaseline.Rows
            .Where(row => now.TryGetValue(row.Key, out var value) && value > row.Value)
            .Select(row => $"{row.Key.Rule} {row.Key.PhaseId}: {row.Value} → {now[row.Key]}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        grown.Should().BeEmpty(
            "a phase already over a bound may only get shorter.\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void PhaseName_APhaseThatNowFits_MustLeaveTheBaseline()
    {
        var now = Measured();
        var stale = PhaseNameBaseline.Rows
            .Where(row => !now.TryGetValue(row.Key, out var value) || value <= Cap(row.Key.Rule))
            .Select(row => $"{row.Key.Rule} {row.Key.PhaseId}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "a phase that now fits its bound (or is gone) must leave phase-name-baseline.tsv, "
            + "so the list keeps telling the truth.\n  " + string.Join("\n  ", stale));
    }

    private static int Cap(string rule) => rule switch
    {
        PhaseNameBaseline.SlugLength => MaxSlugChars,
        PhaseNameBaseline.GoalLength => MaxGoalChars,
        _ => PhaseSpecSchemaFile.GoalMaxLength,
    };

    /// <summary>
    /// What every rule measures on every phase TODAY, so the ratchet can compare a
    /// baselined row against the current file. The word ceiling takes no rows and so
    /// needs no measurement here.
    /// </summary>
    private static IReadOnlyDictionary<(string Rule, string PhaseId), int> Measured() =>
        PhaseSpecFile.All()
            .SelectMany(file => new[]
            {
                ((PhaseNameBaseline.SlugLength, file.PhaseId), file.Slug.Length),
                ((PhaseNameBaseline.GoalLength, file.PhaseId), file.GoalLength),
                ((PhaseNameBaseline.SchemaGoalLength, file.PhaseId), file.GoalLength),
            })
            .GroupBy(pair => pair.Item1)
            // Some counter ids name more than one file. The ratchet judges the worst of
            // them, so a duplicate cannot hide behind its shorter twin.
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Item2));

    private static IReadOnlyList<string> Offenders(
        string? rule, Func<PhaseSpecFile, bool> breaks, Func<PhaseSpecFile, int> measure) =>
    [
        .. PhaseSpecFile.All()
            .Where(file => file.IsDateMinted && breaks(file))
            .Where(file => rule is null || !PhaseNameBaseline.Exempts(rule, file.PhaseId))
            .Select(file => $"{file.PhaseId}: {measure(file)} — {file.Slug}")
            .OrderBy(text => text, StringComparer.Ordinal),
    ];
}

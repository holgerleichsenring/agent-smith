using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-09-7f48: a test gets its schema from <see cref="TestSupport.MigratedStoreTemplate"/>.
/// <para>
/// Applying the migration set is CPU work measured in tenths of a second per store, and 471
/// tests paid it in their constructors although a migrated template existed — together the
/// tail of every suite run. A copy carries the same schema and the same history at the price
/// of a page copy. Only a test whose subject IS migrating may migrate, and it is named here.
/// </para>
/// </summary>
public sealed class TestMigrationRuleTests
{
    // Any call NAMED Migrate or MigrateAsync: Database.Migrate(), IMigrator.MigrateAsync(...),
    // a store migrator's MigrateAsync, and a helper called Migrate(dbPath) all apply the set.
    private static readonly Regex Migrates = new(@"\bMigrate(Async)?\(", RegexOptions.Compiled);

    // Named, not patterned: the template itself; SQL Server stores, which no SQLite template
    // can serve; and the tests whose assertion is a migration step running over existing rows.
    private static readonly HashSet<string> MigrationIsTheSubject =
    [
        "MigratedStoreTemplate.cs",
        "ScratchSqlServer.cs",
        "DataArchiveSqlServerTests.cs",
        "ConfigStudioApiSmokeTests.cs",
        "RunStoreRepairTests.cs",
        "RunSpecsRenameMigrationTests.cs",
        "StepAttributionPersistenceTests.cs",
    ];

    [Fact]
    public void TestMigration_NoTestOutsideTheTemplate_CallsMigrate()
    {
        var offenders = Directory.EnumerateFiles(TestSourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !MigrationIsTheSubject.Contains(Path.GetFileName(f)))
            .Where(f => Path.GetFileName(f) != nameof(TestMigrationRuleTests) + ".cs")
            .Where(f => File.ReadLines(f).Any(line => !IsComment(line) && Migrates.IsMatch(line)))
            .Select(Path.GetFileName)
            .ToList();

        offenders.Should().BeEmpty(
            "a test store comes from MigratedStoreTemplate.OpenCopy/CopyInto/CopyToFile; "
            + "migrating per test is the cost this rule exists to keep out");
    }

    [Fact]
    public void Rule_HasTeeth_TheMarkerMatchesEveryShape()
    {
        Migrates.IsMatch("_context.Database.Migrate();").Should().BeTrue();
        Migrates.IsMatch("await db.Database.MigrateAsync();").Should().BeTrue();
        Migrates.IsMatch("await Migrator().MigrateAsync(db, ct);").Should().BeTrue();
        Migrates.IsMatch("Migrate(dbPath);").Should().BeTrue();
        Migrates.IsMatch("CopyMigratedStore(dbPath);").Should().BeFalse();
    }

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//") || trimmed.StartsWith('*');
    }

    private static string TestSourceRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.Tests.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the rule needs the test project's own sources");
        return dir!.FullName;
    }
}

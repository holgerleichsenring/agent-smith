namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: loads the committed measurement table from the checkout. The table sits
/// under Reports/, deliberately OUTSIDE every fixture root — the project analyzer
/// reads the fixture tree, and a file enumerating dbt and databricks commands
/// inside it would be measuring a repository that contains the answers.
/// </summary>
public sealed class MeasuredCommandsSource
{
    public const string TableFileName = "measured-commands.tsv";

    private readonly CheckoutRoot _root = new();

    public string TablePath() => Path.Combine(_root.ReportsDirectory(), TableFileName);

    public string FixturesDirectory() => _root.FixturesDirectory();

    public MeasuredCommandTable Load() => new MeasuredCommandTableReader().Read(TablePath());

    public IEnumerable<string> FixtureShapes() =>
        Directory.EnumerateDirectories(FixturesDirectory()).Select(Path.GetFileName)!;

    public IEnumerable<string> FixtureVariants(string shape) =>
        Directory.EnumerateDirectories(Path.Combine(FixturesDirectory(), shape))
            .Select(Path.GetFileName)!;
}

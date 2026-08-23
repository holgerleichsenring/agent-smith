namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: reads tools/measure-data-toolchain.sh's TSV. Comment lines carry the
/// provenance; the <c># fixture-hash</c> ones pin the table to the fixture trees
/// that produced it.
/// </summary>
public sealed class MeasuredCommandTableReader
{
    private const string HashMarker = "# fixture-hash";
    private const int ColumnCount = 9;

    public MeasuredCommandTable Read(string tsvPath)
    {
        var rows = new List<MeasuredCommand>();
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenHeader = false;

        foreach (var line in File.ReadAllLines(tsvPath))
        {
            if (line.StartsWith(HashMarker, StringComparison.Ordinal))
            {
                var parts = line.Split('\t');
                hashes[parts[1]] = parts[2];
                continue;
            }
            if (line.Length == 0 || line[0] == '#') continue;
            if (!seenHeader) { seenHeader = true; continue; }
            rows.Add(ParseRow(line, tsvPath));
        }

        return new MeasuredCommandTable(rows, hashes);
    }

    private static MeasuredCommand ParseRow(string line, string tsvPath)
    {
        var f = line.Split('\t');
        if (f.Length != ColumnCount)
            throw new InvalidOperationException(
                $"{tsvPath}: expected {ColumnCount} tab-separated fields, found {f.Length}: {line}");
        return new MeasuredCommand(
            f[0], f[1], f[2], int.Parse(f[3]), f[4], f[5], f[6], f[7], f[8]);
    }
}

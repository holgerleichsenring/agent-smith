using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Core.Services.Verification;

/// <summary>
/// 2026-08-30-0ea8: reads the lens table — requirement id to the stations it applies to.
/// The table holds ids and classifications only: the standard is licensed share-alike, and
/// a table reproducing its text would be adapted material rather than a collection of ids.
/// </summary>
internal sealed class VerificationLensTableParser
{
    private const char Comment = '#';
    private const char ColumnSeparator = '\t';
    private const string NoStation = "none";

    public IReadOnlyDictionary<string, IReadOnlyList<VerificationStation>> Parse(TextReader table)
    {
        var rows = new Dictionary<string, IReadOnlyList<VerificationStation>>(StringComparer.Ordinal);
        while (table.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == Comment) continue;
            var columns = line.Split(ColumnSeparator);
            if (columns.Length != 2)
                throw new InvalidOperationException(
                    $"Lens row '{line}' is not an id and a station list separated by a tab.");
            if (!rows.TryAdd(columns[0].Trim(), Stations(columns[1].Trim())))
                throw new InvalidOperationException(
                    $"Requirement '{columns[0].Trim()}' is classified twice by the lens table.");
        }
        return rows;
    }

    private static IReadOnlyList<VerificationStation> Stations(string column) =>
        string.Equals(column, NoStation, StringComparison.Ordinal)
            ? []
            : [.. column.Split(',').Select(Station)];

    private static VerificationStation Station(string name) =>
        Enum.TryParse<VerificationStation>(name, ignoreCase: true, out var station)
            ? station
            : throw new InvalidOperationException(
                $"'{name}' is not a station of a request; the lens table names one that does not exist.");
}

namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-28-2af6: one table's entry in a data archive manifest — the table name the
/// archive file is named after, and how many rows the archive holds for it. The import
/// compares the number it wrote against this one rather than trusting that it wrote
/// everything.
/// </summary>
/// <param name="Table">The table name, as the model declares it.</param>
/// <param name="Rows">The number of rows the archive carries for that table.</param>
public sealed record ArchivedTable(string Table, long Rows);

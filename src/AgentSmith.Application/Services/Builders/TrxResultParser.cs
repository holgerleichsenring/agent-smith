using System.Xml;
using System.Xml.Linq;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Parses Visual Studio Test Results (TRX) XML into a TrxSummary.
/// Reads counters from ResultSummary/Counters and failure details from
/// UnitTestResult elements with outcome=Failed.
/// </summary>
public sealed class TrxResultParser
{
    private static readonly XNamespace TrxNs = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public TrxSummary Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return TrxSummary.Empty;
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return TrxSummary.Empty;
        }
        var counters = ReadCounters(doc);
        var failures = ReadFailures(doc);
        return new TrxSummary(counters.Total, counters.Passed, counters.Failed, failures);
    }

    private static (int Total, int Passed, int Failed) ReadCounters(XDocument doc)
    {
        var counters = doc.Descendants(TrxNs + "Counters").FirstOrDefault();
        if (counters is null) return (0, 0, 0);
        return (
            ParseInt(counters.Attribute("total")?.Value),
            ParseInt(counters.Attribute("passed")?.Value),
            ParseInt(counters.Attribute("failed")?.Value));
    }

    private static IReadOnlyList<FailedTest> ReadFailures(XDocument doc)
    {
        return doc.Descendants(TrxNs + "UnitTestResult")
            .Where(r => string.Equals(r.Attribute("outcome")?.Value, "Failed", StringComparison.OrdinalIgnoreCase))
            .Select(BuildFailedTest)
            .ToList();
    }

    private static FailedTest BuildFailedTest(XElement result)
    {
        var name = result.Attribute("testName")?.Value ?? "<unknown>";
        var errorInfo = result.Element(TrxNs + "Output")?.Element(TrxNs + "ErrorInfo");
        var message = errorInfo?.Element(TrxNs + "Message")?.Value;
        var stack = errorInfo?.Element(TrxNs + "StackTrace")?.Value;
        return new FailedTest(name, message, stack);
    }

    private static int ParseInt(string? raw) => int.TryParse(raw, out var n) ? n : 0;
}

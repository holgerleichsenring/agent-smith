using System.Xml;
using System.Xml.Linq;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0419: reads the package sources a repository declares. Text in, sources out —
/// what to do with them, and what to say when a file is broken, belongs to the caller.
/// </summary>
public static class PackageSourceParser
{
    /// <summary>
    /// NuGet sources from a nuget.config. <paramref name="problem"/> is set when the
    /// file exists but cannot be read as XML — an unreadable feed config must be heard
    /// about, not skipped: run 354b lost its private-feed credentials to a silent catch.
    /// </summary>
    public static IReadOnlyList<(string Name, string Url)> NuGetSources(
        string content, out string? problem)
    {
        problem = null;
        XDocument doc;
        // A UTF-8 BOM makes XDocument.Parse throw at position 1, and a BOM is simply how
        // Visual Studio writes the file — both repositories in run 354b shipped one.
        try { doc = XDocument.Parse(content.TrimStart('﻿', ' ', '\t', '\r', '\n')); }
        catch (XmlException ex)
        {
            problem = ex.Message;
            return [];
        }

        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        return doc.Descendants(ns + "packageSources")
            .Elements(ns + "add")
            .Where(e => !string.Equals(
                (string?)e.Attribute("key"), "clear", StringComparison.OrdinalIgnoreCase))
            .Select(e => ((string?)e.Attribute("key"), (string?)e.Attribute("value")))
            .Where(pair => !string.IsNullOrEmpty(pair.Item1) && !string.IsNullOrEmpty(pair.Item2))
            .Select(pair => (pair.Item1!, pair.Item2!))
            .ToList();
    }

    /// <summary>npm registries from an .npmrc: `registry=…` and `@scope:registry=…`.</summary>
    public static IEnumerable<(string Key, string Url)> NpmRegistries(string content)
    {
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            if (string.Equals(key, "registry", StringComparison.OrdinalIgnoreCase)
                || key.EndsWith(":registry", StringComparison.OrdinalIgnoreCase))
            {
                yield return (key, line[(idx + 1)..].Trim());
            }
        }
    }
}

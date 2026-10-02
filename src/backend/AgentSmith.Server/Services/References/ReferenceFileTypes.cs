namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the files a website is made of, by extension, and the media type each is
/// stored with. The extension decides, not the bytes: unlike a dialog image, a site's favicon.ico,
/// avif and svg have no magic number the image path would accept, and they are what a site holds.
/// </summary>
public sealed class ReferenceFileTypes
{
    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html", [".htm"] = "text/html", [".css"] = "text/css",
        [".js"] = "text/javascript", [".mjs"] = "text/javascript", [".json"] = "application/json",
        [".svg"] = "image/svg+xml", [".png"] = "image/png", [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
        [".avif"] = "image/avif", [".ico"] = "image/x-icon", [".woff"] = "font/woff",
        [".woff2"] = "font/woff2", [".ttf"] = "font/ttf", [".otf"] = "font/otf",
        [".txt"] = "text/plain", [".md"] = "text/markdown",
    };

    /// <summary>The extensions a set may hold, as a refusal lists them.</summary>
    public static string Allowed => string.Join(", ", MediaTypes.Keys.Select(k => k[1..]));

    /// <summary>The media type <paramref name="path"/> is stored with, or null when its extension is not allowed.</summary>
    public string? MediaTypeOf(string path) =>
        MediaTypes.TryGetValue(Path.GetExtension(path), out var type) ? type : null;
}

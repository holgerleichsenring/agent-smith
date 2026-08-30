using System.IO.Compression;
using System.Text;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Compresses a finished SARIF document to base64-gzip, the wire form the GitHub Code
/// Scanning upload API takes. Moved out of SarifOutputStrategy verbatim: producing a
/// document and packing one for a transport are two reasons to change.
/// </summary>
public static class SarifUploadCompressor
{
    public static string ToBase64Gzip(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal))
            gzip.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }
}

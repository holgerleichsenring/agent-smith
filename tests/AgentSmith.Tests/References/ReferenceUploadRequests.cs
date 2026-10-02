using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AgentSmith.Tests.References;

/// <summary>2026-10-01-283db: multipart bodies and archives the way a browser and an operator make them.</summary>
internal static class ReferenceUploadRequests
{
    /// <summary>A request whose multipart body carries each file under its path as the file name.</summary>
    internal static async Task<HttpContext> MultipartAsync(params (string Path, byte[] Bytes)[] files)
    {
        using var content = new MultipartFormDataContent("----283db");
        foreach (var (path, bytes) in files)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"file\"", FileName = $"\"{path}\"",
            };
            content.Add(part);
        }

        var body = new MemoryStream();
        await content.CopyToAsync(body);
        body.Position = 0;
        var http = new DefaultHttpContext();
        http.Request.Body = body;
        http.Request.ContentLength = body.Length;
        http.Request.ContentType = content.Headers.ContentType!.ToString();
        return http;
    }

    internal static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>A ZIP holding <paramref name="files"/>, deflated.</summary>
    internal static byte[] Zip(params (string Path, byte[] Bytes)[] files)
    {
        using var into = new MemoryStream();
        using (var zip = new ZipArchive(into, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, bytes) in files)
            {
                using var entry = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
                entry.Write(bytes);
            }
        return into.ToArray();
    }

    /// <summary>
    /// <paramref name="zip"/> with every uncompressed-size field rewritten to <paramref name="claimed"/> —
    /// in the local headers (offset 22) and in the central directory (offset 24) — so the archive's
    /// directory lies about how much its entries inflate to.
    /// </summary>
    internal static byte[] Lying(byte[] zip, uint claimed)
    {
        var bytes = (byte[])zip.Clone();
        for (var i = 0; i + 4 <= bytes.Length; i++)
        {
            var signature = BitConverter.ToUInt32(bytes, i);
            if (signature == 0x04034b50) BitConverter.GetBytes(claimed).CopyTo(bytes, i + 22);
            if (signature == 0x02014b50) BitConverter.GetBytes(claimed).CopyTo(bytes, i + 24);
        }
        return bytes;
    }
}

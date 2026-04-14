using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace AgentSmith.Infrastructure.Services.Tools;

/// <summary>
/// Transfers files to and from Docker containers via docker cp (tar streams).
/// </summary>
internal static class DockerFileTransfer
{
    public static async Task CopyToContainerAsync(
        DockerClient client, string containerId,
        Dictionary<string, string> files, string workDir, CancellationToken ct)
    {
        using var tar = new MemoryStream();
        TarArchiveBuilder.WriteDirectoryEntry(tar, workDir.TrimStart('/') + "/");
        foreach (var (name, content) in files)
            TarArchiveBuilder.WriteFileEntry(tar,
                workDir.TrimStart('/') + "/" + name, Encoding.UTF8.GetBytes(content));
        tar.Write(new byte[1024]);
        tar.Position = 0;
        await client.Containers.ExtractArchiveToContainerAsync(
            containerId, new ContainerPathStatParameters { Path = "/" }, tar, ct);
    }

    public static async Task<string?> CopyFromContainerAsync(
        DockerClient client, string containerId, string path)
    {
        try
        {
            var response = await client.Containers.GetArchiveFromContainerAsync(
                containerId,
                new GetArchiveFromContainerParameters { Path = path },
                false, CancellationToken.None);
            using var ms = new MemoryStream();
            await response.Stream.CopyToAsync(ms);
            ms.Position = 0;
            var header = new byte[512];
            if (await ms.ReadAsync(header) < 512) return null;
            var sizeStr = Encoding.ASCII.GetString(header, 124, 11).Trim('\0', ' ');
            if (sizeStr.Length == 0) return null;
            var size = Convert.ToInt64(sizeStr, 8);
            if (size == 0) return null;
            var content = new byte[size];
            var read = await ms.ReadAsync(content.AsMemory(0, (int)size));
            return Encoding.UTF8.GetString(content, 0, read);
        }
        catch { return null; }
    }
}

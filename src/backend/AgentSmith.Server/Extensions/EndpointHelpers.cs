namespace AgentSmith.Server.Extensions;

internal static class EndpointHelpers
{
    internal static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }
}

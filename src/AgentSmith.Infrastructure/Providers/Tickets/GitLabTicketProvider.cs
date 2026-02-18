using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Infrastructure.Providers.Tickets;

/// <summary>
/// Fetches issues from GitLab using the REST API v4.
/// </summary>
public sealed class GitLabTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _privateToken;
    private readonly HttpClient _httpClient;

    public string ProviderType => "GitLab";

    public GitLabTicketProvider(
        string baseUrl, string projectPath, string privateToken, HttpClient httpClient)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _projectPath = projectPath;
        _privateToken = privateToken;
        _httpClient = httpClient;
    }

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new TicketNotFoundException(ticketId);

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var root = json.RootElement;

        var title = root.GetProperty("title").GetString() ?? string.Empty;
        var description = root.GetProperty("description").GetString() ?? string.Empty;
        var state = root.GetProperty("state").GetString() ?? string.Empty;

        return new Ticket(ticketId, title, description, null, state, "GitLab");
    }

    public async Task UpdateStatusAsync(
        TicketId ticketId, string comment, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}/notes";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new { body = comment });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CloseTicketAsync(
        TicketId ticketId, string resolution, CancellationToken cancellationToken = default)
    {
        // Post the resolution as a note first.
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);

        // Then close the issue.
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("PRIVATE-TOKEN", _privateToken);
        request.Content = JsonContent.Create(new { state_event = "close" });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

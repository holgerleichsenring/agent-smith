using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using System.Net;
using Moq;

namespace AgentSmith.Tests.Webhooks.Trust;

[Collection(EnvVarCollection.Name)]
public sealed class AzureDevOpsPermissionReaderTests
{
    private const string Org = "https://dev.azure.com/org";
    private static readonly Guid Namespace = Guid.NewGuid();
    private static readonly Guid IdentityId = Guid.NewGuid();
    private static readonly IdentityDescriptor Descriptor = new("Microsoft.IdentityModel.Claims.ClaimsIdentity", "t\\dev@org.com");

    private readonly Mock<IdentityHttpClient> _identities = new(new Uri(Org), new VssCredentials());
    private readonly AclHandler _acls = new();

    [Fact]
    public async Task ReadAsync_TheFirstTokenWithAnAcl_Answers()
    {
        IdentityIs(Descriptor);
        AclAt("repoV2/p", allow: 6, deny: 0);

        var effective = await WithToken(() => Reader().ReadAsync(
            Org, Namespace, ["repoV2/p/r", "repoV2/p", "repoV2"], IdentityId, CancellationToken.None));

        effective.Should().Be(new AzureDevOpsEffectivePermission(6, 0));
        _acls.Asked.Should().HaveCount(2, "the repository token carries no ACL, its project token does");
        _acls.Asked[1].Should().Contain("/_apis/accesscontrollists/").And.Contain("includeExtendedInfo=true")
            .And.Contain("descriptors=Microsoft.IdentityModel.Claims.ClaimsIdentity");
    }

    [Fact]
    public async Task ReadAsync_NoTokenCarriesAnAcl_ReturnsNull()
    {
        IdentityIs(Descriptor);
        AclAt("elsewhere", allow: 4, deny: 0);

        var effective = await WithToken(() => Reader().ReadAsync(
            Org, Namespace, ["repoV2/p/r"], IdentityId, CancellationToken.None));

        effective.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_UnknownIdentity_Throws()
    {
        _identities.Setup(c => c.ReadIdentitiesAsync(It.IsAny<IList<Guid>>(), It.IsAny<QueryMembership>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentitiesCollection());

        var act = () => WithToken(() => Reader().ReadAsync(Org, Namespace, ["repoV2"], IdentityId, CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private void IdentityIs(IdentityDescriptor descriptor) =>
        _identities.Setup(c => c.ReadIdentitiesAsync(
                It.Is<IList<Guid>>(ids => ids.Single() == IdentityId), QueryMembership.None,
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentitiesCollection { new Identity { Descriptor = descriptor } });

    private void AclAt(string token, int allow, int deny) =>
        _acls.Body = $$"""
            { "count": 1, "value": [ { "token": "{{token}}", "inheritPermissions": true,
              "acesDictionary": { "{{Descriptor.IdentityType}};{{Descriptor.Identifier.Replace("\\", "\\\\")}}": {
                "allow": {{allow}}, "deny": {{deny}},
                "extendedInfo": { "effectiveAllow": {{allow}}, "effectiveDeny": {{deny}} } } } } ] }
            """;

    private AzureDevOpsPermissionReader Reader()
    {
        var clients = new Mock<IAzDoClientFactory>();
        clients.Setup(f => f.CreateIdentityClient(Org, "test-pat")).Returns(_identities.Object);
        var http = new Mock<IHttpClientFactory>();
        http.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_acls, false));
        return new AzureDevOpsPermissionReader(new SecretsProvider(), clients.Object, http.Object);
    }

    private static async Task<T> WithToken<T>(Func<Task<T>> act)
    {
        var previous = Environment.GetEnvironmentVariable("AZURE_DEVOPS_TOKEN");
        Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", "test-pat");
        try { return await act(); }
        finally { Environment.SetEnvironmentVariable("AZURE_DEVOPS_TOKEN", previous); }
    }

    private sealed class AclHandler : HttpMessageHandler
    {
        public string Body { get; set; } = """{ "count": 0, "value": [] }""";

        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) });
        }
    }
}

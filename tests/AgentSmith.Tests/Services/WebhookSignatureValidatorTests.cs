using System.Security.Cryptography;
using System.Text;
using AgentSmith.Cli.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class WebhookSignatureValidatorTests
{
    [Fact]
    public void ValidateGitHub_CorrectSignature_ReturnsTrue()
    {
        var secret = "test-secret";
        var payload = """{"action":"labeled"}""";
        var signature = ComputeGitHubSignature(payload, secret);

        WebhookSignatureValidator.ValidateGitHub(payload, signature, secret)
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateGitHub_WrongSignature_ReturnsFalse()
    {
        WebhookSignatureValidator.ValidateGitHub(
            "payload", "sha256=0000000000000000000000000000000000000000000000000000000000000000", "secret")
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateGitHub_EmptySecret_ReturnsTrue()
    {
        WebhookSignatureValidator.ValidateGitHub("payload", "sha256=anything", "")
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateGitHub_MissingPrefix_ReturnsFalse()
    {
        WebhookSignatureValidator.ValidateGitHub("payload", "no-prefix", "secret")
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateGitLab_CorrectToken_ReturnsTrue()
    {
        WebhookSignatureValidator.ValidateGitLab("my-token", "my-token")
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateGitLab_WrongToken_ReturnsFalse()
    {
        WebhookSignatureValidator.ValidateGitLab("wrong", "correct")
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateGitLab_EmptySecret_ReturnsTrue()
    {
        WebhookSignatureValidator.ValidateGitLab("any-token", "")
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateAzureDevOps_CorrectPassword_ReturnsTrue()
    {
        var secret = "my-azdo-secret";
        var header = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"user:{secret}"));

        WebhookSignatureValidator.ValidateAzureDevOps(header, secret)
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateAzureDevOps_WrongPassword_ReturnsFalse()
    {
        var header = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:wrong-password"));

        WebhookSignatureValidator.ValidateAzureDevOps(header, "correct-password")
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateAzureDevOps_EmptySecret_ReturnsTrue()
    {
        WebhookSignatureValidator.ValidateAzureDevOps("Basic anything", "")
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateAzureDevOps_MissingBasicPrefix_ReturnsFalse()
    {
        WebhookSignatureValidator.ValidateAzureDevOps("Bearer token", "secret")
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateAzureDevOps_InvalidBase64_ReturnsFalse()
    {
        WebhookSignatureValidator.ValidateAzureDevOps("Basic !!!invalid!!!", "secret")
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateAzureDevOps_EmptyUsername_CorrectPassword_ReturnsTrue()
    {
        var secret = "my-secret";
        var header = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($":{secret}"));

        WebhookSignatureValidator.ValidateAzureDevOps(header, secret)
            .Should().BeTrue();
    }

    private static string ComputeGitHubSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

using System.Linq.Expressions;
using System.Reflection;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>
/// 2026-11-23-c81ea: a mocked Azure Repos git client whose calls are set up by name. The SDK carries several overloads
/// per call with long optional tails, which an expression tree cannot omit, so each setup reflects the LONGEST overload
/// whose leading parameters match — the one the provider's named-argument call binds to — with It.IsAny for every
/// parameter, and answers through the invocation's arguments.
/// </summary>
internal sealed class AzureGitClientFake
{
    public Mock<GitHttpClient> Client { get; } = new(
        new Uri("https://localhost/fake"), new VssCredentials(new VssBasicCredential(string.Empty, "fake")));

    public void Answer<T>(string method, Type[] leading, Func<IReadOnlyList<object?>, T> answer) =>
        Setup<T>(method, leading).Returns(new InvocationFunc(i => Task.FromResult(answer(i.Arguments.ToList()))));

    public void Throw<T>(string method, Type[] leading, Func<IReadOnlyList<object?>, Exception?> fault, Func<IReadOnlyList<object?>, T> answer) =>
        Setup<T>(method, leading).Returns(new InvocationFunc(i =>
            fault(i.Arguments.ToList()) is { } ex ? Task.FromException<T>(ex) : Task.FromResult(answer(i.Arguments.ToList()))));

    public IAzDoClientFactory Factory()
    {
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>())).Returns(Client.Object);
        factory.Setup(f => f.CreateGitClientAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Client.Object);
        return factory.Object;
    }

    private Moq.Language.Flow.ISetup<GitHttpClient, Task<T>> Setup<T>(string method, Type[] leading)
    {
        var target = typeof(GitHttpClient).GetMethods()
            .Where(m => m.Name == method && m.ReturnType == typeof(Task<T>))
            .Where(m => m.GetParameters().Length >= leading.Length
                && leading.Select((t, i) => m.GetParameters()[i].ParameterType == t).All(x => x))
            .OrderBy(m => m.GetParameters().Length)
            .Last();
        var isAny = typeof(It).GetMethods().First(m => m.Name == nameof(It.IsAny) && m.IsGenericMethod);
        var args = target.GetParameters()
            .Select(p => (Expression)Expression.Call(isAny.MakeGenericMethod(p.ParameterType)));
        var instance = Expression.Parameter(typeof(GitHttpClient), "c");
        return Client.Setup(Expression.Lambda<Func<GitHttpClient, Task<T>>>(Expression.Call(instance, target, args), instance));
    }
}

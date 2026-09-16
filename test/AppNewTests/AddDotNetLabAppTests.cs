using AwesomeAssertions;
using DotNetLab.Features.Updates;
using DotNetLab.Features.Workspace;
using DotNetLab.Infrastructure.Browser;
using DotNetLab.Infrastructure.Worker;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetLab;

[TestClass]
public sealed class AddDotNetLabAppTests
{
    [TestMethod]
    public void RegistersHostNeutralServices()
    {
        var services = new ServiceCollection();
        var environment = new LabEnvironment(true, "https://example.test/");

        services.AddDotNetLabApp(environment);

        HasSingleton<ILabEnvironment>(services).Should().BeTrue();
        HasScoped<LabWorkspaceState>(services).Should().BeTrue();
        HasScoped<WorkerHost>(services).Should().BeTrue();
        HasScoped<HttpClient>(services).Should().BeTrue();
        services.Should().Contain(d =>
            d.ServiceType == typeof(IUpdateChecker) &&
            d.ImplementationType == typeof(DisabledUpdateChecker) &&
            d.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(d =>
            d.ServiceType == typeof(IWorkerTransport) &&
            d.ImplementationType == typeof(UnsupportedWorkerTransport) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [TestMethod]
    public void ResolvesEnvironmentAndHttpClient()
    {
        var services = new ServiceCollection();
        var environment = new LabEnvironment(false, "https://example.test/");
        services.AddDotNetLabApp(environment);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILabEnvironment>().Should().BeSameAs(environment);

        using var client = provider.GetRequiredService<HttpClient>();
        client.BaseAddress.Should().Be(new Uri("https://example.test/"));
        client.DefaultRequestHeaders.UserAgent.ToString().Should().Contain("DotNetLab");
        provider.GetRequiredService<IWorkerTransport>().Should().BeOfType<UnsupportedWorkerTransport>();
    }

    [TestMethod]
    public void UnsupportedTransport_CreateWorkerThrows()
    {
        var transport = new UnsupportedWorkerTransport();
        var act = () => transport.CreateWorker("main.js", _ => { }, _ => { });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Workers are only supported in the browser.");
    }

    [TestMethod]
    public void RegistersRootComponents()
    {
        var registered = new List<(Type Type, string Selector)>();
        AppBuilder.RegisterRootComponents((type, selector) => registered.Add((type, selector)));

        registered.Should().Contain((typeof(App), "#app"));
        registered.Should().Contain((typeof(HeadOutlet), "head::after"));
    }

    private static bool HasSingleton<T>(IServiceCollection services) =>
        services.Any(d => d.ServiceType == typeof(T) && d.Lifetime == ServiceLifetime.Singleton);

    private static bool HasScoped<T>(IServiceCollection services) =>
        services.Any(d => d.ServiceType == typeof(T) && d.Lifetime == ServiceLifetime.Scoped);
}

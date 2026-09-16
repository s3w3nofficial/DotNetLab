using AwesomeAssertions;
using DotNetLab.Infrastructure.Browser;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetLab;

[TestClass]
public sealed class AddDotNetLabAppTests
{
    [TestMethod]
    public void AddDotNetLabApp_IsHostNeutral()
    {
        var method = typeof(AppBuilder).GetMethod(nameof(AppBuilder.AddDotNetLabApp));
        method.Should().NotBeNull();
        method!.IsStatic.Should().BeTrue();
        method.ReturnType.Should().Be(typeof(IServiceCollection));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(IServiceCollection));
        parameters[1].ParameterType.Should().Be(typeof(ILabEnvironment));
        parameters[2].ParameterType.Should().Be(typeof(bool));
        parameters[2].Name.Should().Be("useReduxDevTools");
    }

    [TestMethod]
    public void RegistersRootComponents()
    {
        var registered = new List<(Type Type, string Selector)>();
        AppBuilder.RegisterRootComponents((type, selector) => registered.Add((type, selector)));

        registered.Should().Contain((typeof(App), "#app"));
        registered.Should().Contain((typeof(HeadOutlet), "head::after"));
    }
}

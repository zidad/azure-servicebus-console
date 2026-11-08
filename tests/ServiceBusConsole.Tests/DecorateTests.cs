using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.Extensions;

namespace ServiceBusConsole.Tests;

public sealed class DecorateTests
{
    public interface IGreeter
    {
        string Greet();
    }

    public sealed class Greeter : IGreeter
    {
        public string Greet() => "hi";
    }

    public sealed class Loud(IGreeter inner) : IGreeter
    {
        public string Greet() => inner.Greet().ToUpperInvariant();
    }

    public static IEnumerable<Func<IServiceCollection, IServiceCollection>> Registrations() =>
    [
        s => s.AddSingleton<IGreeter, Greeter>(),
        s => s.AddSingleton<IGreeter>(_ => new Greeter()),
        s => s.AddSingleton<IGreeter>(new Greeter()),
    ];

    [Test]
    [MethodDataSource(nameof(Registrations))]
    public void Decorate_WrapsTheRegistration_ByTypeFactoryOrInstance(Func<IServiceCollection, IServiceCollection> register)
    {
        using var services = register(new ServiceCollection()).Decorate<IGreeter, Loud>().BuildServiceProvider();

        services.GetRequiredService<IGreeter>().ShouldBeOfType<Loud>().Greet().ShouldBe("HI");
        services.GetRequiredService<IGreeter>().ShouldBeSameAs(services.GetRequiredService<IGreeter>());
    }

    [Test]
    public void Decorate_NothingRegistered_Throws() =>
        Should.Throw<InvalidOperationException>(() => new ServiceCollection().Decorate<IGreeter, Loud>());
}

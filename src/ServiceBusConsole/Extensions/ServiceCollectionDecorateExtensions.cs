using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Extensions;

public static class ServiceCollectionDecorateExtensions
{
    /// <summary>
    /// Wraps the last <typeparamref name="TService"/> registration in <typeparamref name="TDecorator"/>, which receives it
    /// through its constructor. The original stays registered under a private key.
    /// </summary>
    public static IServiceCollection Decorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>(
        this IServiceCollection services)
        where TService : class
        where TDecorator : TService
    {
        var inner = services.LastOrDefault(d => d.ServiceType == typeof(TService) && !d.IsKeyedService)
            ?? throw new InvalidOperationException($"{typeof(TService).Name} is not registered, so it cannot be decorated.");
        var key = new object();
        services.Remove(inner);
        services.Add(inner switch
        {
            { ImplementationInstance: { } instance } => new ServiceDescriptor(typeof(TService), key, instance),
            { ImplementationFactory: { } factory } => new ServiceDescriptor(typeof(TService), key, (sp, _) => factory(sp), inner.Lifetime),
            { ImplementationType: { } type } => new ServiceDescriptor(typeof(TService), key, type, inner.Lifetime),
            _ => throw new InvalidOperationException($"{typeof(TService).Name}'s registration has no implementation."),
        });
        services.Add(new ServiceDescriptor(
            typeof(TService),
            sp => ActivatorUtilities.CreateInstance<TDecorator>(sp, sp.GetRequiredKeyedService<TService>(key)),
            inner.Lifetime));
        return services;
    }
}

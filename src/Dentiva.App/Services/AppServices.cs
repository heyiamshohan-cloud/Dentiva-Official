using Microsoft.Extensions.DependencyInjection;

namespace Dentiva.App.Services;

/// <summary>Static access point to the application's dependency container.</summary>
public static class AppServices
{
    private static IServiceProvider? _provider;

    public static bool IsInitialized => _provider is not null;

    public static void Initialize(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public static T Get<T>() where T : notnull =>
        _provider?.GetService<T>() ?? throw new InvalidOperationException($"{typeof(T).Name} is not registered.");

    public static T? GetService<T>() where T : class => _provider?.GetService<T>();
}

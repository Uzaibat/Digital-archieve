using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IDADRS.NativeSearch;

/// <summary>
/// Extension methods to register the native search service with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="NativeSearchService"/> as a singleton
    /// <see cref="INativeSearchService"/> implementation.
    /// </summary>
    /// <remarks>
    /// Registered as singleton because:
    ///   • The native library probe is done once at startup.
    ///   • All methods are stateless after the initial probe.
    ///   • P/Invoke calls are thread-safe in the C implementation.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// builder.Services.AddNativeSearch();
    /// </code>
    /// </example>
    public static IServiceCollection AddNativeSearch(
        this IServiceCollection services)
    {
        services.TryAddSingleton<INativeSearchService, NativeSearchService>();
        return services;
    }
}

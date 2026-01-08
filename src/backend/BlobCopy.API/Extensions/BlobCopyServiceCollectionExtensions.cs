using BlobCopy.API.Services;

namespace BlobCopy.API.Extensions;

/// <summary>
/// Extension methods for configuring blob copy business services in the DI container.
/// </summary>
public static class BlobCopyServiceCollectionExtensions
{
    /// <summary>
    /// Adds blob copy business services to the dependency injection container.
    /// Registers validation, copy operation, and progress notification services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBlobCopyServices(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register BlobClientFactory for creating blob clients (testable)
        services.AddSingleton<IBlobClientFactory, BlobClientFactory>();

        // Register BlobValidationService for URI and blob existence validation
        // Singleton: Stateless service, no per-request data
        services.AddSingleton<BlobValidationService>();

        // Register BlobCopyService for managing copy operations
        // Scoped: Each request/operation gets a new instance
        services.AddScoped<BlobCopyService>();

        // Register ProgressNotificationService for SignalR updates
        // Scoped: Each operation gets a new instance for isolated progress tracking
        services.AddScoped<ProgressNotificationService>();

        // Register TelemetryService for Application Insights integration
        // Singleton: Stateless service for tracking events and metrics
        services.AddSingleton<TelemetryService>();

        return services;
    }
}

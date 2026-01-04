using Microsoft.ApplicationInsights.Extensibility;

namespace BlobCopy.API.Extensions;

/// <summary>
/// Extension methods for configuring application observability and logging.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Adds Application Insights telemetry collection for the blob copy API.
    /// Enables tracing of requests, exceptions, dependencies, and custom events.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="instrumentationKey">Application Insights instrumentation key</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplicationInsights(
        this IServiceCollection services,
        string? instrumentationKey = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddApplicationInsightsTelemetry(options =>
        {
            if (!string.IsNullOrWhiteSpace(instrumentationKey))
            {
                options.InstrumentationKey = instrumentationKey;
            }
        });

        // Configure telemetry to include request/response bodies for debugging
        services.ConfigureTelemetryModule<RequestTrackingTelemetryModule>(
            (module, _) =>
            {
                module.IncludeHeaders = true;
            });

        return services;
    }

    /// <summary>
    /// Adds structured logging with correlation IDs for distributed tracing.
    /// </summary>
    /// <param name="builder">The logging builder</param>
    /// <returns>The logging builder for chaining</returns>
    public static ILoggingBuilder AddStructuredLogging(this ILoggingBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        builder
            .ClearProviders()
            .AddConsole()
            .AddDebug()
            .AddApplicationInsights();

        return builder;
    }
}

using BlobCopy.API.Hubs;

namespace BlobCopy.API.Extensions;

/// <summary>
/// Extension methods for configuring SignalR services in the DI container.
/// </summary>
public static class SignalRServiceCollectionExtensions
{
    /// <summary>
    /// Adds SignalR services with optimized configuration for blob copy operations.
    /// Sets up the BlobCopyHub with appropriate timeouts, message sizes, and JSON protocol options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBlobCopySignalR(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddSignalR(hubOptions =>
        {
            // Maximum size of a single message (32 KB - sufficient for progress updates)
            hubOptions.MaximumReceiveMessageSize = 32 * 1024;

            // Number of concurrent streams per connection
            hubOptions.StreamBufferCapacity = 10;

            // Timeout for client connection (30 seconds)
            // If no activity for 30s, connection is closed
            hubOptions.ClientTimeoutInterval = TimeSpan.FromSeconds(30);

            // Keep-alive interval (15 seconds)
            // Server sends keep-alive every 15s to prevent proxy timeouts
            hubOptions.KeepAliveInterval = TimeSpan.FromSeconds(15);

            // Maximum message buffer size per connection (100 KB)
            hubOptions.MaximumParallelInvocationsPerClient = 10;
        })
        .AddJsonProtocol(options =>
        {
            // Preserve property casing for JSON serialization
            // Matches C# property names to JSON without camelCase conversion
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });

        return services;
    }

    /// <summary>
    /// Configures CORS for SignalR WebSocket connections.
    /// Allows cross-origin requests from frontend applications.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="allowedOrigins">Optional list of allowed origins. If null, allows all origins.</param>
    public static WebApplication UseSignalRCors(
        this WebApplication app,
        params string[] allowedOrigins)
    {
        if (app == null)
            throw new ArgumentNullException(nameof(app));

        app.UseCors(policy =>
        {
            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                // Development: allow all origins
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }
            else
            {
                // Production: restrict to specific origins
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
        });

        return app;
    }

    /// <summary>
    /// Maps the BlobCopyHub to a SignalR endpoint.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="hubPattern">The URL pattern for the hub (default: /signalr/blob-copy-hub)</param>
    public static WebApplication MapBlobCopyHub(
        this WebApplication app,
        string hubPattern = "/signalr/blob-copy-hub")
    {
        if (app == null)
            throw new ArgumentNullException(nameof(app));

        if (string.IsNullOrWhiteSpace(hubPattern))
            throw new ArgumentException("Hub pattern cannot be null or empty", nameof(hubPattern));

        app.MapHub<BlobCopyHub>(hubPattern);

        return app;
    }
}

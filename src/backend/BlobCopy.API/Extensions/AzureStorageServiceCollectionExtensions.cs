using Azure.Identity;
using Azure.Storage.Blobs;
using BlobCopy.API.Hubs;

namespace BlobCopy.API.Extensions;

/// <summary>
/// Extension methods for configuring Azure Storage services in the DI container.
/// </summary>
public static class AzureStorageServiceCollectionExtensions
{
    /// <summary>
    /// Adds Azure Storage Blob client services to the dependency injection container.
    /// Configures BlobServiceClient with DefaultAzureCredential for managed identity support.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="storageAccountUri">URI of the Azure Storage account (e.g., https://myaccount.blob.core.windows.net)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureStorageServices(
        this IServiceCollection services,
        Uri storageAccountUri)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        
        if (storageAccountUri == null)
            throw new ArgumentNullException(nameof(storageAccountUri));

        // Register BlobServiceClient with DefaultAzureCredential
        // Supports: Managed Identity, Environment credentials, VS Code credentials, Azure CLI credentials, etc.
        services.AddSingleton(_ =>
            new BlobServiceClient(
                storageAccountUri,
                new DefaultAzureCredential()
            )
        );

        return services;
    }

    /// <summary>
    /// Adds Azure Storage Blob client services using a connection string.
    /// Useful for local development with Azure Storage Emulator or for testing.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">Azure Storage connection string</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAzureStorageServicesWithConnectionString(
        this IServiceCollection services,
        string connectionString)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        services.AddSingleton(_ =>
            new BlobServiceClient(connectionString)
        );

        return services;
    }
}

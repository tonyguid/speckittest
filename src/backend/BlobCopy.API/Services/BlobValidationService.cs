using Azure.Storage.Blobs;
using BlobCopy.API.Models;

namespace BlobCopy.API.Services;

/// <summary>
/// Service for validating blob URIs and permissions before copy operations.
/// Checks URI format, blob existence, and user permissions.
/// </summary>
public interface IBlobValidationService
{
    /// <summary>
    /// Validates both source and destination URIs and checks permissions.
    /// </summary>
    /// <param name="sourceUri">Source blob URI to validate</param>
    /// <param name="destinationUri">Destination blob URI to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of ValidationError if invalid, empty list if valid</returns>
    Task<List<ValidationError>> ValidateUrisAsync(
        string sourceUri,
        string destinationUri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates source blob exists and is readable.
    /// </summary>
    /// <param name="sourceUri">Source blob URI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ValidationError if invalid, null if valid</returns>
    Task<ValidationError?> ValidateSourceBlobAsync(
        string sourceUri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates destination container exists and is writable.
    /// </summary>
    /// <param name="destinationUri">Destination blob URI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ValidationError if invalid, null if valid</returns>
    Task<ValidationError?> ValidateDestinationAsync(
        string destinationUri,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the size in bytes of a blob at the given URI.
    /// </summary>
    /// <param name="blobUri">Blob URI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blob size in bytes, or -1 if blob doesn't exist</returns>
    Task<long> GetBlobSizeAsync(
        string blobUri,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of IBlobValidationService.
/// </summary>
public class BlobValidationService : IBlobValidationService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IBlobClientFactory _blobClientFactory;
    private readonly ILogger<BlobValidationService> _logger;

    // Azure Blob Storage constraints
    private const long MaxBlobSize = 190994931721216; // 190.7 TB
    private const long MinBlobSize = 1;

    /// <summary>
    /// Initializes a new instance of BlobValidationService.
    /// </summary>
    public BlobValidationService(
        BlobServiceClient blobServiceClient,
        IBlobClientFactory blobClientFactory,
        ILogger<BlobValidationService> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _blobClientFactory = blobClientFactory ?? throw new ArgumentNullException(nameof(blobClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ValidationError>> ValidateUrisAsync(
        string sourceUri,
        string destinationUri,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        // Validate inputs are not empty
        if (string.IsNullOrWhiteSpace(sourceUri))
        {
            errors.Add(new ValidationError
            {
                Field = "sourceUri",
                Message = "Source URI is required",
                Code = "EMPTY_FIELD"
            });
        }

        if (string.IsNullOrWhiteSpace(destinationUri))
        {
            errors.Add(new ValidationError
            {
                Field = "destinationUri",
                Message = "Destination URI is required",
                Code = "EMPTY_FIELD"
            });
        }

        if (errors.Count > 0)
            return errors;

        // Validate URI formats
        var sourceFormatError = ValidateUriFormat(sourceUri, "sourceUri");
        if (sourceFormatError != null)
            errors.Add(sourceFormatError);

        var destFormatError = ValidateUriFormat(destinationUri, "destinationUri");
        if (destFormatError != null)
            errors.Add(destFormatError);

        if (errors.Count > 0)
            return errors;

        // Check if URIs are identical
        if (sourceUri.Equals(destinationUri, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ValidationError
            {
                Field = "destinationUri",
                Message = "Source and destination URIs cannot be identical",
                Code = "IDENTICAL_URIS"
            });
            return errors;
        }

        // Validate permissions and existence
        var sourceError = await ValidateSourceBlobAsync(sourceUri, cancellationToken);
        if (sourceError != null)
            errors.Add(sourceError);

        var destError = await ValidateDestinationAsync(destinationUri, cancellationToken);
        if (destError != null)
            errors.Add(destError);

        return errors;
    }

    public async Task<ValidationError?> ValidateSourceBlobAsync(
        string sourceUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = _blobClientFactory.CreateBlobClient(sourceUri);
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);

            // Check blob size
            if (properties.Value.ContentLength > MaxBlobSize)
            {
                _logger.LogWarning("Source blob {SourceUri} exceeds maximum size", sourceUri);
                return new ValidationError
                {
                    Field = "sourceUri",
                    Message = $"Source blob exceeds maximum size ({MaxBlobSize} bytes)",
                    Code = "BLOB_TOO_LARGE"
                };
            }

            if (properties.Value.ContentLength < MinBlobSize)
            {
                _logger.LogWarning("Source blob {SourceUri} is empty", sourceUri);
                return new ValidationError
                {
                    Field = "sourceUri",
                    Message = "Source blob is empty or invalid",
                    Code = "BLOB_EMPTY"
                };
            }

            _logger.LogInformation(
                "Source blob {SourceUri} validated successfully. Size: {Size} bytes",
                sourceUri,
                properties.Value.ContentLength
            );

            return null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            _logger.LogWarning(ex, "Access denied to source blob {SourceUri}", sourceUri);
            return new ValidationError
            {
                Field = "sourceUri",
                Message = "Access denied to source blob. Check permissions.",
                Code = "AUTHENTICATION_FAILED"
            };
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Source blob {SourceUri} not found", sourceUri);
            return new ValidationError
            {
                Field = "sourceUri",
                Message = "Source blob not found",
                Code = "SOURCE_NOT_FOUND"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating source blob {SourceUri}", sourceUri);
            return new ValidationError
            {
                Field = "sourceUri",
                Message = "Error validating source blob",
                Code = "VALIDATION_ERROR"
            };
        }
    }

    public async Task<ValidationError?> ValidateDestinationAsync(
        string destinationUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse URI to get container name
            var uri = new Uri(destinationUri);
            var pathSegments = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathSegments.Length < 1)
            {
                return new ValidationError
                {
                    Field = "destinationUri",
                    Message = "Invalid destination URI format",
                    Code = "INVALID_URI_FORMAT"
                };
            }

            var containerName = pathSegments[0];

            // Validate container name
            var containerNameError = ValidateContainerName(containerName);
            if (containerNameError != null)
            {
                return containerNameError;
            }

            // Get or create container
            var container = _blobServiceClient.GetBlobContainerClient(containerName);

            try
            {
                // Check if container exists and we have permissions
                await container.GetPropertiesAsync(cancellationToken: cancellationToken);
                _logger.LogInformation("Destination container {ContainerName} validated successfully", containerName);
                return null;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
            {
                _logger.LogWarning(ex, "Access denied to destination container {ContainerName}", containerName);
                return new ValidationError
                {
                    Field = "destinationUri",
                    Message = "Access denied to destination container. Check permissions.",
                    Code = "AUTHENTICATION_FAILED"
                };
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Destination container {ContainerName} not found", containerName);
                return new ValidationError
                {
                    Field = "destinationUri",
                    Message = "Destination container not found",
                    Code = "DESTINATION_INACCESSIBLE"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating destination {DestinationUri}", destinationUri);
            return new ValidationError
            {
                Field = "destinationUri",
                Message = "Error validating destination",
                Code = "VALIDATION_ERROR"
            };
        }
    }

    public async Task<long> GetBlobSizeAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = new BlobClient(new Uri(blobUri));
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blob size for {BlobUri}", blobUri);
            return -1;
        }
    }

    /// <summary>
    /// Validates URI format per Azure Blob Storage requirements.
    /// </summary>
    private ValidationError? ValidateUriFormat(string uri, string fieldName)
    {
        // Check basic HTTPS requirement
        if (!uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationError
            {
                Field = fieldName,
                Message = "URI must use HTTPS protocol",
                Code = "INVALID_URI_FORMAT"
            };
        }

        // Check for valid Azure Blob Storage domain
        if (!uri.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase) &&
            !uri.Contains(".blob.storage.azure.us", StringComparison.OrdinalIgnoreCase) &&
            !uri.Contains(".blob.core.chinacloudapi.cn", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationError
            {
                Field = fieldName,
                Message = "URI is not a valid Azure Blob Storage URI",
                Code = "INVALID_URI_FORMAT"
            };
        }

        // Try to parse as URI
        try
        {
            new Uri(uri);
        }
        catch (UriFormatException)
        {
            return new ValidationError
            {
                Field = fieldName,
                Message = "URI format is invalid",
                Code = "INVALID_URI_FORMAT"
            };
        }

        return null;
    }

    /// <summary>
    /// Validates container name per Azure Blob Storage requirements.
    /// </summary>
    private ValidationError? ValidateContainerName(string containerName)
    {
        // Azure container naming rules:
        // - 3-63 characters
        // - Lowercase letters, digits, hyphens
        // - Cannot start or end with hyphen
        // - Cannot have consecutive hyphens

        if (containerName.Length < 3 || containerName.Length > 63)
        {
            return new ValidationError
            {
                Field = "destinationUri",
                Message = "Container name must be 3-63 characters",
                Code = "INVALID_CONTAINER_NAME"
            };
        }

        if (!containerName.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-'))
        {
            return new ValidationError
            {
                Field = "destinationUri",
                Message = "Container name must contain only lowercase letters, digits, and hyphens",
                Code = "INVALID_CONTAINER_NAME"
            };
        }

        if (containerName.StartsWith('-') || containerName.EndsWith('-'))
        {
            return new ValidationError
            {
                Field = "destinationUri",
                Message = "Container name cannot start or end with hyphen",
                Code = "INVALID_CONTAINER_NAME"
            };
        }

        if (containerName.Contains("--"))
        {
            return new ValidationError
            {
                Field = "destinationUri",
                Message = "Container name cannot contain consecutive hyphens",
                Code = "INVALID_CONTAINER_NAME"
            };
        }

        return null;
    }
}

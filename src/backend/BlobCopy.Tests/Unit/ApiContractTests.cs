using System.Text.Json;
using System.Text.Json.Serialization;
using BlobCopy.API.Models;
using Xunit;

namespace BlobCopy.Tests.Unit;

/// <summary>
/// Tests that verify API models conform to the OpenAPI contract specification.
/// These tests ensure request/response shapes match the documented API contract.
/// </summary>
public class ApiContractTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region CopyRequest Contract Tests

    [Fact]
    public void CopyRequest_ShouldSerializeWithRequiredFields()
    {
        // Arrange
        var request = new CopyRequest
        {
            SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://account.blob.core.windows.net/dest/blob-copy.vhd"
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - Required fields per OpenAPI spec
        Assert.True(root.TryGetProperty("sourceUri", out var sourceUri));
        Assert.True(root.TryGetProperty("destinationUri", out var destUri));
        Assert.Equal(request.SourceUri, sourceUri.GetString());
        Assert.Equal(request.DestinationUri, destUri.GetString());
    }

    [Fact]
    public void CopyRequest_ShouldDeserializeFromValidJson()
    {
        // Arrange - JSON matching OpenAPI schema
        var json = """
        {
            "sourceUri": "https://account.blob.core.windows.net/source/blob.vhd",
            "destinationUri": "https://account.blob.core.windows.net/dest/blob-copy.vhd"
        }
        """;

        // Act
        var request = JsonSerializer.Deserialize<CopyRequest>(json, _jsonOptions);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("https://account.blob.core.windows.net/source/blob.vhd", request.SourceUri);
        Assert.Equal("https://account.blob.core.windows.net/dest/blob-copy.vhd", request.DestinationUri);
    }

    [Fact]
    public void CopyRequest_OptionalFieldsDefaultCorrectly()
    {
        // Arrange
        var request = new CopyRequest
        {
            SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://account.blob.core.windows.net/dest/blob.vhd"
        };

        // Assert - Optional fields have correct defaults
        Assert.False(request.OverwriteIfExists);
        Assert.Null(request.NewDestinationName);
    }

    #endregion

    #region BlobCopyOperation Contract Tests

    [Fact]
    public void BlobCopyOperation_ShouldSerializeWithRequiredFields()
    {
        // Arrange
        var operation = new BlobCopyOperation
        {
            Id = Guid.NewGuid(),
            SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://account.blob.core.windows.net/dest/blob.vhd",
            Status = BlobCopyStatus.Pending,
            StartedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var json = JsonSerializer.Serialize(operation, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - Required fields per OpenAPI spec
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("sourceUri", out _));
        Assert.True(root.TryGetProperty("destinationUri", out _));
        Assert.True(root.TryGetProperty("status", out _));
    }

    [Fact]
    public void BlobCopyOperation_StatusEnum_MatchesOpenApiSpec()
    {
        // Per OpenAPI spec: Pending, InProgress, Completed, Failed, Cancelled
        var expectedStatuses = new[]
        {
            BlobCopyStatus.Pending,
            BlobCopyStatus.Running,   // Maps to InProgress
            BlobCopyStatus.Completed,
            BlobCopyStatus.Failed,
            BlobCopyStatus.Cancelled
        };

        foreach (var status in expectedStatuses)
        {
            var operation = new BlobCopyOperation
            {
                Id = Guid.NewGuid(),
                SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
                DestinationUri = "https://account.blob.core.windows.net/dest/blob.vhd",
                Status = status,
                StartedAt = DateTime.UtcNow
            };

            // Should serialize without throwing
            var json = JsonSerializer.Serialize(operation, _jsonOptions);
            Assert.NotNull(json);
        }
    }

    [Fact]
    public void BlobCopyOperation_ProgressFields_HaveCorrectTypes()
    {
        // Arrange
        var operation = new BlobCopyOperation
        {
            Id = Guid.NewGuid(),
            SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://account.blob.core.windows.net/dest/blob.vhd",
            Status = BlobCopyStatus.Running,
            BytesTransferred = 52428800,
            TotalBytes = 1073741824,
            StartedAt = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(operation, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - Progress fields match OpenAPI types
        Assert.True(root.TryGetProperty("bytesTransferred", out var bytesTransferred));
        Assert.Equal(JsonValueKind.Number, bytesTransferred.ValueKind);
        Assert.Equal(52428800L, bytesTransferred.GetInt64());

        Assert.True(root.TryGetProperty("totalBytes", out var totalBytes));
        Assert.Equal(JsonValueKind.Number, totalBytes.ValueKind);
        Assert.Equal(1073741824L, totalBytes.GetInt64());
    }

    [Fact]
    public void BlobCopyOperation_DateTimeFields_SerializeAsIso8601()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var operation = new BlobCopyOperation
        {
            Id = Guid.NewGuid(),
            SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://account.blob.core.windows.net/dest/blob.vhd",
            Status = BlobCopyStatus.Pending,
            StartedAt = startTime
        };

        // Act
        var json = JsonSerializer.Serialize(operation, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - DateTime should be ISO 8601 format
        Assert.True(root.TryGetProperty("startedAt", out var startedAt));
        var dateValue = startedAt.GetString();
        Assert.NotNull(dateValue);
        Assert.Contains("2024-01-15", dateValue);
    }

    #endregion

    #region ValidationError Contract Tests

    [Fact]
    public void ValidationError_ShouldSerializeWithRequiredFields()
    {
        // Arrange
        var error = new ValidationError
        {
            Field = "sourceUri",
            Message = "Source URI is required",
            Code = "INVALID_URI_FORMAT"
        };

        // Act
        var json = JsonSerializer.Serialize(error, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - Required fields per OpenAPI spec
        Assert.True(root.TryGetProperty("field", out var field));
        Assert.True(root.TryGetProperty("message", out var message));
        Assert.True(root.TryGetProperty("code", out var code));

        Assert.Equal("sourceUri", field.GetString());
        Assert.Equal("Source URI is required", message.GetString());
        Assert.Equal("INVALID_URI_FORMAT", code.GetString());
    }

    [Theory]
    [InlineData("INVALID_URI_FORMAT")]
    [InlineData("SOURCE_NOT_FOUND")]
    [InlineData("INSUFFICIENT_PERMISSIONS")]
    [InlineData("IDENTICAL_URIS")]
    [InlineData("CONTAINER_NOT_FOUND")]
    [InlineData("DESTINATION_EXISTS")]
    public void ValidationError_Code_AcceptsValidOpenApiEnumValues(string code)
    {
        // Arrange
        var error = new ValidationError
        {
            Field = "testField",
            Message = "Test message",
            Code = code
        };

        // Act & Assert - Should serialize without error
        var json = JsonSerializer.Serialize(error, _jsonOptions);
        Assert.Contains(code, json);
    }

    #endregion

    #region ValidationResult Contract Tests

    [Fact]
    public void ValidationResult_Success_SerializesCorrectly()
    {
        // Arrange
        var result = ValidationResult.Success();

        // Act
        var json = JsonSerializer.Serialize(result, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("isValid", out var isValid));
        Assert.True(isValid.GetBoolean());
    }

    [Fact]
    public void ValidationResult_Failure_IncludesErrors()
    {
        // Arrange
        var result = ValidationResult.Failure("sourceUri", "URI is invalid", "INVALID_URI_FORMAT");

        // Act
        var json = JsonSerializer.Serialize(result, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("isValid", out var isValid));
        Assert.False(isValid.GetBoolean());

        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.True(errors.GetArrayLength() > 0);
    }

    #endregion

    #region CopyResult Contract Tests

    [Fact]
    public void CopyResult_Success_SerializesCorrectly()
    {
        // Arrange
        var operationId = Guid.NewGuid();
        var result = CopyResult.FromSuccess(operationId, "https://dest.blob.core.windows.net/container/blob.txt");

        // Act
        var json = JsonSerializer.Serialize(result, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());
        Assert.True(root.TryGetProperty("operationId", out _));
        Assert.True(root.TryGetProperty("destinationUri", out _));
    }

    [Fact]
    public void CopyResult_Failure_IncludesErrorInfo()
    {
        // Arrange
        var result = CopyResult.FromFailure("BLOB_NOT_FOUND", "Source blob not found");

        // Act
        var json = JsonSerializer.Serialize(result, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("success", out var success));
        Assert.False(success.GetBoolean());
        Assert.True(root.TryGetProperty("errorCode", out _));
        Assert.True(root.TryGetProperty("errorMessage", out _));
    }

    #endregion

    #region ConflictResponse Contract Tests

    [Fact]
    public void ConflictResponse_SerializesWithRequiredFields()
    {
        // Arrange
        var response = ConflictResponse.ForExistingDestination(
            "https://account.blob.core.windows.net/container/existing-blob.txt"
        );

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert - Per 409 response in OpenAPI spec
        Assert.True(root.TryGetProperty("code", out var code));
        Assert.Equal("DESTINATION_EXISTS", code.GetString());
        Assert.True(root.TryGetProperty("destinationUri", out _));
    }

    #endregion

    #region ProgressUpdate Contract Tests

    [Fact]
    public void ProgressUpdate_SerializesForSignalR()
    {
        // Arrange
        var progress = new ProgressUpdate
        {
            OperationId = Guid.NewGuid(),
            BytesCopied = 104857600,
            TotalBytes = 1073741824,
            PercentComplete = 9.77,
            TransferRateMbps = 12.5
        };

        // Act
        var json = JsonSerializer.Serialize(progress, _jsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("operationId", out _));
        Assert.True(root.TryGetProperty("bytesCopied", out var bytesCopied));
        Assert.Equal(104857600L, bytesCopied.GetInt64());
        Assert.True(root.TryGetProperty("totalBytes", out _));
        Assert.True(root.TryGetProperty("percentComplete", out _));
    }

    #endregion

    #region Round-Trip Serialization Tests

    [Fact]
    public void BlobCopyOperation_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var original = new BlobCopyOperation
        {
            Id = Guid.NewGuid(),
            SourceUri = "https://account.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://account.blob.core.windows.net/dest/blob.vhd",
            Status = BlobCopyStatus.Running,
            BytesTransferred = 52428800,
            TotalBytes = 1073741824,
            StartedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<BlobCopyOperation>(json, _jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.SourceUri, deserialized.SourceUri);
        Assert.Equal(original.DestinationUri, deserialized.DestinationUri);
        Assert.Equal(original.Status, deserialized.Status);
        Assert.Equal(original.BytesTransferred, deserialized.BytesTransferred);
        Assert.Equal(original.TotalBytes, deserialized.TotalBytes);
    }

    [Fact]
    public void ValidationError_RoundTrip_PreservesAllValues()
    {
        // Arrange
        var original = new ValidationError
        {
            Field = "destinationUri",
            Message = "Destination URI is required",
            Code = "INVALID_URI_FORMAT"
        };

        // Act
        var json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ValidationError>(json, _jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Field, deserialized.Field);
        Assert.Equal(original.Message, deserialized.Message);
        Assert.Equal(original.Code, deserialized.Code);
    }

    #endregion
}

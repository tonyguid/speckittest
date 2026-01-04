using Xunit;
using BlobCopy.API.Models;

namespace BlobCopy.Tests.Models;

/// <summary>
/// Unit tests for ErrorResponse model.
/// </summary>
public class ErrorResponseTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Arrange & Act
        var response = new ErrorResponse();

        // Assert
        Assert.Equal(0, response.Status);
        Assert.Equal(string.Empty, response.Code);
        Assert.Equal(string.Empty, response.Message);
        Assert.Equal(string.Empty, response.CorrelationId);
        Assert.Null(response.ValidationErrors);
    }

    [Fact]
    public void CanRepresentBadRequestError()
    {
        // Arrange & Act
        var response = new ErrorResponse
        {
            Status = 400,
            Code = "VALIDATION_FAILED",
            Message = "Request validation failed",
            CorrelationId = "abc-123-def"
        };

        // Assert
        Assert.Equal(400, response.Status);
        Assert.Equal("VALIDATION_FAILED", response.Code);
    }

    [Fact]
    public void CanIncludeValidationErrors()
    {
        // Arrange
        var response = new ErrorResponse
        {
            Status = 400,
            Code = "INVALID_REQUEST",
            Message = "Request contains validation errors",
            ValidationErrors = new List<ValidationError>
            {
                new ValidationError
                {
                    Field = "sourceUri",
                    Message = "Invalid format",
                    Code = "INVALID_URI_FORMAT"
                },
                new ValidationError
                {
                    Field = "destinationUri",
                    Message = "Not found",
                    Code = "DESTINATION_INACCESSIBLE"
                }
            }
        };

        // Assert
        Assert.NotNull(response.ValidationErrors);
        Assert.Equal(2, response.ValidationErrors.Count);
    }

    [Fact]
    public void CanRepresentUnauthorizedError()
    {
        // Arrange & Act
        var response = new ErrorResponse
        {
            Status = 401,
            Code = "UNAUTHORIZED",
            Message = "Authentication failed"
        };

        // Assert
        Assert.Equal(401, response.Status);
        Assert.Equal("UNAUTHORIZED", response.Code);
    }

    [Fact]
    public void CanRepresentNotFoundError()
    {
        // Arrange & Act
        var response = new ErrorResponse
        {
            Status = 404,
            Code = "NOT_FOUND",
            Message = "Copy operation not found"
        };

        // Assert
        Assert.Equal(404, response.Status);
        Assert.Equal("NOT_FOUND", response.Code);
    }

    [Fact]
    public void CanRepresentServerError()
    {
        // Arrange & Act
        var response = new ErrorResponse
        {
            Status = 500,
            Code = "INTERNAL_ERROR",
            Message = "An unexpected error occurred"
        };

        // Assert
        Assert.Equal(500, response.Status);
        Assert.Equal("INTERNAL_ERROR", response.Code);
    }

    [Fact]
    public void Timestamp_DefaultsToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var response = new ErrorResponse();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.InRange(response.Timestamp, beforeCreation, afterCreation.AddSeconds(1));
    }
}

using BlobCopy.API.Controllers;
using BlobCopy.API.Models;
using BlobCopy.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace BlobCopy.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for the /validate endpoint.
/// Tests validation flow with various URI formats and blob states.
/// </summary>
public class ValidateEndpointIntegrationTests
{
    private readonly Mock<IBlobValidationService> _mockValidationService;
    private readonly Mock<IBlobCopyService> _mockCopyService;
    private readonly Mock<IProgressNotificationService> _mockProgressService;
    private readonly Mock<ILogger<BlobCopyController>> _mockLogger;
    private readonly BlobCopyController _controller;

    public ValidateEndpointIntegrationTests()
    {
        _mockValidationService = new Mock<IBlobValidationService>();
        _mockCopyService = new Mock<IBlobCopyService>();
        _mockProgressService = new Mock<IProgressNotificationService>();
        _mockLogger = new Mock<ILogger<BlobCopyController>>();

        _controller = new BlobCopyController(
            _mockValidationService.Object,
            _mockCopyService.Object,
            _mockProgressService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Validate_WithValidAzureBlobs_ReturnsEmptyErrorList()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/myblob.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/destination/myblob.vhd"
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());

        var result = await _controller.ValidateAsync(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var errors = Assert.IsType<List<ValidationError>>(okResult.Value);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Validate_WithNonExistentSourceBlob_ReturnsSourceNotFoundError()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/nonexistent.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/destination/copy.vhd"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError
            {
                Field = "sourceUri",
                Message = "Source blob not found",
                Code = "SOURCE_NOT_FOUND"
            }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.ValidateAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var returnedErrors = Assert.IsType<List<ValidationError>>(badRequestResult.Value);
        Assert.Single(returnedErrors);
        Assert.Equal("SOURCE_NOT_FOUND", returnedErrors[0].Code);
    }

    [Fact]
    public async Task Validate_WithIdenticalSourceAndDestination_ReturnsIdenticalUrisError()
    {
        var uri = "https://storage.blob.core.windows.net/container/blob.vhd";
        var request = new CopyRequest
        {
            SourceUri = uri,
            DestinationUri = uri
        };

        var errors = new List<ValidationError>
        {
            new ValidationError
            {
                Field = "destinationUri",
                Message = "Source and destination URIs cannot be identical",
                Code = "IDENTICAL_URIS"
            }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(uri, uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.ValidateAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var returnedErrors = Assert.IsType<List<ValidationError>>(badRequestResult.Value);
        Assert.Single(returnedErrors);
        Assert.Equal("IDENTICAL_URIS", returnedErrors[0].Code);
    }

    [Fact]
    public async Task Validate_WithMultipleValidationErrors_ReturnsAllErrors()
    {
        var request = new CopyRequest
        {
            SourceUri = "invalid-uri",
            DestinationUri = "also-invalid"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError { Field = "sourceUri", Message = "Invalid format", Code = "INVALID_URI_FORMAT" },
            new ValidationError { Field = "destinationUri", Message = "Invalid format", Code = "INVALID_URI_FORMAT" }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.ValidateAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var returnedErrors = Assert.IsType<List<ValidationError>>(badRequestResult.Value);
        Assert.Equal(2, returnedErrors.Count);
    }

    [Fact]
    public async Task Validate_WithAccessDeniedToBlob_ReturnsAuthenticationFailedError()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/restricted/blob.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/destination/copy.vhd"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError
            {
                Field = "sourceUri",
                Message = "Access denied to source blob. Check permissions.",
                Code = "AUTHENTICATION_FAILED"
            }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.ValidateAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var returnedErrors = Assert.IsType<List<ValidationError>>(badRequestResult.Value);
        Assert.Single(returnedErrors);
        Assert.Equal("AUTHENTICATION_FAILED", returnedErrors[0].Code);
    }

    [Fact]
    public async Task Validate_WithInvalidDestinationContainer_ReturnsDestinationError()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/nonexistent/copy.vhd"
        };

        var errors = new List<ValidationError>
        {
            new ValidationError
            {
                Field = "destinationUri",
                Message = "Destination container not found",
                Code = "DESTINATION_INACCESSIBLE"
            }
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(errors);

        var result = await _controller.ValidateAsync(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var returnedErrors = Assert.IsType<List<ValidationError>>(badRequestResult.Value);
        Assert.Single(returnedErrors);
        Assert.Equal("DESTINATION_INACCESSIBLE", returnedErrors[0].Code);
    }

    [Theory]
    [InlineData("https://storage.blob.storage.azure.us/container/blob")]
    [InlineData("https://storage.blob.core.chinacloudapi.cn/container/blob")]
    public async Task Validate_WithAlternateAzureClouds_Succeeds(string uri)
    {
        var request = new CopyRequest
        {
            SourceUri = uri,
            DestinationUri = "https://storage.blob.core.windows.net/dest/blob"
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());

        var result = await _controller.ValidateAsync(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Validate_RespectsSecurityContextAndDoesNotStartCopy()
    {
        var request = new CopyRequest
        {
            SourceUri = "https://storage.blob.core.windows.net/source/blob.vhd",
            DestinationUri = "https://storage.blob.core.windows.net/destination/blob.vhd"
        };

        _mockValidationService
            .Setup(x => x.ValidateUrisAsync(
                request.SourceUri,
                request.DestinationUri,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ValidationError>());

        await _controller.ValidateAsync(request);

        // Verify that copy service was never called
        _mockCopyService.Verify(
            x => x.StartCopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

using Xunit;
using Moq;
using BlobCopy.API.Middleware;

namespace BlobCopy.Tests.Middleware;

/// <summary>
/// Unit tests for RequestLoggingMiddleware.
/// </summary>
public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_IfNotProvided()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new RequestLoggingMiddleware(requestDelegate.Object, mockLogger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.NotNull(context.Items["CorrelationId"]);
        Assert.NotEmpty((string)context.Items["CorrelationId"]);
    }

    [Fact]
    public async Task InvokeAsync_PreservesCorrelationId_IfProvided()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new RequestLoggingMiddleware(requestDelegate.Object, mockLogger.Object);

        var correlationId = "test-correlation-id-123";
        var context = new DefaultHttpContext();
        context.Request.Headers.Add("x-correlation-id", correlationId);
        context.Request.Method = "POST";
        context.Request.Path = "/api/copy";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(correlationId, context.Items["CorrelationId"]);
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeaders()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new RequestLoggingMiddleware(requestDelegate.Object, mockLogger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/health";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.TryGetValue("x-correlation-id", out var headerValue));
        Assert.NotEmpty(headerValue.ToString());
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestInformation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new RequestLoggingMiddleware(requestDelegate.Object, mockLogger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/status";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("HTTP Request:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task InvokeAsync_LogsResponseInformation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var requestDelegate = new Mock<RequestDelegate>();
        var middleware = new RequestLoggingMiddleware(requestDelegate.Object, mockLogger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Response.StatusCode = 200;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("HTTP Response:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task InvokeAsync_LogsExceptionWhenNextThrows()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var exception = new InvalidOperationException("Test error");
        var requestDelegate = new Mock<RequestDelegate>();
        requestDelegate.Setup(rd => rd(It.IsAny<HttpContext>())).ThrowsAsync(exception);

        var middleware = new RequestLoggingMiddleware(requestDelegate.Object, mockLogger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/copy";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("HTTP Request failed:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void Constructor_WithNullDelegate_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RequestLoggingMiddleware>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RequestLoggingMiddleware(null!, mockLogger.Object)
        );
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var requestDelegate = new Mock<RequestDelegate>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RequestLoggingMiddleware(requestDelegate.Object, null!)
        );
    }
}

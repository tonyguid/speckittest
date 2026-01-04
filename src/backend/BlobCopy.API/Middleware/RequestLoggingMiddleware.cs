namespace BlobCopy.API.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses with correlation IDs.
/// Enables distributed tracing across the application stack.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the RequestLoggingMiddleware.
    /// </summary>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes an HTTP request and logs request/response details.
    /// Generates and propagates correlation IDs for distributed tracing.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or retrieve correlation ID
        string correlationId = context.Request.Headers.TryGetValue("x-correlation-id", out var existingId)
            ? existingId.ToString()
            : Guid.NewGuid().ToString();

        // Add correlation ID to context for downstream use
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Add("x-correlation-id", correlationId);

        // Log request
        _logger.LogInformation(
            "HTTP Request: {Method} {Path} from {RemoteIP}. CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress,
            correlationId
        );

        // Time the request
        var startTime = DateTime.UtcNow;

        try
        {
            await _next(context);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "HTTP Response: {StatusCode} {Method} {Path} completed in {DurationMs}ms. CorrelationId: {CorrelationId}",
                context.Response.StatusCode,
                context.Request.Method,
                context.Request.Path,
                duration.TotalMilliseconds,
                correlationId
            );
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(
                ex,
                "HTTP Request failed: {Method} {Path} after {DurationMs}ms. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                duration.TotalMilliseconds,
                correlationId
            );

            throw;
        }
    }
}

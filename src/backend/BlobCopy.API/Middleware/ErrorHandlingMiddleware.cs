using System.Net;
using System.Text.Json;
using BlobCopy.API.Models;

namespace BlobCopy.API.Middleware;

/// <summary>
/// Global error handling middleware that catches unhandled exceptions
/// and returns standardized error responses.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ArgumentException argEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                errorResponse.Code = "INVALID_ARGUMENT";
                errorResponse.Message = argEx.Message;
                break;

            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                errorResponse.Code = "UNAUTHORIZED";
                errorResponse.Message = "Access denied. Please check your credentials.";
                break;

            case OperationCanceledException:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                errorResponse.Code = "OPERATION_CANCELLED";
                errorResponse.Message = "The operation was cancelled.";
                break;

            case Azure.RequestFailedException azureEx:
                response.StatusCode = azureEx.Status;
                errorResponse.Code = MapAzureErrorCode(azureEx.ErrorCode);
                errorResponse.Message = GetUserFriendlyAzureMessage(azureEx);
                break;

            case TimeoutException:
                response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
                errorResponse.Code = "TIMEOUT";
                errorResponse.Message = "The operation timed out. Please try again.";
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                errorResponse.Code = "INTERNAL_ERROR";
                errorResponse.Message = "An unexpected error occurred. Please try again later.";
                break;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
    }

    private static string MapAzureErrorCode(string? errorCode)
    {
        return errorCode switch
        {
            "BlobNotFound" => "SOURCE_NOT_FOUND",
            "ContainerNotFound" => "CONTAINER_NOT_FOUND",
            "AuthenticationFailed" => "AUTH_FAILED",
            "AuthorizationFailure" => "AUTH_FAILED",
            "InvalidUri" => "INVALID_URI",
            _ => "AZURE_ERROR"
        };
    }

    private static string GetUserFriendlyAzureMessage(Azure.RequestFailedException ex)
    {
        return ex.ErrorCode switch
        {
            "BlobNotFound" => "The source blob was not found. Please check the URI and try again.",
            "ContainerNotFound" => "The container was not found. Please verify the URI.",
            "AuthenticationFailed" => "Authentication failed. Please check your credentials.",
            "AuthorizationFailure" => "You don't have permission to access this resource.",
            "InvalidUri" => "The provided URI is invalid.",
            _ => $"Azure Storage error: {ex.Message}"
        };
    }
}

/// <summary>
/// Extension methods to register the error handling middleware.
/// </summary>
public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}

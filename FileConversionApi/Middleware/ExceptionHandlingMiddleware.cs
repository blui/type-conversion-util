using System.Net;
using System.Text.Json;
using FileConversionApi.Utilities;

namespace FileConversionApi.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Catches unhandled exceptions and returns structured error responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
        var (statusCode, message) = GetErrorDetails(exception);
        var errorId = GenerateErrorId();

        _logger.LogError(exception,
            "Unhandled exception - ErrorId: {ErrorId}, StatusCode: {StatusCode}, Path: {Path}, Method: {Method}, IP: {RemoteIp}",
            errorId,
            statusCode,
            context.Request.Path,
            context.Request.Method,
            context.Connection.RemoteIpAddress);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            error = message,
            errorId,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static (int statusCode, string message) GetErrorDetails(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => ((int)HttpStatusCode.Forbidden, "Access denied"),
            ArgumentException or InvalidDataException => ((int)HttpStatusCode.BadRequest, "Invalid input"),
            FileNotFoundException or DirectoryNotFoundException => ((int)HttpStatusCode.NotFound, "Resource not found"),
            TimeoutException => ((int)HttpStatusCode.RequestTimeout, "Operation timed out"),
            OutOfMemoryException => ((int)HttpStatusCode.InsufficientStorage, "Resource limit exceeded"),
            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };
    }

    private static string GenerateErrorId()
    {
        return $"ERR-{UniqueIdGenerator.GenerateId()}";
    }
}

/// <summary>
/// Extension methods for exception handling middleware.
/// </summary>
public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

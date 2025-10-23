using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace FileConversionApi.Middleware;

/// <summary>
/// Comprehensive exception handling middleware
/// Provides structured error logging, categorization, and fault tolerance
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    // Error severity levels
    public enum ErrorLevel
    {
        CRITICAL, // System failure, data loss, security breach
        WARNING,  // Degraded functionality, recoverable errors
        INFO,     // Normal operation, informational messages
        DEBUG     // Development debugging information
    }

    // Error categories for classification and monitoring
    public enum ErrorCategory
    {
        SYSTEM,       // OS, filesystem, network issues
        CONVERSION,   // Document conversion failures
        SECURITY,     // Authentication, authorization failures
        VALIDATION,   // Input validation errors
        DEPENDENCY,   // Missing libraries, services
        PERFORMANCE,  // Resource limits, timeouts
        CONFIGURATION // Configuration errors
    }

    // Error metrics for monitoring
    private static readonly ErrorMetrics _metrics = new();

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var errorResponse = CreateErrorResponse(context, exception);

        // Log the error with detailed telemetry
        LogError(exception, context, errorResponse.Category, errorResponse.Level, new
        {
            RequestId = context.TraceIdentifier,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            RemoteIP = context.Connection.RemoteIpAddress?.ToString(),
            RequestPath = context.Request.Path.ToString(),
            RequestMethod = context.Request.Method,
            QueryString = context.Request.QueryString.ToString()
        });

        // Set response
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = errorResponse.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(new
        {
            error = errorResponse.Message,
            errorId = errorResponse.ErrorId,
            timestamp = errorResponse.Timestamp,
            category = errorResponse.Category.ToString(),
            level = errorResponse.Level.ToString()
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private ErrorResponse CreateErrorResponse(HttpContext context, Exception exception)
    {
        var (category, level, statusCode, userMessage) = CategorizeException(exception, context);

        return new ErrorResponse
        {
            ErrorId = GenerateErrorId(),
            Timestamp = DateTime.UtcNow,
            Message = userMessage,
            Category = category,
            Level = level,
            StatusCode = statusCode,
            Exception = exception
        };
    }

    private (ErrorCategory category, ErrorLevel level, int statusCode, string userMessage) CategorizeException(Exception exception, HttpContext context)
    {
        // Check for specific exception types and HTTP context
        if (exception is UnauthorizedAccessException || exception is InvalidOperationException && exception.Message.Contains("security", StringComparison.OrdinalIgnoreCase))
        {
            return (ErrorCategory.SECURITY, ErrorLevel.WARNING, (int)HttpStatusCode.Forbidden, "Access denied");
        }

        if (exception is ArgumentException || exception is InvalidDataException)
        {
            return (ErrorCategory.VALIDATION, ErrorLevel.INFO, (int)HttpStatusCode.BadRequest, "Invalid input data");
        }

        if (exception is TimeoutException || exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return (ErrorCategory.PERFORMANCE, ErrorLevel.WARNING, (int)HttpStatusCode.RequestTimeout, "Operation timed out");
        }

        if (exception.Message.Contains("LibreOffice", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("conversion", StringComparison.OrdinalIgnoreCase))
        {
            return (ErrorCategory.CONVERSION, ErrorLevel.WARNING, (int)HttpStatusCode.InternalServerError, "Document conversion failed");
        }

        if (exception is FileNotFoundException || exception is DirectoryNotFoundException)
        {
            return (ErrorCategory.SYSTEM, ErrorLevel.WARNING, (int)HttpStatusCode.NotFound, "Resource not found");
        }

        if (exception is OutOfMemoryException || exception.Message.Contains("memory", StringComparison.OrdinalIgnoreCase))
        {
            return (ErrorCategory.PERFORMANCE, ErrorLevel.CRITICAL, (int)HttpStatusCode.InsufficientStorage, "System resource limit exceeded");
        }

        // Default categorization
        return (ErrorCategory.SYSTEM, ErrorLevel.WARNING, (int)HttpStatusCode.InternalServerError, "An unexpected error occurred");
    }

    /// <summary>
    /// Log error information with detailed telemetry
    /// </summary>
    public static void LogError(
        Exception error,
        HttpContext? req = null,
        ErrorCategory category = ErrorCategory.SYSTEM,
        ErrorLevel level = ErrorLevel.WARNING,
        object? context = null)
    {
        var timestamp = DateTime.UtcNow;
        var errorId = GenerateErrorId();

        // Update error metrics (thread-safe)
        lock (_metrics)
        {
            _metrics.TotalErrors++;
            _metrics.ErrorsByCategory[category] = _metrics.ErrorsByCategory.GetValueOrDefault(category) + 1;
            _metrics.ErrorsByLevel[level] = _metrics.ErrorsByLevel.GetValueOrDefault(level) + 1;

            // Keep recent errors for monitoring
            _metrics.RecentErrors.Insert(0, new ErrorSummary
            {
                Id = errorId,
                Timestamp = timestamp,
                Category = category,
                Level = level,
                Message = error.Message
            });

            if (_metrics.RecentErrors.Count > Constants.ErrorTracking.MaxRecentErrors)
            {
                _metrics.RecentErrors.RemoveAt(_metrics.RecentErrors.Count - 1);
            }
        }

        // Create structured log entry
        var logEntry = new
        {
            ErrorId = errorId,
            Timestamp = timestamp,
            Level = level.ToString(),
            Category = category.ToString(),
            Error = new
            {
                Message = error.Message,
                Type = error.GetType().Name,
                StackTrace = error.StackTrace,
                Source = error.Source
            },
            Request = req != null ? new
            {
                TraceIdentifier = req.TraceIdentifier,
                Path = req.Request.Path.ToString(),
                Method = req.Request.Method,
                QueryString = req.Request.QueryString.ToString(),
                UserAgent = req.Request.Headers.UserAgent.ToString(),
                RemoteIpAddress = req.Connection.RemoteIpAddress?.ToString()
            } : null,
            Context = context,
            SystemInfo = new
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                FrameworkVersion = Environment.Version.ToString(),
                WorkingSet = Environment.WorkingSet,
                ProcessorCount = Environment.ProcessorCount
            }
        };

        // Log based on severity level
        var logger = req?.RequestServices.GetService(typeof(ILogger<ExceptionHandlingMiddleware>)) as ILogger<ExceptionHandlingMiddleware>;
        if (logger != null)
        {
            switch (level)
            {
                case ErrorLevel.CRITICAL:
                    logger.LogCritical(error, "Critical error occurred: {ErrorId} - {Message}", errorId, error.Message);
                    break;
                case ErrorLevel.WARNING:
                    logger.LogWarning(error, "Warning error occurred: {ErrorId} - {Message}", errorId, error.Message);
                    break;
                case ErrorLevel.INFO:
                    logger.LogInformation(error, "Info error occurred: {ErrorId} - {Message}", errorId, error.Message);
                    break;
                case ErrorLevel.DEBUG:
                    logger.LogDebug(error, "Debug error occurred: {ErrorId} - {Message}", errorId, error.Message);
                    break;
            }

            // Log structured telemetry data
            logger.LogInformation("Error telemetry: {@LogEntry}", logEntry);
        }
    }

    /// <summary>
    /// Generate unique error ID
    /// </summary>
    private static string GenerateErrorId()
    {
        return $"ERR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}";
    }

    /// <summary>
    /// Get error metrics for monitoring
    /// </summary>
    public static ErrorMetrics GetMetrics()
    {
        lock (_metrics)
        {
            return new ErrorMetrics
            {
                TotalErrors = _metrics.TotalErrors,
                ErrorsByCategory = new Dictionary<ErrorCategory, int>(_metrics.ErrorsByCategory),
                ErrorsByLevel = new Dictionary<ErrorLevel, int>(_metrics.ErrorsByLevel),
                RecentErrors = new List<ErrorSummary>(_metrics.RecentErrors)
            };
        }
    }
}

/// <summary>
/// Error response structure
/// </summary>
public class ErrorResponse
{
    public string ErrorId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public ExceptionHandlingMiddleware.ErrorCategory Category { get; set; }
    public ExceptionHandlingMiddleware.ErrorLevel Level { get; set; }
    public int StatusCode { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Error metrics for monitoring
/// </summary>
public class ErrorMetrics
{
    public int TotalErrors { get; set; }
    public Dictionary<ExceptionHandlingMiddleware.ErrorCategory, int> ErrorsByCategory { get; set; } = new();
    public Dictionary<ExceptionHandlingMiddleware.ErrorLevel, int> ErrorsByLevel { get; set; } = new();
    public List<ErrorSummary> RecentErrors { get; set; } = new();
}

/// <summary>
/// Error summary for monitoring
/// </summary>
public class ErrorSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public ExceptionHandlingMiddleware.ErrorCategory Category { get; set; }
    public ExceptionHandlingMiddleware.ErrorLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Extension methods for error handling
/// </summary>
public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}

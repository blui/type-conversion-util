using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Middleware;

/// <summary>
/// Middleware for API key authentication.
/// Validates X-API-Key header against configured valid keys.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityConfig _securityConfig;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyMiddleware(
        RequestDelegate next,
        IOptions<SecurityConfig> securityConfig,
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _securityConfig = securityConfig?.Value ?? throw new ArgumentNullException(nameof(securityConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for public endpoints
        if (IsPublicEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Skip authentication if not required
        if (!_securityConfig.RequireApiKey)
        {
            await _next(context);
            return;
        }

        // Validate API keys are configured
        if (_securityConfig.ApiKeys == null || _securityConfig.ApiKeys.Count == 0)
        {
            _logger.LogWarning("API key authentication is enabled but no API keys are configured");
            await _next(context);
            return;
        }

        // Extract API key from header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            _logger.LogWarning("API key authentication failed: Missing {HeaderName} header from {IpAddress}",
                ApiKeyHeaderName, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = $"Missing {ApiKeyHeaderName} header. Include a valid API key in the request header."
            });
            return;
        }

        var providedKey = extractedApiKey.ToString();

        // Validate API key (case-sensitive)
        if (!_securityConfig.ApiKeys.Contains(providedKey))
        {
            _logger.LogWarning("API key authentication failed: Invalid API key from {IpAddress}",
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Unauthorized",
                message = "Invalid API key. Contact your administrator for a valid key."
            });
            return;
        }

        // Authentication successful
        _logger.LogDebug("API key authentication successful for {Path} from {IpAddress}",
            context.Request.Path, context.Connection.RemoteIpAddress);

        await _next(context);
    }

    /// <summary>
    /// Determines if an endpoint should bypass API key authentication.
    /// </summary>
    private static bool IsPublicEndpoint(PathString path)
    {
        // Public endpoints that don't require authentication
        var publicPaths = new[]
        {
            "/health",           // Health checks
            "/api-docs",         // Swagger UI
            "/swagger"           // Swagger endpoints
        };

        // Case-insensitive comparison is intentional for Windows/IIS compatibility.
        // ASP.NET Core on Windows treats paths as case-insensitive by framework design.
        // This ensures consistent behavior where "/Health" and "/health" are both accessible,
        // matching the standard Windows file system and IIS routing behavior.
        return publicPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Extension method for registering API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Middleware;

/// <summary>
/// Security middleware for security headers
/// </summary>
public class SecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersConfig _securityHeadersConfig;

    public SecurityMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeadersConfig> securityHeadersConfig)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _securityHeadersConfig = securityHeadersConfig?.Value ?? throw new ArgumentNullException(nameof(securityHeadersConfig));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        AddSecurityHeaders(context.Response);

        await _next(context);
    }

    /// <summary>
    /// Add essential security headers to response
    /// </summary>
    private void AddSecurityHeaders(HttpResponse response)
    {
        if (_securityHeadersConfig.NoSniff)
        {
            response.Headers[Constants.SecurityHeaders.XContentTypeOptions] = Constants.SecurityHeaders.NoSniff;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.FrameOptions))
        {
            response.Headers[Constants.SecurityHeaders.XFrameOptions] = _securityHeadersConfig.FrameOptions;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.XssProtection))
        {
            response.Headers[Constants.SecurityHeaders.XXssProtection] = _securityHeadersConfig.XssProtection;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.ReferrerPolicy))
        {
            response.Headers[Constants.SecurityHeaders.ReferrerPolicy] = _securityHeadersConfig.ReferrerPolicy;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.ContentSecurityPolicy))
        {
            response.Headers[Constants.SecurityHeaders.ContentSecurityPolicy] = _securityHeadersConfig.ContentSecurityPolicy;
        }
    }
}

/// <summary>
/// Extension method to add security middleware
/// </summary>
public static class SecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityMiddleware>();
    }
}

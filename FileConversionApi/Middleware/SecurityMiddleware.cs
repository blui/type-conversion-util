using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using FileConversionApi.Models;

namespace FileConversionApi.Middleware;

/// <summary>
/// Security middleware for IP filtering and security headers
/// </summary>
public class SecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly SecurityConfig _securityConfig;
    private readonly SecurityHeadersConfig _securityHeadersConfig;

    public SecurityMiddleware(
        RequestDelegate next,
        ILogger<SecurityMiddleware> logger,
        IOptions<SecurityConfig> securityConfig,
        IOptions<SecurityHeadersConfig> securityHeadersConfig)
    {
        _next = next;
        _logger = logger;
        _securityConfig = securityConfig.Value;
        _securityHeadersConfig = securityHeadersConfig.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // IP whitelist check (if enabled)
        if (_securityConfig.EnableIPFiltering && !IsAllowedIP(context.Connection.RemoteIpAddress?.ToString()))
        {
            _logger.LogWarning("Access denied for IP: {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsync("Access denied");
            return;
        }

        // Add security headers
        AddSecurityHeaders(context.Response);

        await _next(context);
    }

    /// <summary>
    /// Check if IP address is allowed
    /// </summary>
    private bool IsAllowedIP(string? ipAddress)
    {
        // If no whitelist configured, allow all
        if (_securityConfig.IPWhitelist == null || !_securityConfig.IPWhitelist.Any())
        {
            return true;
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            return false;
        }

        // Allow localhost
        if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress == "localhost")
        {
            return true;
        }

        // Check whitelist entries
        foreach (var allowedIP in _securityConfig.IPWhitelist)
        {
            // Simple CIDR prefix match for common internal networks
            if (allowedIP.Contains('/'))
            {
                var prefix = allowedIP.Split('/')[0];
                if (ipAddress.StartsWith(prefix.TrimEnd('.') + "."))
                {
                    return true;
                }
            }
            // Exact match
            else if (ipAddress == allowedIP || allowedIP == "localhost")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Add essential security headers to response
    /// </summary>
    private void AddSecurityHeaders(HttpResponse response)
    {
        if (_securityHeadersConfig.NoSniff)
        {
            response.Headers["X-Content-Type-Options"] = "nosniff";
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.FrameOptions))
        {
            response.Headers["X-Frame-Options"] = _securityHeadersConfig.FrameOptions;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.XssProtection))
        {
            response.Headers["X-XSS-Protection"] = _securityHeadersConfig.XssProtection;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.ReferrerPolicy))
        {
            response.Headers["Referrer-Policy"] = _securityHeadersConfig.ReferrerPolicy;
        }

        if (!string.IsNullOrEmpty(_securityHeadersConfig.ContentSecurityPolicy))
        {
            response.Headers["Content-Security-Policy"] = _securityHeadersConfig.ContentSecurityPolicy;
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

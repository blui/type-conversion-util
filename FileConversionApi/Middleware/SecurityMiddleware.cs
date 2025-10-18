using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using FileConversionApi.Models;
using System.Text.RegularExpressions;

namespace FileConversionApi.Middleware;

/// <summary>
/// Security middleware for defense-in-depth protection
/// Handles input validation, path traversal prevention, and security controls
/// </summary>
public class SecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly SecurityConfig _securityConfig;

    // Security constants
    private const int MaxFilenameLength = 255;

    public SecurityMiddleware(
        RequestDelegate next,
        ILogger<SecurityMiddleware> logger,
        IOptions<SecurityConfig> securityConfig)
    {
        _next = next;
        _logger = logger;
        _securityConfig = securityConfig.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // IP whitelist check
        if (!IsAllowedIP(context.Connection.RemoteIpAddress?.ToString()))
        {
            _logger.LogWarning("Access denied for IP: {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsync("Access denied");
            return;
        }

        // Security headers
        AddSecurityHeaders(context.Response);

        // Path traversal protection
        if (!ValidateRequestPath(context.Request.Path))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("Invalid request path");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Check if IP address is allowed
    /// </summary>
    private bool IsAllowedIP(string? ipAddress)
    {
        // If IP filtering is disabled, allow all
        if (!_securityConfig.EnableIPFiltering)
        {
            return true;
        }

        // If no whitelist configured, allow all
        if (_securityConfig.IPWhitelist == null || !_securityConfig.IPWhitelist.Any())
        {
            return true;
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            return false;
        }

        // Allow localhost variations
        if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress.StartsWith("127."))
        {
            return true;
        }

        // Check if IP matches any whitelist entry
        foreach (var allowedIP in _securityConfig.IPWhitelist)
        {
            if (allowedIP.Contains('/'))
            {
                // CIDR notation - simplified check
                if (IsInCIDRRange(ipAddress, allowedIP))
                {
                    return true;
                }
            }
            else
            {
                // Exact IP match
                if (ipAddress == allowedIP || allowedIP == "localhost")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Proper CIDR range check using bitwise operations
    /// </summary>
    private bool IsInCIDRRange(string ipString, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) return false;

            if (!IPAddress.TryParse(parts[0], out var networkIP) ||
                !IPAddress.TryParse(ipString, out var testIP))
                return false;

            if (!int.TryParse(parts[1], out int prefixLength))
                return false;

            // Validate prefix length
            if (prefixLength < 0 || prefixLength > 32)
                return false;

            // Get address bytes
            byte[] networkBytes = networkIP.GetAddressBytes();
            byte[] testBytes = testIP.GetAddressBytes();

            // Check address family match (IPv4 vs IPv6)
            if (networkBytes.Length != testBytes.Length)
                return false;

            // Compare full bytes
            int fullBytes = prefixLength / 8;
            for (int i = 0; i < fullBytes; i++)
            {
                if (networkBytes[i] != testBytes[i])
                    return false;
            }

            // Compare remaining bits if any
            int remainingBits = prefixLength % 8;
            if (remainingBits > 0 && fullBytes < networkBytes.Length)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((networkBytes[fullBytes] & mask) != (testBytes[fullBytes] & mask))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Add security headers to response
    /// </summary>
    private void AddSecurityHeaders(HttpResponse response)
    {
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
        response.Headers["X-XSS-Protection"] = "1; mode=block";
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy - very restrictive for air-gapped operation
        response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'";
    }

    /// <summary>
    /// Validate filename for security
    /// </summary>
    private static bool IsValidFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        // Check length
        if (filename.Length > MaxFilenameLength)
            return false;

        // Check for path traversal
        if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            return false;

        // Check for hidden files
        if (filename.StartsWith("."))
            return false;

        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (filename.Any(c => invalidChars.Contains(c)))
            return false;

        // Check for control characters
        if (filename.Any(c => char.IsControl(c)))
            return false;

        return true;
    }

    /// <summary>
    /// Validate request path for security
    /// </summary>
    private static bool ValidateRequestPath(PathString path)
    {
        var pathString = path.ToString();

        // Check for path traversal attempts
        if (pathString.Contains("..") || pathString.Contains("\\"))
            return false;

        // Check for suspicious patterns
        if (Regex.IsMatch(pathString, @"[\x00-\x1F\x7F-\x9F]"))
            return false;

        return true;
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

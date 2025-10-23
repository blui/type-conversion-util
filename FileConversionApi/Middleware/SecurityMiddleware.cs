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

        // Parse client IP address
        if (!IPAddress.TryParse(ipAddress, out var clientIP))
        {
            _logger.LogWarning("Invalid IP address format: {IP}", ipAddress);
            return false;
        }

        // Allow localhost
        if (IPAddress.IsLoopback(clientIP))
        {
            return true;
        }

        // Check whitelist entries
        foreach (var allowedEntry in _securityConfig.IPWhitelist)
        {
            // CIDR notation check
            if (allowedEntry.Contains('/'))
            {
                if (IsIPInCIDRRange(clientIP, allowedEntry))
                {
                    return true;
                }
            }
            // Exact match
            else if (IPAddress.TryParse(allowedEntry, out var allowedIP) && clientIP.Equals(allowedIP))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if IP address is within CIDR range using proper bit-level comparison
    /// </summary>
    private bool IsIPInCIDRRange(IPAddress clientIP, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid CIDR notation: {CIDR}", cidr);
                return false;
            }

            if (!IPAddress.TryParse(parts[0], out var networkAddress))
            {
                _logger.LogWarning("Invalid network address in CIDR: {CIDR}", cidr);
                return false;
            }

            if (!int.TryParse(parts[1], out var prefixLength))
            {
                _logger.LogWarning("Invalid prefix length in CIDR: {CIDR}", cidr);
                return false;
            }

            // Only support IPv4 CIDR for now
            if (clientIP.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                networkAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            if (prefixLength < 0 || prefixLength > 32)
            {
                _logger.LogWarning("Invalid prefix length (must be 0-32): {PrefixLength}", prefixLength);
                return false;
            }

            // Convert IP addresses to byte arrays
            var clientBytes = clientIP.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            // Create subnet mask
            uint mask = prefixLength == 0 ? 0 : ~(uint.MaxValue >> prefixLength);
            var maskBytes = BitConverter.GetBytes(mask);

            // Reverse if little-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(maskBytes);
            }

            // Compare network portions
            for (int i = 0; i < 4; i++)
            {
                if ((clientBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking CIDR range: {CIDR}", cidr);
            return false;
        }
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

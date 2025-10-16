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
    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB
    private const int MaxFilenameLength = 255;
    private const int UploadTimeoutSeconds = 30;

    // Allowed file extensions
    private readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".pdf", ".txt", ".xml", ".csv", ".xlsx", ".pptx",
        ".odt", ".ods", ".odp", ".odg", ".odf", ".rtf",
        ".sxw", ".sxc", ".sxi", ".sxd",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".svg", ".psd",
        ".html", ".htm"
    };

    // MIME type validation mapping
    private readonly Dictionary<string, string> _mimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".xml"] = "application/xml",
        [".csv"] = "text/csv",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".odg"] = "application/vnd.oasis.opendocument.graphics",
        [".rtf"] = "application/rtf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".tiff"] = "image/tiff",
        [".tif"] = "image/tiff",
        [".svg"] = "image/svg+xml",
        [".psd"] = "image/vnd.adobe.photoshop",
        [".html"] = "text/html",
        [".htm"] = "text/html"
    };

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

        // Input validation for file uploads
        if (context.Request.Method == "POST" && context.Request.Path.StartsWithSegments("/api/convert"))
        {
            if (!await ValidateFileUploadAsync(context))
            {
                return; // Validation failed, response already sent
            }
        }

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
        // If no whitelist configured, allow all
        if (_securityConfig.IPWhitelist == null || !_securityConfig.IPWhitelist.Any())
        {
            return true;
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            return false;
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
                if (ipAddress == allowedIP)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Simplified CIDR range check
    /// </summary>
    private bool IsInCIDRRange(string ip, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) return false;

            var networkIP = parts[0];
            var prefixLength = int.Parse(parts[1]);

            // For simplicity, check if IP starts with network prefix
            // In production, use a proper CIDR library
            return ip.StartsWith(networkIP.Split('.').Take(prefixLength / 8).Aggregate("", (a, b) => a + "." + b).TrimStart('.'));
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
    /// Validate file upload request
    /// </summary>
    private async Task<bool> ValidateFileUploadAsync(HttpContext context)
    {
        try
        {
            // Check content length
            if (context.Request.ContentLength > MaxFileSize)
            {
                _logger.LogWarning("File too large: {Size} bytes", context.Request.ContentLength);
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                await context.Response.WriteAsync($"File too large. Maximum size: {MaxFileSize} bytes");
                return false;
            }

            // Parse multipart form data
            var form = await context.Request.ReadFormAsync();

            // Validate file exists
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsync("No file provided");
                return false;
            }

            // Validate file size
            if (file.Length > MaxFileSize)
            {
                _logger.LogWarning("Uploaded file too large: {Size} bytes", file.Length);
                context.Response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
                await context.Response.WriteAsync($"File too large. Maximum size: {MaxFileSize} bytes");
                return false;
            }

            // Validate filename
            if (!IsValidFilename(file.FileName))
            {
                _logger.LogWarning("Invalid filename: {Filename}", file.FileName);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsync("Invalid filename");
                return false;
            }

            // Validate file extension
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Disallowed file extension: {Extension}", extension);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsync($"File type not allowed: {extension}");
                return false;
            }

            // Validate MIME type
            if (!string.IsNullOrEmpty(file.ContentType) &&
                _mimeTypes.TryGetValue(extension, out var expectedMime) &&
                !file.ContentType.StartsWith(expectedMime.Split('/')[0]))
            {
                _logger.LogWarning("MIME type mismatch. Expected: {Expected}, Got: {Actual}",
                    expectedMime, file.ContentType);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsync("Invalid file content type");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file upload");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync("Validation error");
            return false;
        }
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

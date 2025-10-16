using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using FileConversionApi.Services;

namespace FileConversionApi.Controllers;

/// <summary>
/// Health check controller
/// Provides health monitoring endpoints
/// </summary>
[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly ILibreOfficeService _libreOfficeService;
    private readonly HealthCheckService _healthCheckService;

    public HealthController(
        ILogger<HealthController> logger,
        ILibreOfficeService libreOfficeService,
        HealthCheckService healthCheckService)
    {
        _logger = logger;
        _libreOfficeService = libreOfficeService;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Basic health check
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    [ProducesResponseType(typeof(HealthResponse), 503)]
    public async Task<IActionResult> GetHealth()
    {
        var healthResult = await _healthCheckService.CheckHealthAsync();

        var response = new HealthResponse
        {
            Status = healthResult.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            Services = new Dictionary<string, ServiceHealth>()
        };

        // Check LibreOffice availability
        var libreOfficeAvailable = await _libreOfficeService.IsAvailableAsync();
        response.Services["LibreOffice"] = new ServiceHealth
        {
            Status = libreOfficeAvailable ? "Healthy" : "Unhealthy",
            Message = libreOfficeAvailable ? "Available" : "Not available"
        };

        // Add system health checks
        foreach (var entry in healthResult.Entries)
        {
            response.Services[entry.Key] = new ServiceHealth
            {
                Status = entry.Value.Status.ToString(),
                Message = entry.Value.Description ?? "No description"
            };
        }

        // Calculate overall status
        response.Status = healthResult.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";

        var statusCode = healthResult.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Detailed health check with system information
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), 200)]
    [ProducesResponseType(typeof(DetailedHealthResponse), 503)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        var healthResult = await _healthCheckService.CheckHealthAsync();
        var basicResponse = await GetHealth() as ObjectResult;
        var basicHealth = basicResponse?.Value as HealthResponse ?? new HealthResponse();

        var response = new DetailedHealthResponse
        {
            Status = basicHealth.Status,
            Timestamp = basicHealth.Timestamp,
            Services = basicHealth.Services,
            SystemInfo = new SystemInformation
            {
                OsVersion = Environment.OSVersion.ToString(),
                FrameworkVersion = Environment.Version.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            },
            HealthChecks = new List<HealthCheckDetail>()
        };

        // Add detailed health check information
        foreach (var entry in healthResult.Entries)
        {
            response.HealthChecks.Add(new HealthCheckDetail
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description,
                Duration = entry.Value.Duration,
                Data = entry.Value.Data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString())
            });
        }

        var statusCode = healthResult.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }
}

/// <summary>
/// Health response
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = "Unknown";
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();
}

/// <summary>
/// Service health information
/// </summary>
public class ServiceHealth
{
    public string Status { get; set; } = "Unknown";
    public string? Message { get; set; }
}

/// <summary>
/// Detailed health response
/// </summary>
public class DetailedHealthResponse : HealthResponse
{
    public SystemInformation SystemInfo { get; set; } = new();
    public List<HealthCheckDetail> HealthChecks { get; set; } = new();
}

/// <summary>
/// System information
/// </summary>
public class SystemInformation
{
    public string OsVersion { get; set; } = string.Empty;
    public string FrameworkVersion { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long WorkingSet { get; set; }
    public TimeSpan Uptime { get; set; }
}

/// <summary>
/// Health check detail
/// </summary>
public class HealthCheckDetail
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
}

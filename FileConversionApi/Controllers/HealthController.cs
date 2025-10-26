using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using FileConversionApi.Models;
using FileConversionApi.Services;

namespace FileConversionApi.Controllers;

/// <summary>
/// Provides application health status.
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
    /// Returns basic health status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    [ProducesResponseType(typeof(HealthResponse), 503)]
    public async Task<IActionResult> GetHealth()
    {
        var healthResult = await _healthCheckService.CheckHealthAsync();
        var libreOfficeAvailable = await _libreOfficeService.IsAvailableAsync();

        var response = new HealthResponse
        {
            Status = healthResult.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Services = new Dictionary<string, ServiceHealth>
            {
                ["LibreOffice"] = new()
                {
                    Status = libreOfficeAvailable ? "Healthy" : "Unhealthy",
                    Message = libreOfficeAvailable ? "Available" : "Not available"
                }
            }
        };

        foreach (var entry in healthResult.Entries)
        {
            response.Services[entry.Key] = new ServiceHealth
            {
                Status = entry.Value.Status.ToString(),
                Message = entry.Value.Description ?? "No description"
            };
        }

        var statusCode = healthResult.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Returns detailed health status with system information.
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), 200)]
    [ProducesResponseType(typeof(DetailedHealthResponse), 503)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        var healthResult = await _healthCheckService.CheckHealthAsync();
        var libreOfficeAvailable = await _libreOfficeService.IsAvailableAsync();

        var response = new DetailedHealthResponse
        {
            Status = healthResult.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Services = new Dictionary<string, ServiceHealth>
            {
                ["LibreOffice"] = new()
                {
                    Status = libreOfficeAvailable ? "Healthy" : "Unhealthy",
                    Message = libreOfficeAvailable ? "Available" : "Not available"
                }
            },
            SystemInfo = new SystemInformation
            {
                OsVersion = Environment.OSVersion.ToString(),
                FrameworkVersion = Environment.Version.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
            },
            HealthChecks = healthResult.Entries.Select(entry => new HealthCheckDetail
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description,
                Duration = entry.Value.Duration,
                Data = entry.Value.Data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString())
            }).ToList()
        };

        foreach (var entry in healthResult.Entries)
        {
            response.Services[entry.Key] = new ServiceHealth
            {
                Status = entry.Value.Status.ToString(),
                Message = entry.Value.Description ?? "No description"
            };
        }

        var statusCode = healthResult.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }
}

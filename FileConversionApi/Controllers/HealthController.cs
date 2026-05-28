using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services;
using FileConversionApi.Services.Interfaces;

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
    private readonly ILibreOfficeProcessManager _libreOfficeProcessManager;
    private readonly HealthCheckService _healthCheckService;

    public HealthController(
        ILogger<HealthController> logger,
        ILibreOfficeProcessManager libreOfficeProcessManager,
        HealthCheckService healthCheckService)
    {
        _logger = logger;
        _libreOfficeProcessManager = libreOfficeProcessManager;
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Returns health status with system information.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    [ProducesResponseType(typeof(HealthResponse), 503)]
    public async Task<IActionResult> GetHealth()
    {
        var healthResult = await _healthCheckService.CheckHealthAsync();
        var libreOfficeAvailable = await _libreOfficeProcessManager.IsAvailableAsync();

        // Build the Services dictionary from the registered health-check entries first, then
        // append the LibreOffice path probe under a stable distinct key. An earlier shape set
        // the LibreOffice entry up front and then re-looped Entries with no collision guard,
        // silently overwriting anything keyed "LibreOffice" by a future probe.
        var services = healthResult.Entries.ToDictionary(
            entry => entry.Key,
            entry => new ServiceHealth
            {
                Status = entry.Value.Status.ToString(),
                Message = entry.Value.Description ?? "No description"
            });

        services["LibreOffice.PathProbe"] = new ServiceHealth
        {
            Status = libreOfficeAvailable ? "Healthy" : "Unhealthy",
            Message = libreOfficeAvailable ? "Available" : "Not available"
        };

        var response = new HealthResponse
        {
            Status = healthResult.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Services = services,
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

        var statusCode = healthResult.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }
}

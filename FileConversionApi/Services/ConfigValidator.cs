using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FileConversionApi.Services;

/// <summary>
/// Configuration validator for comprehensive config validation and management
/// Provides schema-based validation and environment-specific configuration management
/// </summary>
public class ConfigValidator : IConfigValidator
{
    private readonly ILogger<ConfigValidator> _logger;
    private readonly IConfiguration _configuration;

    // Configuration validation schemas
    private readonly Dictionary<string, Dictionary<string, ConfigValidationRule>> _configSchemas;

    public ConfigValidator(ILogger<ConfigValidator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _configSchemas = InitializeSchemas();
    }

    /// <inheritdoc/>
    public ValidationResult ValidateConfiguration()
    {
        var result = new ValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>(),
            Info = new Dictionary<string, object>()
        };

        try
        {
            // Validate each configuration section
            foreach (var section in _configSchemas)
            {
                ValidateSection(section.Key, section.Value, result);
            }

            // Check for security-sensitive configurations
            ValidateSecurityConfiguration(result);

            // Check for environment-specific requirements
            ValidateEnvironmentConfiguration(result);

            _logger.LogInformation("Configuration validation completed. Valid: {Valid}, Errors: {Errors}, Warnings: {Warnings}",
                result.IsValid, result.Errors.Count, result.Warnings.Count);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
        }

        return result;
    }

    /// <inheritdoc/>
    public T GetValidatedConfig<T>(string sectionName) where T : new()
    {
        try
        {
            var section = _configuration.GetSection(sectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException($"Configuration section '{sectionName}' not found");
            }

            var config = section.Get<T>();
            if (config == null)
            {
                throw new InvalidOperationException($"Failed to bind configuration section '{sectionName}'");
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get validated config for section: {Section}", sectionName);
            throw;
        }
    }

    /// <inheritdoc/>
    public Dictionary<string, object> GetConfigurationHealth()
    {
        var health = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["sections"] = new Dictionary<string, object>(),
            ["security"] = new Dictionary<string, object>(),
            ["environment"] = new Dictionary<string, object>()
        };

        // Check each section
        foreach (var sectionName in _configSchemas.Keys)
        {
            var section = _configuration.GetSection(sectionName);
            var sectionHealth = new Dictionary<string, object>
            {
                ["exists"] = section.Exists(),
                ["valueCount"] = section.GetChildren().Count()
            };
            ((Dictionary<string, object>)health["sections"]).Add(sectionName, sectionHealth);
        }

        // Security health checks
        var securitySection = _configuration.GetSection("Security");
        ((Dictionary<string, object>)health["security"]).Add("ipWhitelistEnabled", securitySection.GetSection("IPWhitelist").GetChildren().Any());
        ((Dictionary<string, object>)health["security"]).Add("advancedSecurityEnabled", securitySection.GetValue<bool>("EnableAdvancedSecurity"));

        // Environment info
        ((Dictionary<string, object>)health["environment"]).Add("environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
        ((Dictionary<string, object>)health["environment"]).Add("os", Environment.OSVersion.ToString());
        ((Dictionary<string, object>)health["environment"]).Add("framework", Environment.Version.ToString());

        return health;
    }

    private void ValidateSection(string sectionName, Dictionary<string, ConfigValidationRule> rules, ValidationResult result)
    {
        var section = _configuration.GetSection(sectionName);

        foreach (var rule in rules)
        {
            var key = rule.Key;
            var validationRule = rule.Value;
            var value = section[key];

            if (string.IsNullOrEmpty(value) && validationRule.Required)
            {
                result.Errors.Add($"Required configuration '{sectionName}:{key}' is missing");
                result.IsValid = false;
                continue;
            }

            if (!string.IsNullOrEmpty(value))
            {
                var validationError = ValidateValue(key, value, validationRule);
                if (!string.IsNullOrEmpty(validationError))
                {
                    result.Errors.Add($"{sectionName}:{key} - {validationError}");
                    result.IsValid = false;
                }
            }
            else if (validationRule.DefaultValue != null)
            {
                result.Warnings.Add($"Using default value for '{sectionName}:{key}': {validationRule.DefaultValue}");
            }
        }
    }

    private string? ValidateValue(string key, string value, ConfigValidationRule rule)
    {
        try
        {
            switch (rule.Type)
            {
                case "number":
                    if (!double.TryParse(value, out var numValue))
                        return "Must be a valid number";

                    if (rule.Min.HasValue && numValue < rule.Min.Value)
                        return $"Must be >= {rule.Min.Value}";

                    if (rule.Max.HasValue && numValue > rule.Max.Value)
                        return $"Must be <= {rule.Max.Value}";

                    break;

                case "boolean":
                    if (!bool.TryParse(value) && value != "0" && value != "1")
                        return "Must be true/false, 0/1";
                    break;

                case "enum":
                    if (rule.AllowedValues != null && !rule.AllowedValues.Contains(value))
                        return $"Must be one of: {string.Join(", ", rule.AllowedValues)}";
                    break;

                case "string":
                    if (rule.Pattern != null && !Regex.IsMatch(value, rule.Pattern))
                        return $"Must match pattern: {rule.Pattern}";
                    break;

                case "path":
                    if (!Path.IsPathRooted(value) && !Path.IsPathFullyQualified(value))
                    {
                        // For relative paths, just warn but don't fail
                        return null; // We'll handle this in warnings
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            return $"Validation error: {ex.Message}";
        }

        return null;
    }

    private void ValidateSecurityConfiguration(ValidationResult result)
    {
        var securitySection = _configuration.GetSection("Security");

        // Check IP whitelist configuration
        var ipWhitelist = securitySection.GetSection("IPWhitelist").GetChildren();
        if (ipWhitelist.Any())
        {
            result.Info["ipWhitelistEnabled"] = true;
            result.Info["ipWhitelistCount"] = ipWhitelist.Count();

            // Validate IP formats
            foreach (var ip in ipWhitelist)
            {
                if (!IsValidIpOrCidr(ip.Value))
                {
                    result.Warnings.Add($"Invalid IP/CIDR format in whitelist: {ip.Value}");
                }
            }
        }

        // Check SSL configuration
        var sslSection = _configuration.GetSection("SSL");
        var sslEnabled = sslSection.GetValue<bool>("Enabled");
        if (sslEnabled)
        {
            var certPath = sslSection.GetValue<string>("CertificatePath");
            var certPassword = sslSection.GetValue<string>("CertificatePassword");

            if (string.IsNullOrEmpty(certPath))
                result.Errors.Add("SSL enabled but CertificatePath not configured");

            if (string.IsNullOrEmpty(certPassword))
                result.Warnings.Add("SSL certificate password not configured - may require user input");
        }
    }

    private void ValidateEnvironmentConfiguration(ValidationResult result)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        // Environment-specific validations
        switch (environment.ToLower())
        {
            case "development":
                result.Info["environment"] = "Development";
                result.Warnings.Add("Running in development mode - ensure this is not production");
                break;

            case "production":
                result.Info["environment"] = "Production";

                // Production-specific checks
                var fileHandling = _configuration.GetSection("FileHandling");
                var tempDir = fileHandling.GetValue<string>("TempDirectory");
                if (string.IsNullOrEmpty(tempDir) || tempDir.Contains("temp"))
                {
                    result.Warnings.Add("Using temporary directory for file storage in production");
                }
                break;

            default:
                result.Info["environment"] = environment;
                break;
        }
    }

    private bool IsValidIpOrCidr(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        // Check if it's a valid IP address
        if (System.Net.IPAddress.TryParse(value, out _)) return true;

        // Check if it's a valid CIDR notation (simplified check)
        var parts = value.Split('/');
        if (parts.Length == 2)
        {
            if (System.Net.IPAddress.TryParse(parts[0], out _) &&
                int.TryParse(parts[1], out var prefix) &&
                prefix >= 0 && prefix <= 32)
            {
                return true;
            }
        }

        return false;
    }

    private Dictionary<string, Dictionary<string, ConfigValidationRule>> InitializeSchemas()
    {
        return new Dictionary<string, Dictionary<string, ConfigValidationRule>>
        {
            ["Server"] = new()
            {
                ["Port"] = new() { Type = "number", Min = 1, Max = 65535, DefaultValue = "3000", Required = false },
                ["Host"] = new() { Type = "string", Pattern = @"^[\w.-]+$", DefaultValue = "localhost", Required = false },
                ["Environment"] = new() { Type = "enum", AllowedValues = new[] { "Development", "Test", "Staging", "Production" }, DefaultValue = "Development", Required = false }
            },
            ["FileHandling"] = new()
            {
                ["UploadLimit"] = new() { Type = "string", Pattern = @"^\d+(kb|mb|gb)$", DefaultValue = "50mb", Required = false },
                ["MaxFileSize"] = new() { Type = "number", Min = 1024, Max = 2147483648, DefaultValue = "52428800", Required = false },
                ["TempDirectory"] = new() { Type = "path", DefaultValue = "./temp", Required = false },
                ["OutputDirectory"] = new() { Type = "path", DefaultValue = "./temp/converted", Required = false }
            },
            ["RateLimiting"] = new()
            {
                ["EnableEndpointRateLimiting"] = new() { Type = "boolean", DefaultValue = "true", Required = false },
                ["HttpStatusCode"] = new() { Type = "number", Min = 400, Max = 599, DefaultValue = "429", Required = false }
            },
            ["Security"] = new()
            {
                ["EnableAdvancedSecurity"] = new() { Type = "boolean", DefaultValue = "true", Required = false }
            },
            ["LibreOffice"] = new()
            {
                ["SdkPath"] = new() { Type = "path", DefaultValue = "C:\\Program Files\\LibreOfficeSDK", Required = false },
                ["ConversionQuality"] = new() { Type = "enum", AllowedValues = new[] { "low", "medium", "high" }, DefaultValue = "high", Required = false }
            },
            ["SSL"] = new()
            {
                ["Enabled"] = new() { Type = "boolean", DefaultValue = "false", Required = false },
                ["AcceptSelfSigned"] = new() { Type = "boolean", DefaultValue = "true", Required = false }
            }
        };
    }
}

/// <summary>
/// Configuration validation rule
/// </summary>
public class ConfigValidationRule
{
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? Pattern { get; set; }
    public string[]? AllowedValues { get; set; }
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Info { get; set; } = new();
}

/// <summary>
/// Config validator interface
/// </summary>
public interface IConfigValidator
{
    ValidationResult ValidateConfiguration();
    T GetValidatedConfig<T>(string sectionName) where T : new();
    Dictionary<string, object> GetConfigurationHealth();
}

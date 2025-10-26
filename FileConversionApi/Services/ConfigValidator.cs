using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FileConversionApi.Services;

public class ConfigValidator : IConfigValidator
{
    private readonly ILogger<ConfigValidator> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, Dictionary<string, ConfigValidationRule>> _schemas;

    public ConfigValidator(ILogger<ConfigValidator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _schemas = InitializeSchemas();
    }

    public ValidationResult ValidateConfiguration()
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            foreach (var section in _schemas)
            {
                ValidateSection(section.Key, section.Value, result);
            }

            ValidateSecuritySettings(result);
            ValidateEnvironment(result);

            _logger.LogInformation("Config validation: Valid={Valid}, Errors={Errors}, Warnings={Warnings}",
                result.IsValid, result.Errors.Count, result.Warnings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Config validation failed");
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
        }

        return result;
    }

    public T GetValidatedConfig<T>(string sectionName) where T : new()
    {
        var section = _configuration.GetSection(sectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException($"Config section '{sectionName}' not found");
        }

        return section.Get<T>() ?? throw new InvalidOperationException($"Failed to bind section '{sectionName}'");
    }

    public Dictionary<string, object> GetConfigurationHealth()
    {
        var health = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["sections"] = new Dictionary<string, object>(),
            ["security"] = GetSecurityHealth(),
            ["environment"] = GetEnvironmentHealth()
        };

        foreach (var sectionName in _schemas.Keys)
        {
            var section = _configuration.GetSection(sectionName);
            var sectionHealth = new Dictionary<string, object>
            {
                ["exists"] = section.Exists(),
                ["valueCount"] = section.GetChildren().Count()
            };
            ((Dictionary<string, object>)health["sections"]).Add(sectionName, sectionHealth);
        }

        return health;
    }

    private void ValidateSection(string sectionName, Dictionary<string, ConfigValidationRule> rules, ValidationResult result)
    {
        var section = _configuration.GetSection(sectionName);

        foreach (var rule in rules)
        {
            var value = section[rule.Key];

            if (string.IsNullOrEmpty(value) && rule.Value.Required)
            {
                result.Errors.Add($"Required config '{sectionName}:{rule.Key}' is missing");
                result.IsValid = false;
                continue;
            }

            if (!string.IsNullOrEmpty(value))
            {
                var error = ValidateValue(rule.Key, value, rule.Value);
                if (!string.IsNullOrEmpty(error))
                {
                    result.Errors.Add($"{sectionName}:{rule.Key} - {error}");
                    result.IsValid = false;
                }
            }
            else if (rule.Value.DefaultValue != null)
            {
                result.Warnings.Add($"Using default for '{sectionName}:{rule.Key}': {rule.Value.DefaultValue}");
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
                    if (!double.TryParse(value, out var num)) return "Must be a valid number";
                    if (rule.Min.HasValue && num < rule.Min.Value) return $"Must be >= {rule.Min.Value}";
                    if (rule.Max.HasValue && num > rule.Max.Value) return $"Must be <= {rule.Max.Value}";
                    break;

                case "boolean":
                    if (!bool.TryParse(value, out _) && value != "0" && value != "1")
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
            }
        }
        catch (Exception ex)
        {
            return $"Validation error: {ex.Message}";
        }

        return null;
    }

    private void ValidateSecuritySettings(ValidationResult result)
    {
        var security = _configuration.GetSection("Security");
        var ipWhitelist = security.GetSection("IPWhitelist").GetChildren().ToList();

        if (ipWhitelist.Any())
        {
            result.Info["ipWhitelistEnabled"] = true;
            result.Info["ipWhitelistCount"] = ipWhitelist.Count;

            foreach (var ip in ipWhitelist)
            {
                if (!IsValidIpOrCidr(ip.Value))
                {
                    result.Warnings.Add($"Invalid IP/CIDR in whitelist: {ip.Value}");
                }
            }
        }
    }

    private void ValidateEnvironment(ValidationResult result)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        result.Info["environment"] = env;

        if (env.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add("Running in development mode");
        }
    }

    private Dictionary<string, object> GetSecurityHealth()
    {
        var security = _configuration.GetSection("Security");
        return new Dictionary<string, object>
        {
            ["ipWhitelistEnabled"] = security.GetSection("IPWhitelist").GetChildren().Any(),
            ["ipFilteringEnabled"] = security.GetValue<bool>("EnableIPFiltering")
        };
    }

    private Dictionary<string, object> GetEnvironmentHealth()
    {
        return new Dictionary<string, object>
        {
            ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            ["os"] = Environment.OSVersion.ToString(),
            ["framework"] = Environment.Version.ToString()
        };
    }

    private bool IsValidIpOrCidr(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (System.Net.IPAddress.TryParse(value, out _)) return true;

        var parts = value.Split('/');
        return parts.Length == 2 &&
               System.Net.IPAddress.TryParse(parts[0], out _) &&
               int.TryParse(parts[1], out var prefix) &&
               prefix >= 0 && prefix <= 32;
    }

    private Dictionary<string, Dictionary<string, ConfigValidationRule>> InitializeSchemas()
    {
        return new Dictionary<string, Dictionary<string, ConfigValidationRule>>
        {
            ["FileHandling"] = new()
            {
                ["MaxFileSize"] = new() { Type = "number", Min = 1024, Max = 2147483648, Required = false },
                ["TempDirectory"] = new() { Type = "string", Required = false }
            },
            ["Security"] = new()
            {
                ["EnableIPFiltering"] = new() { Type = "boolean", Required = false }
            }
        };
    }
}

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

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Info { get; set; } = new();
}

public interface IConfigValidator
{
    ValidationResult ValidateConfiguration();
    T GetValidatedConfig<T>(string sectionName) where T : new();
    Dictionary<string, object> GetConfigurationHealth();
}

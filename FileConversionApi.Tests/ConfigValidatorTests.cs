using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FileConversionApi.Services;

namespace FileConversionApi.Tests;

public class ConfigValidatorTests
{
    private readonly ConfigValidator _validator;
    private readonly Mock<ILogger<ConfigValidator>> _loggerMock;
    private readonly IConfiguration _configuration;

    public ConfigValidatorTests()
    {
        _loggerMock = new Mock<ILogger<ConfigValidator>>();

        // Create in-memory configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:Port"] = "3000",
            ["Server:Host"] = "localhost",
            ["Server:Environment"] = "Development",
            ["FileHandling:UploadLimit"] = "50mb",
            ["FileHandling:MaxFileSize"] = "52428800",
            ["FileHandling:TempDirectory"] = "./temp",
            ["RateLimiting:EnableEndpointRateLimiting"] = "true",
            ["RateLimiting:HttpStatusCode"] = "429",
            ["Security:EnableAdvancedSecurity"] = "true",
            ["LibreOffice:SdkPath"] = "C:\\Program Files\\LibreOfficeSDK",
            ["LibreOffice:ConversionQuality"] = "high",
            ["SSL:Enabled"] = "false"
        });

        _configuration = configBuilder.Build();
        _validator = new ConfigValidator(_loggerMock.Object, _configuration);
    }

    [Fact]
    public void ValidateConfiguration_WithValidConfig_ReturnsValidResult()
    {
        // Act
        var result = _validator.ValidateConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmptyOrNull();
        result.Info.Should().NotBeNull();
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidPort_ReturnsInvalidResult()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:Port"] = "99999", // Invalid port
            ["Server:Host"] = "localhost",
            ["Server:Environment"] = "Development"
        });
        var invalidConfig = configBuilder.Build();
        var validator = new ConfigValidator(_loggerMock.Object, invalidConfig);

        // Act
        var result = validator.ValidateConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().ContainMatch("*Server:Port*");
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidEnvironment_ReturnsInvalidResult()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:Port"] = "3000",
            ["Server:Host"] = "localhost",
            ["Server:Environment"] = "InvalidEnv" // Invalid environment
        });
        var invalidConfig = configBuilder.Build();
        var validator = new ConfigValidator(_loggerMock.Object, invalidConfig);

        // Act
        var result = validator.ValidateConfiguration();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().ContainMatch("*Server:Environment*");
    }

    [Fact]
    public void GetValidatedConfig_WithValidSection_ReturnsConfigObject()
    {
        // Act
        var serverConfig = _validator.GetValidatedConfig<TestServerConfig>("Server");

        // Assert
        serverConfig.Should().NotBeNull();
        serverConfig.Port.Should().Be(3000);
        serverConfig.Host.Should().Be("localhost");
        serverConfig.Environment.Should().Be("Development");
    }

    [Fact]
    public void GetValidatedConfig_WithInvalidSection_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _validator.GetValidatedConfig<TestServerConfig>("NonExistentSection"));
    }

    [Fact]
    public void GetConfigurationHealth_ReturnsHealthInformation()
    {
        // Act
        var health = _validator.GetConfigurationHealth();

        // Assert
        health.Should().NotBeNull();
        health.Should().ContainKey("timestamp");
        health.Should().ContainKey("sections");
        health.Should().ContainKey("security");
        health.Should().ContainKey("environment");

        var sections = health["sections"] as Dictionary<string, object>;
        sections.Should().NotBeNull();
        sections.Should().ContainKey("Server");
        sections.Should().ContainKey("FileHandling");
        sections.Should().ContainKey("Security");
    }

    // Test configuration classes
    private class TestServerConfig
    {
        public int Port { get; set; }
        public string Host { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
    }
}

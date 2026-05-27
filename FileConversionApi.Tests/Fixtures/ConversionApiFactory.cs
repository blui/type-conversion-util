using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Hosts the real Program entry point in-process for integration tests. An optional
/// <see cref="Action{T}"/> over <see cref="IServiceCollection"/> registers test doubles
/// or configuration bindings via the standard ASP.NET Core test-host service-override seam.
/// </summary>
public sealed class ConversionApiFactory : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _configureServices;

    /// <summary>
    /// Hosts the production DI graph unchanged.
    /// </summary>
    public ConversionApiFactory()
    {
    }

    /// <summary>
    /// Hosts the production DI graph and applies the supplied service-collection mutator
    /// during web host construction. The mutator runs after the production services are
    /// registered, so callers can replace registrations with test doubles.
    /// </summary>
    /// <param name="configureServices">
    /// Service-collection mutator invoked from <see cref="ConfigureWebHost"/>. Must not be null.
    /// </param>
    public ConversionApiFactory(Action<IServiceCollection> configureServices)
    {
        _configureServices = configureServices ?? throw new ArgumentNullException(nameof(configureServices));
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        if (_configureServices is not null)
        {
            builder.ConfigureServices(_configureServices);
        }
    }
}

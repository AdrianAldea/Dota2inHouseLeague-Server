using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace IHLDota2Bot.ServiceDefaults.Tests;

/// <summary>
/// Smoke tests for the shared Aspire service-defaults wiring. This project is
/// mostly framework configuration, so the meaningful assertions are: the defaults
/// register what the rest of the solution depends on (the "self" liveness check,
/// OpenTelemetry providers), and the resulting DI container actually builds.
/// </summary>
public class ServiceDefaultsTests
{
    [Fact]
    public void AddDefaultHealthChecks_RegistersSelfLivenessCheck()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var self = Assert.Single(options.Registrations, r => r.Name == "self");
        Assert.Contains("live", self.Tags);
    }

    [Fact]
    public void AddServiceDefaults_ReturnsSameBuilderForChaining()
    {
        var builder = Host.CreateApplicationBuilder();

        var returned = builder.AddServiceDefaults();

        Assert.Same(builder, returned);
    }

    [Fact]
    public void AddServiceDefaults_RegistersHealthChecksAndBuildsContainer()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        Assert.Contains(options.Registrations, r => r.Name == "self");
    }

    [Fact]
    public void ConfigureOpenTelemetry_RegistersTracerAndMeterProviders()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureOpenTelemetry();

        using var provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }
}

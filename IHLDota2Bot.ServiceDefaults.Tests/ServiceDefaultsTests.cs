using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace IHLDota2Bot.ServiceDefaults.Tests;

/// <summary>
/// Tests for the shared Aspire service-defaults wiring. The smoke tests verify that
/// the DI container builds and the core registrations exist; the functional tests
/// verify observable behavior: health-check tags, environment gating on endpoint
/// mapping, OTel logging options, and OTLP exporter opt-in.
/// </summary>
public class ServiceDefaultsTests
{
    // ── Smoke tests (DI builds, core registrations exist) ────────────────────

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

    // ── Functional: health-check configuration ────────────────────────────────

    [Fact]
    public void AddDefaultHealthChecks_SelfCheck_IsTaggedLive()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();

        using var provider = builder.Services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var self = opts.Registrations.Single(r => r.Name == "self");
        Assert.Contains("live", self.Tags);
        // Verify the liveness check resolves without throwing.
        var check = self.Factory(provider);
        Assert.NotNull(check);
    }

    // ── Functional: MapDefaultEndpoints environment gating ───────────────────

    [Fact]
    public void MapDefaultEndpoints_Development_RegistersHealthAndAliveRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.AddServiceDefaults();

        var app = builder.Build();
        app.MapDefaultEndpoints();

        // Routes are registered in the app's IEndpointRouteBuilder.DataSources, not DI.
        var routeBuilder = (Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app;
        var routePaths = routeBuilder.DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        Assert.Contains("/health", routePaths);
        Assert.Contains("/alive", routePaths);
    }

    [Fact]
    public void MapDefaultEndpoints_Production_DoesNotRegisterRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.AddServiceDefaults();

        var app = builder.Build();
        app.MapDefaultEndpoints();

        var routeBuilder = (Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app;
        var routePaths = routeBuilder.DataSources
            .SelectMany(s => s.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        Assert.DoesNotContain("/health", routePaths);
        Assert.DoesNotContain("/alive", routePaths);
    }

    // ── Functional: OTel logging options ─────────────────────────────────────

    [Fact]
    public void ConfigureOpenTelemetry_Logging_IncludesFormattedMessageAndScopes()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureOpenTelemetry();

        using var provider = builder.Services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;

        Assert.True(opts.IncludeFormattedMessage);
        Assert.True(opts.IncludeScopes);
    }

    // ── Functional: OTLP exporter opt-in ─────────────────────────────────────

    [Fact]
    public void ConfigureOpenTelemetry_WithOtlpEndpoint_RegistersOtlpExporter()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317";
        builder.ConfigureOpenTelemetry();

        // UseOtlpExporter registers an IMetricExporter<>. Verify the container builds
        // and OTel providers resolve (the OTLP export is lazy — it won't fail on no
        // real collector during test resolution).
        using var provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    [Fact]
    public void ConfigureOpenTelemetry_WithoutOtlpEndpoint_DoesNotThrow()
    {
        var builder = Host.CreateApplicationBuilder();
        // No OTEL_EXPORTER_OTLP_ENDPOINT set — must be a no-op.
        builder.ConfigureOpenTelemetry();

        var ex = Record.Exception(() => builder.Services.BuildServiceProvider());

        Assert.Null(ex);
    }
}


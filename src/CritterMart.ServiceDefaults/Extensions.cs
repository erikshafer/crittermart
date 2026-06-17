using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Standard Aspire ServiceDefaults: OpenTelemetry (ADR 005), health checks, service
// discovery, and HTTP resilience. Each service calls builder.AddServiceDefaults().
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.AddFrontendCors();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Critter Stack meters — the tracing pipeline below already registers the
                    // matching ActivitySources, but metrics need their own meter registration or
                    // the counters emit into a void. "Marten" carries marten.event.append (ADR 005's
                    // TrackEventCounters); Wolverine's meter is "Wolverine:{ServiceName}" (NOT just
                    // "Wolverine" — the meter name differs from the ActivitySource), so a wildcard
                    // catches all three services (Wolverine:Catalog/Inventory/Orders) from this
                    // shared ServiceDefaults without hardcoding a service name.
                    .AddMeter("Marten")
                    .AddMeter("Wolverine:*");
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    // Critter Stack ActivitySources — the cross-service handler + event spans.
                    .AddSource("Wolverine")
                    .AddSource("Marten")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Aspire injects OTEL_EXPORTER_OTLP_ENDPOINT (the dashboard's collector).
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // CORS for the round-two SPA. The Vite + React storefront calls each Wolverine.Http service
    // directly over HTTP with no BFF (ADR 006 / ADR 015), so every service must opt the browser
    // origin in or the cross-origin calls are blocked. Origins come from config ("Cors:AllowedOrigins",
    // an array); the AppHost injects the real frontend origin once AddViteApp lands. Until then,
    // Development falls back to the conventional Vite dev-server origin so the storefront works on a
    // plain `dotnet run`. Identity is a hardcoded id carried in the request body (ADR 009) — no
    // cookies — so credentials are not allowed, which keeps AllowAnyHeader/AllowAnyMethod safe.
    public static TBuilder AddFrontendCors<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is null or { Length: 0 })
        {
            origins = builder.Environment.IsDevelopment()
                ? ["http://localhost:5173"]
                : [];
        }

        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}

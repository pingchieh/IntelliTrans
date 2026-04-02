using IntelliTrans.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IntelliTrans.Cli;

public static class HostBuilderExtensions
{
    public static TBuilder ConfigureDatabase<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        string dbType = builder.Configuration.GetValue("DBType", DbType.Sqlite.Name);
        builder.Services.AddDbContext<IntelliSenseDbContext>(options =>
        {
            if (dbType == DbType.Sqlite.Name)
            {
                options.UseSqlite(
                    builder.Configuration.GetConnectionString(DbType.Sqlite.Name)!,
                    x => x.MigrationsAssembly(DbType.Sqlite.Assembly)
                );
            }

            if (dbType == DbType.Postgres.Name)
            {
                options.UseNpgsql(
                    builder.Configuration.GetConnectionString(DbType.Postgres.Name)!,
                    x => x.MigrationsAssembly(DbType.Postgres.Assembly)
                );
            }
        });
        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder
            .Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(builder.Environment.ApplicationName);
                resource.AddEnvironmentVariableDetector();
                resource.AddOperatingSystemDetector();
                resource.AddProcessDetector();
                resource.AddProcessRuntimeDetector();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(builder.Environment.ApplicationName)
                    .AddMeter("OpenAI.*")
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource("OpenAI.*")
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
        );

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}

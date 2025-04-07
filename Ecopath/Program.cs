using Ecopath.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ecopath;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddGrpc();
        builder.Services.AddGrpcHealthChecks()
                        .AddCheck("Sample", () => HealthCheckResult.Healthy());

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        app.MapGrpcService<EcopathWorkflowService>();
        app.MapGrpcHealthChecksService();

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}
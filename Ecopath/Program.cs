using Ecopath.EwE;
using Ecopath.Services;
using EwECore;

namespace Ecopath;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<ExceptionMetadataInterceptor>();
        });

        builder.Services.AddSingleton<CheckSimulationService>(); // Add CheckSimulationService as a Singleton
        builder.Services.AddSingleton<cCore>(); //<ICore, cCore>();  // better would it be when cCore derives from ICore, but this is not the case
        builder.Services.AddSingleton<IEwEConfiguration, EwEConfiguration>();
        builder.Services.AddSingleton<IEwEController, EwEController>();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        app.MapGrpcService<EcopathEcologyService>();
        app.MapGrpcService<EcopathWorkflowService>();
        app.MapGrpcService<EcopathFisheryService>();
        app.MapGrpcService<EcopathMarketService>();

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}
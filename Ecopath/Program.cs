using ControlledVocabularies.Common;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Registries;
using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;

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

        builder.Services.AddSingleton<CheckSimulationService>();
        builder.Services.AddSingleton<IEwECore, EwE.Wrapper.EwECore>();
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

        // Just pretending, to be made real w Rik
        GlobalServiceLocator.Register(new KeyFieldDescriptorRegistry());
        GlobalServiceLocator.Register(new VocabularyRegistry());

        app.Run();
    }
}
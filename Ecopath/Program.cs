using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;

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
        builder.Services.AddSingleton<ASFISSpeciesCodeVocabulary>();
        builder.Services.AddSingleton<ISSCFGGearCodeVocabulary>();
        builder.Services.AddSingleton<ISO3166CountryCodeVocabulary>();
        builder.Services.AddSingleton<SURIMILifestageVocabulary>();

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
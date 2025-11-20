using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
using EwEUtils.Logging;

namespace Ecopath;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Initialize LoggerFactory of EwE sources that don't get the ILogger via Dependency injection
        LoggingContext.LoggerFactory = LoggerFactory.Create(logBuilder =>
        {
            logBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));   // so you can add a 'Logging' section to the appsettings.json to configure logging
            logBuilder.AddConsole();
            logBuilder.AddDebug();
        });

        // Create a logger for the Program class
        var logger = LoggingContext.LoggerFactory.CreateLogger<Program>();
        logger.LogInformation("Ecopath starting up.......................");

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
        builder.Services.AddSingleton<IKeyFieldDescriptorRegistry, KeyFieldDescriptorRegistry>();
        builder.Services.AddSingleton<IVocabularyRegistry, VocabularyRegistry>();
        builder.Services.AddSingleton<IForeignKeyResolver, ForeignKeyResolver>();
        builder.Services.AddSingleton<FieldInferenceOrchestrator>();
        builder.Services.AddSingleton<IKeyFieldDescriptorIndexer, KeyFieldDescriptorIndexer>();
        builder.Services.AddSingleton<IMultiLevelKeyFactory, MultiLevelKeyFactory>();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        app.MapGrpcService<EcopathEcologyService>();
        app.MapGrpcService<EcopathWorkflowService>();
        app.MapGrpcService<EcopathFisheryService>();
        app.MapGrpcService<EcopathMarketService>();

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        logger.LogInformation("Ecopath running.......................");
        app.Run();
        logger.LogInformation("Ecopath shutting down.......................");
    }
}
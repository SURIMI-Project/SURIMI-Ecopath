using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;

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
        builder.Services.AddSingleton<IKeyFieldDescriptorRegistry, KeyFieldDescriptorRegistry>();
        builder.Services.AddSingleton<IVocabularyRegistry, VocabularyRegistry>();
        builder.Services.AddSingleton<IKeyFieldDescriptorIndexer, KeyFieldDescriptorIndexer>();
        builder.Services.AddSingleton<IForeignKeyResolver, ForeignKeyResolver>();
        builder.Services.AddSingleton<IStrategyBasedMatcher, StrategyBasedMatcher>();
        builder.Services.AddSingleton<IVocabularyMatcher, GenericVocabularyMatcher>();
        builder.Services.AddSingleton<IFieldInferenceOrchestrator, FieldInferenceOrchestrator>();

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
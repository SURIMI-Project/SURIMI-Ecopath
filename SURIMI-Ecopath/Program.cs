using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.BlobStore;
using Eii.BlobStore.Minio;
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
using Minio;
using Minio.DataModel.Args;

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

        builder.Services.AddSingleton<IBlobStore>(sp =>
        {
            // if AWS_ACCESS_KEY_ID is set, use MinIO
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
            {
                var minio = new MinioClient()
                    .WithEndpoint(Environment.GetEnvironmentVariable("AWS_S3_ENDPOINT"), 443)
                    .WithCredentials(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"), Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"))
                    .WithSSL(true) // set to true if your endpoint uses HTTPS
                    .Build();

                // Ensure bucket exists (idempotent)
                var bucket = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME")!;
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var exists = minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cts.Token).GetAwaiter().GetResult();
                if (!exists)
                {
                    minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cts.Token).GetAwaiter().GetResult();
                }

                logger.LogInformation("Environment variable AWS_ACCESS_KEY_ID found. Using MinioBlobStore");
                return new MinioBlobStore(minio, bucket, inputBasePrefix: "surimi-ecopath/config", outputBasePrefix: "surimi-ecopath", localInputRoot: "Includes", localOutputRoot: "Output");
            }

            // Default local Filesystem
            logger.LogInformation("Using LocalBlobStore");
            return new LocalBlobStore( inputRoot: "Includes", outputRoot: "Output");
        });

        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<ExceptionMetadataInterceptor>();
            options.MaxReceiveMessageSize = 100 * 1024 * 1024; // 100 MB
            options.MaxSendMessageSize = 100 * 1024 * 1024;    // 100 MB
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
        app.MapGrpcService<EcologyProviderService>();
        app.MapGrpcService<EcopathWorkflowService>();
        app.MapGrpcService<CatchProviderService>();
        app.MapGrpcService<CatchConsumerService>();
        app.MapGrpcService<SalesProviderService>();
        app.MapGrpcService<SpeciePriceConsumerService>();


        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        LoadVaultSecretsInEnvironmentVariables();
        logger.LogInformation("============================= Logging All Environment Variables =============================");
        foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
        {
            logger.LogInformation("{Key}: {Value}", envVar.Key, envVar.Value);
        }
        logger.LogInformation("============================= End of Environment Variables =============================");

        logger.LogInformation("Ecopath running.......................");
        app.Run();
        logger.LogInformation("Ecopath shutting down.......................");

        void LoadVaultSecretsInEnvironmentVariables()
        {
            var vaultAddr = Environment.GetEnvironmentVariable("VAULT_ADDR");
            var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
            var vaultTopDir = Environment.GetEnvironmentVariable("VAULT_TOP_DIR");
            var vaultRelativePath = Environment.GetEnvironmentVariable("VAULT_RELATIVE_PATH");
            var vaultMount = Environment.GetEnvironmentVariable("VAULT_MOUNT");
            if (string.IsNullOrEmpty(vaultAddr) || string.IsNullOrEmpty(vaultToken) || string.IsNullOrEmpty(vaultTopDir) || string.IsNullOrEmpty(vaultRelativePath) || string.IsNullOrEmpty(vaultMount))
            {
                Console.WriteLine("Vault Addr, Token, Top Dir, Relative Path, or Mount not set in environment variables. Skipping Vault loading.");
                return;
            }
            var vaultClient = new VaultSharp.VaultClient(new VaultSharp.VaultClientSettings(vaultAddr, new VaultSharp.V1.AuthMethods.Token.TokenAuthMethodInfo(vaultToken)));
            // Assuming secrets are stored under "secret/data/surimi"
            var secretPath = $"{vaultTopDir}/{vaultRelativePath}";
            var secret = vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(secretPath, mountPoint: vaultMount).Result;
            foreach (var kv in secret.Data.Data)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value.ToString());
                Console.WriteLine($"Loaded secret '{kv.Key}' from Vault into environment variables.");
            }
        }
    }
}
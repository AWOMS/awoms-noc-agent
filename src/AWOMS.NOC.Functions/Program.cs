using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using AWOMS.NOC.Shared;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register Azure Cosmos DB client
        var cosmosConnectionString = context.Configuration["CosmosDbConnectionString"];
        if (!string.IsNullOrWhiteSpace(cosmosConnectionString))
        {
            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var database = cosmosClient.CreateDatabaseIfNotExistsAsync(Constants.CosmosDatabaseName)
                .GetAwaiter()
                .GetResult()
                .Database;

            database.CreateContainerIfNotExistsAsync(Constants.MachineContainerName, Constants.AgentPartitionKeyPath)
                .GetAwaiter()
                .GetResult();

            database.CreateContainerIfNotExistsAsync(Constants.TelemetryContainerName, Constants.AgentPartitionKeyPath)
                .GetAwaiter()
                .GetResult();

            services.AddSingleton(cosmosClient);
        }

        // Register HttpClient for AlertProcessor
        services.AddHttpClient();
    })
    .Build();

host.Run();

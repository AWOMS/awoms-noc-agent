using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using AWOMS.NOC.Shared;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register Azure Table Storage client
        var tableStorageConnectionString =
            context.Configuration["TableStorageConnectionString"] ??
            context.Configuration["AzureWebJobsStorage"];

        if (!string.IsNullOrWhiteSpace(tableStorageConnectionString))
        {
            var tableServiceClient = new TableServiceClient(tableStorageConnectionString);

            tableServiceClient.CreateTableIfNotExistsAsync(Constants.MachinesTableName)
                .GetAwaiter()
                .GetResult();

            tableServiceClient.CreateTableIfNotExistsAsync(Constants.MetricSnapshotTableName)
                .GetAwaiter()
                .GetResult();

            tableServiceClient.CreateTableIfNotExistsAsync(Constants.MetricHistoryTableName)
                .GetAwaiter()
                .GetResult();

            services.AddSingleton(tableServiceClient);
        }

        // Register HttpClient for AlertProcessor
        services.AddHttpClient();
    })
    .Build();

host.Run();

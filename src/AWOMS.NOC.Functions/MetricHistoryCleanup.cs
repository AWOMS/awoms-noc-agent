using AWOMS.NOC.Shared;
using AWOMS.NOC.Shared.Models;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AWOMS.NOC.Functions;

public class MetricHistoryCleanup
{
    private readonly ILogger<MetricHistoryCleanup> _logger;
    private readonly TableServiceClient _tableServiceClient;

    public MetricHistoryCleanup(ILogger<MetricHistoryCleanup> logger, TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
    }

    [Function("MetricHistoryCleanup")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer)
    {
        var cutoffUtc = DateTime.UtcNow.AddYears(-1);
        var machinesTable = _tableServiceClient.GetTableClient(Constants.MachinesTableName);
        var historyTable = _tableServiceClient.GetTableClient(Constants.MetricHistoryTableName);

        var deletedCount = 0;

        try
        {
            await foreach (var machine in machinesTable.QueryAsync<TableMachineEntity>(m => m.PartitionKey == "machines"))
            {
                var actions = new List<TableTransactionAction>(100);

                await foreach (var historyEntity in historyTable.QueryAsync<TableMetricHistoryEntity>(
                    h => h.PartitionKey == machine.AgentId && h.MetricTimestamp < cutoffUtc))
                {
                    actions.Add(new TableTransactionAction(TableTransactionActionType.Delete, historyEntity));

                    if (actions.Count == 100)
                    {
                        await historyTable.SubmitTransactionAsync(actions);
                        deletedCount += actions.Count;
                        actions.Clear();
                    }
                }

                if (actions.Count > 0)
                {
                    await historyTable.SubmitTransactionAsync(actions);
                    deletedCount += actions.Count;
                }
            }

            _logger.LogInformation("Metric history cleanup completed. Deleted {Count} rows older than {CutoffUtc:O}", deletedCount, cutoffUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metric history cleanup failed");
        }
    }
}

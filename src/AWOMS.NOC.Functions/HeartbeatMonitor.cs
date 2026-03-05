using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using AWOMS.NOC.Shared.Models;
using AWOMS.NOC.Shared;
using System.Text.Json;

namespace AWOMS.NOC.Functions;

public class HeartbeatMonitor
{
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly TableServiceClient _tableServiceClient;

    public HeartbeatMonitor(ILogger<HeartbeatMonitor> logger, TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
    }

    [Function("HeartbeatMonitor")]
    [QueueOutput(Constants.AlertsQueueName)]
    public async Task<List<string>> Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("HeartbeatMonitor function triggered at: {Time}", DateTime.UtcNow);
        
        var alertMessages = new List<string>();

        try
        {
            // Get timeout value from configuration (default to 5 minutes)
            var heartbeatTimeoutMinutes = int.TryParse(Environment.GetEnvironmentVariable("HeartbeatTimeoutMinutes"), out var timeout) ? timeout : 5;

            var machineTable = _tableServiceClient.GetTableClient(Constants.MachinesTableName);
            var timeoutThreshold = DateTime.UtcNow.AddMinutes(-heartbeatTimeoutMinutes);

            await foreach (var machine in machineTable.QueryAsync<TableMachineEntity>(entity => entity.PartitionKey == "machines"))
            {
                var nowUtc = DateTime.UtcNow;
                var evaluation = EvaluateMachineHeartbeat(machine, timeoutThreshold, heartbeatTimeoutMinutes, nowUtc);

                if (evaluation.Alert is not null)
                {
                    alertMessages.Add(JsonSerializer.Serialize(evaluation.Alert));
                }

                if (evaluation.ShouldUpdate)
                {
                    machine.IsOnline = evaluation.NewIsOnline;
                    if (evaluation.LastAlertSentUtc is DateTime lastAlertSentUtc)
                    {
                        machine.LastAlertSent = lastAlertSentUtc;
                    }

                    await machineTable.UpdateEntityAsync(machine, machine.ETag, TableUpdateMode.Replace);
                }
            }

            _logger.LogInformation("Heartbeat check complete. Sent {Count} alerts", alertMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HeartbeatMonitor");
        }

        return alertMessages;
    }

    internal static HeartbeatEvaluation EvaluateMachineHeartbeat(
        TableMachineEntity machine,
        DateTime timeoutThreshold,
        int heartbeatTimeoutMinutes,
        DateTime nowUtc)
    {
        if (machine.LastHeartbeat < timeoutThreshold)
        {
            if (machine.IsOnline)
            {
                return new HeartbeatEvaluation(
                    ShouldUpdate: true,
                    NewIsOnline: false,
                    LastAlertSentUtc: nowUtc,
                    Alert: new AlertData
                    {
                        AgentId = machine.AgentId,
                        MachineName = machine.MachineName,
                        Severity = "Critical",
                        Category = "Heartbeat",
                        MetricName = "Heartbeat Timeout",
                        Message = $"Machine {machine.MachineName} has not reported for {heartbeatTimeoutMinutes} minutes",
                        CurrentValue = machine.LastHeartbeat,
                        ThresholdValue = heartbeatTimeoutMinutes,
                        Timestamp = nowUtc
                    });
            }

            return new HeartbeatEvaluation(ShouldUpdate: false, NewIsOnline: machine.IsOnline, LastAlertSentUtc: null, Alert: null);
        }

        if (!machine.IsOnline)
        {
            return new HeartbeatEvaluation(
                ShouldUpdate: true,
                NewIsOnline: true,
                LastAlertSentUtc: null,
                Alert: new AlertData
                {
                    AgentId = machine.AgentId,
                    MachineName = machine.MachineName,
                    Severity = "Warning",
                    Category = "Heartbeat",
                    MetricName = "Heartbeat Recovered",
                    Message = $"Machine {machine.MachineName} is back online",
                    CurrentValue = machine.LastHeartbeat,
                    Timestamp = nowUtc
                });
        }

        return new HeartbeatEvaluation(ShouldUpdate: false, NewIsOnline: machine.IsOnline, LastAlertSentUtc: null, Alert: null);
    }
}

internal sealed record HeartbeatEvaluation(bool ShouldUpdate, bool NewIsOnline, DateTime? LastAlertSentUtc, AlertData? Alert);

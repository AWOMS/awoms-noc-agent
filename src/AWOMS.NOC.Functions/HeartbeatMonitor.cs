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
            var machineTable = _tableServiceClient.GetTableClient(Constants.MachineTableName);
            await machineTable.CreateIfNotExistsAsync();

            var machines = machineTable.QueryAsync<MachineEntity>(filter: $"PartitionKey eq 'machines'");
            var timeoutThreshold = DateTime.UtcNow.AddMinutes(-Thresholds.HeartbeatTimeoutMinutes);

            await foreach (var machine in machines)
            {
                if (machine.LastHeartbeat < timeoutThreshold)
                {
                    // Machine is offline or hasn't reported
                    if (machine.IsOnline)
                    {
                        // First time detecting offline - send alert
                        _logger.LogWarning("Machine {MachineName} ({AgentId}) is offline. Last heartbeat: {LastHeartbeat}",
                            machine.MachineName, machine.AgentId, machine.LastHeartbeat);

                        var alert = new AlertData
                        {
                            AgentId = machine.AgentId,
                            MachineName = machine.MachineName,
                            Severity = "Critical",
                            Category = "Heartbeat",
                            MetricName = "Heartbeat Timeout",
                            Message = $"Machine {machine.MachineName} has not reported for {Thresholds.HeartbeatTimeoutMinutes} minutes",
                            CurrentValue = machine.LastHeartbeat,
                            ThresholdValue = Thresholds.HeartbeatTimeoutMinutes,
                            Timestamp = DateTime.UtcNow
                        };

                        alertMessages.Add(JsonSerializer.Serialize(alert));

                        // Update machine status to offline
                        machine.IsOnline = false;
                        machine.LastAlertSent = DateTime.UtcNow;
                        await machineTable.UpdateEntityAsync(machine, machine.ETag);
                    }
                    else
                    {
                        // Already marked as offline, don't send repeated alerts
                        _logger.LogInformation("Machine {MachineName} is still offline", machine.MachineName);
                    }
                }
                else if (!machine.IsOnline)
                {
                    // Machine came back online
                    _logger.LogInformation("Machine {MachineName} is back online", machine.MachineName);
                    machine.IsOnline = true;
                    await machineTable.UpdateEntityAsync(machine, machine.ETag);

                    // Optionally send recovery alert
                    var recoveryAlert = new AlertData
                    {
                        AgentId = machine.AgentId,
                        MachineName = machine.MachineName,
                        Severity = "Warning",
                        Category = "Heartbeat",
                        MetricName = "Heartbeat Recovered",
                        Message = $"Machine {machine.MachineName} is back online",
                        CurrentValue = machine.LastHeartbeat,
                        Timestamp = DateTime.UtcNow
                    };

                    alertMessages.Add(JsonSerializer.Serialize(recoveryAlert));
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
}

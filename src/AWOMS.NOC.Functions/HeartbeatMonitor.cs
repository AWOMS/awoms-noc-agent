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
        _logger.LogDebug("Timer schedule status - IsPastDue: {IsPastDue}", myTimer.IsPastDue);
        
        var alertMessages = new List<string>();

        try
        {
            // Get timeout value from configuration (default to 5 minutes)
            var heartbeatTimeoutMinutes = int.TryParse(Environment.GetEnvironmentVariable("HeartbeatTimeoutMinutes"), out var timeout) ? timeout : 5;
            _logger.LogDebug("Heartbeat timeout configuration: {TimeoutMinutes} minutes", heartbeatTimeoutMinutes);

            _logger.LogDebug("Attempting to connect to table: {TableName}", Constants.MachineTableName);
            var machineTable = _tableServiceClient.GetTableClient(Constants.MachineTableName);
            _logger.LogDebug("Creating table if not exists: {TableName}", Constants.MachineTableName);
            await machineTable.CreateIfNotExistsAsync();
            _logger.LogDebug("Successfully created/accessed table: {TableName}", Constants.MachineTableName);

            _logger.LogDebug("Querying machines from table with filter: PartitionKey eq 'machines'");
            var machines = machineTable.QueryAsync<MachineEntity>(filter: $"PartitionKey eq 'machines'");
            var timeoutThreshold = DateTime.UtcNow.AddMinutes(-heartbeatTimeoutMinutes);
            _logger.LogDebug("Heartbeat timeout threshold calculated: {TimeoutThreshold}", timeoutThreshold);

            int machineCount = 0;
            await foreach (var machine in machines)
            {
                machineCount++;
                _logger.LogDebug("Processing machine: {MachineName} (AgentId: {AgentId}), LastHeartbeat: {LastHeartbeat}, IsOnline: {IsOnline}", 
                    machine.MachineName, machine.AgentId, machine.LastHeartbeat, machine.IsOnline);
                
                if (machine.LastHeartbeat < timeoutThreshold)
                {
                    // Machine is offline or hasn't reported
                    if (machine.IsOnline)
                    {
                        // First time detecting offline - send alert
                        _logger.LogWarning("Machine {MachineName} ({AgentId}) is offline. Last heartbeat: {LastHeartbeat}",
                            machine.MachineName, machine.AgentId, machine.LastHeartbeat);
                        _logger.LogDebug("Creating alert for offline machine: {MachineName}", machine.MachineName);
                        
                        var alert = new AlertData
                        {
                            AgentId = machine.AgentId,
                            MachineName = machine.MachineName,
                            Severity = "Critical",
                            Category = "Heartbeat",
                            MetricName = "Heartbeat Timeout",
                            Message = $"Machine {machine.MachineName} has not reported for {heartbeatTimeoutMinutes} minutes",
                            CurrentValue = machine.LastHeartbeat,
                            ThresholdValue = heartbeatTimeoutMinutes,
                            Timestamp = DateTime.UtcNow
                        };

                        var serializedAlert = JsonSerializer.Serialize(alert);
                        alertMessages.Add(serializedAlert);
                        _logger.LogDebug("Alert queued for machine {MachineName}: {Alert}", machine.MachineName, serializedAlert);

                        // Update machine status to offline
                        _logger.LogDebug("Updating machine {MachineName} status to offline in table", machine.MachineName);
                        machine.IsOnline = false;
                        machine.LastAlertSent = DateTime.UtcNow;
                        await machineTable.UpdateEntityAsync(machine, machine.ETag);
                        _logger.LogDebug("Successfully updated machine {MachineName} status to offline", machine.MachineName);
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
                    _logger.LogDebug("Updating machine {MachineName} status to online in table", machine.MachineName);
                    machine.IsOnline = true;
                    await machineTable.UpdateEntityAsync(machine, machine.ETag);
                    _logger.LogDebug("Successfully updated machine {MachineName} status to online", machine.MachineName);

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

                    var serializedRecoveryAlert = JsonSerializer.Serialize(recoveryAlert);
                    alertMessages.Add(serializedRecoveryAlert);
                    _logger.LogDebug("Recovery alert queued for machine {MachineName}: {Alert}", machine.MachineName, serializedRecoveryAlert);
                }
                else
                {
                    _logger.LogDebug("Machine {MachineName} is online and healthy. Last heartbeat: {LastHeartbeat}", 
                        machine.MachineName, machine.LastHeartbeat);
                }
            }

            _logger.LogInformation("Heartbeat check complete. Processed {MachineCount} machines, sent {AlertCount} alerts", machineCount, alertMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HeartbeatMonitor. Stack trace: {StackTrace}", ex.StackTrace);
            _logger.LogDebug("Exception type: {ExceptionType}, Message: {Message}", ex.GetType().Name, ex.Message);
            throw;
        }

        return alertMessages;
    }
}

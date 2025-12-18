using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using AWOMS.NOC.Shared.Models;
using System.Text.Json;
using System.Net.Http;
using System.Text;

namespace AWOMS.NOC.Functions;

public class AlertProcessor
{
    private readonly ILogger<AlertProcessor> _logger;
    private readonly HttpClient _httpClient;

    public AlertProcessor(ILogger<AlertProcessor> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    [Function("AlertProcessor")]
    public async Task Run([QueueTrigger(AWOMS.NOC.Shared.Constants.AlertsQueueName)] string alertMessage)
    {
        _logger.LogInformation("Processing alert from queue");

        try
        {
            var alert = JsonSerializer.Deserialize<AlertData>(alertMessage);
            if (alert == null)
            {
                _logger.LogWarning("Failed to deserialize alert message");
                return;
            }

            _logger.LogInformation("Alert: {Severity} - {Category}/{MetricName} for {MachineName}",
                alert.Severity, alert.Category, alert.MetricName, alert.MachineName);

            // Send to configured alert channels
            await SendEmailAlert(alert);
            await SendTeamsAlert(alert);
            await SendWebhookAlert(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing alert");
            throw; // Re-throw to let Azure Functions retry
        }
    }

    private async Task SendEmailAlert(AlertData alert)
    {
        try
        {
            var smtpEnabled = Environment.GetEnvironmentVariable("EmailAlerts_Enabled");
            if (smtpEnabled != "true")
            {
                _logger.LogDebug("Email alerts not enabled");
                return;
            }

            var emailTo = Environment.GetEnvironmentVariable("EmailAlerts_To");
            if (string.IsNullOrEmpty(emailTo))
            {
                _logger.LogWarning("Email alerts enabled but no recipient configured");
                return;
            }

            // In production, you would integrate with SendGrid or SMTP here
            _logger.LogInformation("Would send email alert to {EmailTo}: {Message}", emailTo, alert.Message);
            
            // Example SendGrid integration (requires SendGrid NuGet package):
            // var apiKey = Environment.GetEnvironmentVariable("SendGrid_ApiKey");
            // var client = new SendGridClient(apiKey);
            // var from = new EmailAddress("noc-alerts@yourdomain.com", "AWOMS NOC");
            // var to = new EmailAddress(emailTo);
            // var subject = $"[{alert.Severity}] {alert.Category} Alert: {alert.MachineName}";
            // var plainTextContent = alert.Message;
            // var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, null);
            // await client.SendEmailAsync(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email alert");
        }
    }

    private async Task SendTeamsAlert(AlertData alert)
    {
        try
        {
            var teamsWebhookUrl = Environment.GetEnvironmentVariable("TeamsAlerts_WebhookUrl");
            if (string.IsNullOrEmpty(teamsWebhookUrl))
            {
                _logger.LogDebug("Teams webhook not configured");
                return;
            }

            var color = alert.Severity == "Critical" ? "FF0000" : "FFA500";
            var teamsMessage = new
            {
                type = "message",
                attachments = new object[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            body = new object[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    size = "Large",
                                    weight = "Bolder",
                                    text = $"[{alert.Severity}] {alert.Category} Alert"
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = $"**Machine:** {alert.MachineName}",
                                    wrap = true
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = $"**Metric:** {alert.MetricName}",
                                    wrap = true
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = $"**Message:** {alert.Message}",
                                    wrap = true
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = $"**Time:** {alert.Timestamp:yyyy-MM-dd HH:mm:ss} UTC",
                                    wrap = true,
                                    isSubtle = true
                                }
                            },
                            schema = "http://adaptivecards.io/schemas/adaptive-card.json",
                            version = "1.4",
                            msteams = new
                            {
                                width = "Full"
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(teamsMessage);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(teamsWebhookUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams alert sent successfully");
            }
            else
            {
                _logger.LogWarning("Failed to send Teams alert. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams alert");
        }
    }

    private async Task SendWebhookAlert(AlertData alert)
    {
        try
        {
            var webhookUrl = Environment.GetEnvironmentVariable("GenericWebhook_Url");
            if (string.IsNullOrEmpty(webhookUrl))
            {
                _logger.LogDebug("Generic webhook not configured");
                return;
            }

            var json = JsonSerializer.Serialize(alert);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook alert sent successfully");
            }
            else
            {
                _logger.LogWarning("Failed to send webhook alert. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending webhook alert");
        }
    }
}

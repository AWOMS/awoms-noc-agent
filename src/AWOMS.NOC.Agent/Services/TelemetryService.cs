using AWOMS.NOC.Shared.Models;
using AWOMS.NOC.Shared;
using System.Text;
using System.Text.Json;
using Polly;
using Polly.Extensions.Http;

namespace AWOMS.NOC.Agent.Services;

public class TelemetryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelemetryService> _logger;
    private readonly AgentConfiguration _configuration;

    public TelemetryService(HttpClient httpClient, ILogger<TelemetryService> logger, AgentConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> SendTelemetryAsync(TelemetryPayload payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_configuration.ApiEndpoint}/api/telemetry")
            {
                Content = content
            };
            request.Headers.Add(Constants.ApiKeyHeaderName, _configuration.ApiKey);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telemetry sent successfully");
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to send telemetry. Status: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending telemetry");
            return false;
        }
    }

    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"Retry {retryCount} after {timespan.TotalSeconds} seconds due to {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                });
    }
}

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
            _logger.LogDebug("Starting telemetry send. AgentId: {AgentId}, Endpoint: {Endpoint}", 
                payload.AgentId, _configuration.ApiEndpoint);
            
            var json = JsonSerializer.Serialize(payload);
            _logger.LogDebug("Serialized telemetry payload: {Payload}", json);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_configuration.ApiEndpoint}/api/telemetry")
            {
                Content = content
            };
            request.Headers.Add(Constants.ApiKeyHeaderName, _configuration.ApiKey);
            _logger.LogDebug("Request prepared. URL: {Url}, Headers configured", request.RequestUri);

            _logger.LogDebug("Sending HTTP request to telemetry endpoint");
            var response = await _httpClient.SendAsync(request);
            _logger.LogDebug("Response received. Status code: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telemetry sent successfully. Status: {StatusCode}", response.StatusCode);
                _logger.LogDebug("Telemetry data accepted by endpoint for AgentId: {AgentId}", payload.AgentId);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to send telemetry. Status: {StatusCode}, Reason: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                _logger.LogDebug("Response content: {Content}", await response.Content.ReadAsStringAsync());
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending telemetry. Exception type: {ExceptionType}", ex.GetType().Name);
            _logger.LogDebug("Exception details - Message: {Message}, StackTrace: {StackTrace}", 
                ex.Message, ex.StackTrace);
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
                    string reason = outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString() ?? "Unknown";
                    Console.WriteLine($"[RETRY] Attempt {retryCount} after {timespan.TotalSeconds} seconds due to {reason}");
                });
    }
}

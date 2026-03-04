using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Services;
using AWOMS.NOC.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http;
using System.Text;

namespace AWOMS.NOC.Agent.Tests.Services;

public class TelemetryServiceTests
{
    [Fact]
    public async Task SendTelemetryAsync_WithSuccessResponse_ShouldReturnTrueAndIncludeApiKeyHeader()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);
        var config = new AgentConfiguration
        {
            ApiEndpoint = "https://central.example",
            ApiKey = "test-key"
        };

        var service = new TelemetryService(httpClient, NullLogger<TelemetryService>.Instance, config);
        var result = await service.SendTelemetryAsync(MakePayload());

        result.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.RequestUri!.ToString().Should().Be("https://central.example/api/telemetry");
        request.Headers.Should().Contain(h => h.Key == "x-api-key" && h.Value.Contains("test-key"));
    }

    [Fact]
    public async Task SendTelemetryAsync_WithNonSuccessResponse_ShouldReturnFalse()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var httpClient = new HttpClient(handler);
        var config = new AgentConfiguration
        {
            ApiEndpoint = "https://central.example",
            ApiKey = "test-key"
        };

        var service = new TelemetryService(httpClient, NullLogger<TelemetryService>.Instance, config);
        var result = await service.SendTelemetryAsync(MakePayload());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendTelemetryAsync_WhenHandlerThrows_ShouldReturnFalse()
    {
        var handler = new CapturingHttpMessageHandler(_ => throw new HttpRequestException("network error"));
        var httpClient = new HttpClient(handler);
        var config = new AgentConfiguration
        {
            ApiEndpoint = "https://central.example",
            ApiKey = "test-key"
        };

        var service = new TelemetryService(httpClient, NullLogger<TelemetryService>.Instance, config);
        var result = await service.SendTelemetryAsync(MakePayload());

        result.Should().BeFalse();
    }

    private static TelemetryPayload MakePayload()
    {
        return new TelemetryPayload
        {
            AgentId = "agent-1",
            MachineName = "machine-1",
            DomainName = "domain",
            IpAddress = "10.0.0.1",
            OsVersion = "Windows",
            Timestamp = DateTime.UtcNow,
            Metrics =
            [
                new MetricData
                {
                    Category = "CPU",
                    Name = "CPU Usage",
                    Value = 25.2,
                    Unit = "%",
                    Timestamp = DateTime.UtcNow
                }
            ],
            Alerts = []
        };
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content is null
                    ? null
                    : new StringContent(await request.Content.ReadAsStringAsync(cancellationToken), Encoding.UTF8, request.Content.Headers.ContentType?.MediaType)
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Requests.Add(clone);
            return _responder(request);
        }
    }
}
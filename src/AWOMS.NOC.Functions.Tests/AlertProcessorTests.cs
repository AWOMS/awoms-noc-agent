using AWOMS.NOC.Shared.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AWOMS.NOC.Functions.Tests;

public class AlertProcessorTests
{
    [Fact]
    public async Task Run_WithTeamsWebhookConfigured_ShouldPostToTeams()
    {
        Environment.SetEnvironmentVariable("EmailAlerts_Enabled", "false");
        Environment.SetEnvironmentVariable("TeamsAlerts_WebhookUrl", "https://teams.example/webhook");
        Environment.SetEnvironmentVariable("GenericWebhook_Url", null);

        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var processor = new AWOMS.NOC.Functions.AlertProcessor(NullLogger<AWOMS.NOC.Functions.AlertProcessor>.Instance, httpClientFactory.Object);
        var alert = new AlertData
        {
            AgentId = "a1",
            MachineName = "m1",
            Severity = "Critical",
            Category = "CPU",
            MetricName = "CPU Usage",
            Message = "High CPU",
            Timestamp = DateTime.UtcNow
        };

        await processor.Run(JsonSerializer.Serialize(alert));

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://teams.example/webhook");
    }

    [Fact]
    public async Task Run_WithGenericWebhookConfigured_ShouldPostToWebhook()
    {
        Environment.SetEnvironmentVariable("EmailAlerts_Enabled", "false");
        Environment.SetEnvironmentVariable("TeamsAlerts_WebhookUrl", null);
        Environment.SetEnvironmentVariable("GenericWebhook_Url", "https://webhook.example/alerts");

        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var processor = new AWOMS.NOC.Functions.AlertProcessor(NullLogger<AWOMS.NOC.Functions.AlertProcessor>.Instance, httpClientFactory.Object);
        var alert = new AlertData
        {
            AgentId = "a1",
            MachineName = "m1",
            Severity = "Warning",
            Category = "Disk",
            MetricName = "Free Space (C:)",
            Message = "Low disk",
            Timestamp = DateTime.UtcNow
        };

        await processor.Run(JsonSerializer.Serialize(alert));

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().Be("https://webhook.example/alerts");
    }

    [Fact]
    public async Task Run_WithAllChannelsDisabled_ShouldNotSendRequests()
    {
        Environment.SetEnvironmentVariable("EmailAlerts_Enabled", "false");
        Environment.SetEnvironmentVariable("TeamsAlerts_WebhookUrl", null);
        Environment.SetEnvironmentVariable("GenericWebhook_Url", null);

        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var processor = new AWOMS.NOC.Functions.AlertProcessor(NullLogger<AWOMS.NOC.Functions.AlertProcessor>.Instance, httpClientFactory.Object);
        var alert = new AlertData
        {
            AgentId = "a1",
            MachineName = "m1",
            Severity = "Warning",
            Category = "Memory",
            MetricName = "Memory Usage",
            Message = "High memory",
            Timestamp = DateTime.UtcNow
        };

        await processor.Run(JsonSerializer.Serialize(alert));

        handler.Requests.Should().BeEmpty();
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cloned = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content is null ? null : new StringContent(await request.Content.ReadAsStringAsync(cancellationToken), Encoding.UTF8, request.Content.Headers.ContentType?.MediaType)
            };

            foreach (var header in request.Headers)
            {
                cloned.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Requests.Add(cloned);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
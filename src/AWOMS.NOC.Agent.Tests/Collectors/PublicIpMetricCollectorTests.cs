using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Collectors;
using FluentAssertions;
using System.Net;
using System.Net.Http;

namespace AWOMS.NOC.Agent.Tests.Collectors;

public class PublicIpMetricCollectorTests
{
    [Fact]
    public async Task CollectAsync_WithSuccessfulResponse_ShouldReturnPublicIpMetric()
    {
        var handler = new InlineHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("8.8.4.4")
        });
        var collector = new PublicIpMetricCollector(
            new SingleHttpClientFactory(new HttpClient(handler)),
            new AgentConfiguration { PublicIpUrl = "https://api.ipify.org/" });

        var metrics = await collector.CollectAsync();

        metrics.Should().ContainSingle(m =>
            m.Category == "Network" &&
            m.Name == "Public IP Address" &&
            (string?)m.Value == "8.8.4.4");
    }

    [Fact]
    public async Task CollectAsync_WhenHttpFails_ShouldReturnErrorMetric()
    {
        var handler = new InlineHttpHandler(_ => throw new HttpRequestException("failed"));
        var collector = new PublicIpMetricCollector(
            new SingleHttpClientFactory(new HttpClient(handler)),
            new AgentConfiguration { PublicIpUrl = "https://api.ipify.org/" });

        var metrics = await collector.CollectAsync();

        metrics.Should().ContainSingle(m =>
            m.Name == "Public IP Collection Error" &&
            m.Unit == "error");
    }

    private sealed class SingleHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public SingleHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class InlineHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public InlineHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
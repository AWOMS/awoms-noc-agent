using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;

namespace AWOMS.NOC.Functions.Tests;

public class TelemetryIngestionTests
{
    [Fact]
    public async Task Run_WithoutApiKeyHeader_ShouldReturnUnauthorized()
    {
        Environment.SetEnvironmentVariable("ApiKey", "expected-key");

        var request = CreateRequest(
            bodyJson: "{}",
            headers: new HttpHeadersCollection());

        var ingestion = new AWOMS.NOC.Functions.TelemetryIngestion(
            NullLogger<AWOMS.NOC.Functions.TelemetryIngestion>.Instance,
            Mock.Of<CosmosClient>());

        var result = await ingestion.Run(request);

        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Run_WithInvalidPayload_ShouldReturnBadRequest()
    {
        Environment.SetEnvironmentVariable("ApiKey", "expected-key");

        var headers = new HttpHeadersCollection();
        headers.Add("x-api-key", "expected-key");

        var request = CreateRequest(
            bodyJson: "{\"MachineName\":\"no-agent-id\"}",
            headers: headers);

        var ingestion = new AWOMS.NOC.Functions.TelemetryIngestion(
            NullLogger<AWOMS.NOC.Functions.TelemetryIngestion>.Instance,
            Mock.Of<CosmosClient>());

        var result = await ingestion.Run(request);

        result.HttpResponse.Should().NotBeNull();
        result.HttpResponse!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static HttpRequestData CreateRequest(string bodyJson, HttpHeadersCollection headers)
    {
        var functionContext = new Mock<FunctionContext>();
        var request = new Mock<HttpRequestData>(functionContext.Object);

        request.SetupGet(r => r.Headers).Returns(headers);
        request.SetupGet(r => r.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(bodyJson)));
        request.SetupGet(r => r.Url).Returns(new Uri("https://localhost/api/telemetry"));
        request.SetupGet(r => r.Method).Returns("POST");

        request
            .Setup(r => r.CreateResponse())
            .Returns(() =>
            {
                var response = new Mock<HttpResponseData>(functionContext.Object);
                response.SetupProperty(r => r.StatusCode, HttpStatusCode.OK);
                response.SetupGet(r => r.Headers).Returns(new HttpHeadersCollection());
                response.SetupProperty(r => r.Body, new MemoryStream());
                response.SetupGet(r => r.Cookies).Returns(Mock.Of<HttpCookies>());
                return response.Object;
            });

        return request.Object;
    }
}
using System.Net;
using MyAccountingApp.Api.Http;

namespace MyAccountingApp.Api.Tests;

public class FxRetryHandlerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public StubHandler(params HttpResponseMessage[] responses)
        {
            this._responses = new Queue<HttpResponseMessage>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(this._responses.Dequeue());
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(FxRetryHandler handler, StubHandler stub)
    {
        handler.InnerHandler = stub;
        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.frankfurter.dev/v1/latest");
        HttpMessageInvoker invoker = new(handler);
        return await invoker.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task ShouldRetry_WhenTransientThenSuccess()
    {
        // Arrange
        FxRetryHandler handler = new();
        StubHandler stub = new(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        using HttpResponseMessage response = await SendAsync(handler, stub);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, stub.CallCount);
    }

    [Fact]
    public async Task ShouldNotRetry_WhenNotFound()
    {
        // Arrange
        FxRetryHandler handler = new();
        StubHandler stub = new(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        using HttpResponseMessage response = await SendAsync(handler, stub);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task ShouldNotRetry_WhenQuotaExceeded()
    {
        // Arrange
        FxRetryHandler handler = new();
        StubHandler stub = new(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        // Act
        using HttpResponseMessage response = await SendAsync(handler, stub);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task ShouldReturnLastResponse_WhenAlwaysFailing()
    {
        // Arrange
        FxRetryHandler handler = new();
        StubHandler stub = new(
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            new HttpResponseMessage(HttpStatusCode.BadGateway));

        // Act
        using HttpResponseMessage response = await SendAsync(handler, stub);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(4, stub.CallCount);
    }
}

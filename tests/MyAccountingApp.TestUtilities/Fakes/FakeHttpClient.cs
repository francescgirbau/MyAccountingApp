using System.Net;
using Moq;
using Moq.Protected;

namespace MyAccountingApp.TestUtilities.Fakes;
public static class FakeHttpClient
{
    public static HttpClient CreateFakeHttpClient(string responseContent, HttpStatusCode statusCode)
    {
        Mock<HttpMessageHandler> handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent),
            });

        return new HttpClient(handlerMock.Object);
    }
}

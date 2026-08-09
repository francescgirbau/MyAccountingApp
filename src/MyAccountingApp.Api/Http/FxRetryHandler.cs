using System.Net;
using Polly;
using Polly.Retry;

namespace MyAccountingApp.Api.Http;

public sealed class FxRetryHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public FxRetryHandler()
    {
        this._pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<OperationCanceledException>()
                    .HandleResult(response =>
                        response.StatusCode == HttpStatusCode.RequestTimeout ||
                        (int)response.StatusCode >= 500),
            })
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await this._pipeline.ExecuteAsync(
            async token => await base.SendAsync(request, token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }
}

namespace ZitadelSDK.UnitTests.TestHelpers;

internal sealed class QueueHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public int CallCount { get; private set; }

    public void EnqueueResponse(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No response configured for request.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

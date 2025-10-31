using Grpc.Core;

namespace ZitadelSDK.UnitTests.TestHelpers;

internal static class CallCredentialsTestHelper
{
    public static async Task<Metadata> InvokeAsync(CallCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var metadata = new Metadata();
        var interceptor = ExtractInterceptor(credentials);
        var context = new AuthInterceptorContext("service", "method");

        await interceptor(context, metadata).ConfigureAwait(false);
        return metadata;
    }

    private static AsyncAuthInterceptor ExtractInterceptor(CallCredentials credentials)
    {
        var interceptorField = credentials.GetType().GetField(
            "interceptor",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (interceptorField?.GetValue(credentials) is AsyncAuthInterceptor interceptor)
        {
            return interceptor;
        }

        throw new InvalidOperationException("Unable to extract AsyncAuthInterceptor from CallCredentials.");
    }
}

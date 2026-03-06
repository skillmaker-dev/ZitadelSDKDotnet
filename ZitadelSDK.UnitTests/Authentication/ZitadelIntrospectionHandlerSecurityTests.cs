using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reflection;
using System.Text.Encodings.Web;
using ZitadelSDK.Authentication;

namespace ZitadelSDK.UnitTests.Authentication;

public class ZitadelIntrospectionHandlerSecurityTests
{
    [Fact]
    public void GetIntrospectionEndpoint_WithHttpIntrospectionEndpoint_Throws()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            IntrospectionEndpoint = "http://example.com/oauth/v2/introspect"
        };

        var handler = CreateInitializedHandler(options);

        var exception = Assert.Throws<InvalidOperationException>(() => InvokePrivate<string>(handler, "GetIntrospectionEndpoint"));
        Assert.Contains("must use HTTPS", exception.Message);
    }

    [Fact]
    public void GetIntrospectionEndpoint_WithHttpAuthority_Throws()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "http://example.com"
        };

        var handler = CreateInitializedHandler(options);

        var exception = Assert.Throws<InvalidOperationException>(() => InvokePrivate<string>(handler, "GetIntrospectionEndpoint"));
        Assert.Contains("must use HTTPS", exception.Message);
    }

    [Fact]
    public void GetIntrospectionEndpoint_WithHttpsAuthority_ReturnsExpectedPath()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com/"
        };

        var handler = CreateInitializedHandler(options);

        var endpoint = InvokePrivate<string>(handler, "GetIntrospectionEndpoint");

        Assert.Equal("https://example.com/oauth/v2/introspect", endpoint);
    }

    [Fact]
    public void GetCacheKey_UsesHashedTokenAndPrefix()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            CacheKeyPrefix = "zitadel:test:"
        };

        var handler = CreateInitializedHandler(options);

        var token = "sensitive-token-value";
        var cacheKey = InvokePrivate<string>(handler, "GetCacheKey", token);

        Assert.StartsWith("zitadel:test:", cacheKey);
        Assert.DoesNotContain(token, cacheKey);

        var hashPart = cacheKey["zitadel:test:".Length..];
        Assert.Matches("^[A-F0-9]{64}$", hashPart);

        var cacheKeySecond = InvokePrivate<string>(handler, "GetCacheKey", token);
        Assert.Equal(cacheKey, cacheKeySecond);
    }

    private static object CreateInitializedHandler(ZitadelIntrospectionOptions options)
    {
        var assembly = typeof(ZitadelIntrospectionOptions).Assembly;
        var handlerType = assembly.GetType("ZitadelSDK.Authentication.ZitadelIntrospectionHandler", throwOnError: true)!;

        var optionsMonitor = new StaticOptionsMonitor(options);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var handler = Activator.CreateInstance(
            handlerType,
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            httpClientFactory,
            memoryCache)!;

        var authenticationHandler = (IAuthenticationHandler)handler;
        var scheme = new AuthenticationScheme("ZITADEL", "ZITADEL", handlerType);
        authenticationHandler.InitializeAsync(scheme, new DefaultHttpContext()).GetAwaiter().GetResult();

        return handler;
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        try
        {
            return (T)method.Invoke(instance, args)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private sealed class StaticOptionsMonitor(ZitadelIntrospectionOptions options) : IOptionsMonitor<ZitadelIntrospectionOptions>
    {
        public ZitadelIntrospectionOptions CurrentValue => options;

        public ZitadelIntrospectionOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<ZitadelIntrospectionOptions, string?> listener)
        {
            return null;
        }
    }
}

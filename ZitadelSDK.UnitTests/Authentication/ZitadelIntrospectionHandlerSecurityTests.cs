using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Reflection;
using System.Text;
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
    public void GetIntrospectionEndpoint_WithHttpIntrospectionEndpointAndInsecureTransportAllowed_ReturnsExpectedPath()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            IntrospectionEndpoint = "http://example.com/oauth/v2/introspect",
            AllowInsecureTransport = true
        };

        var handler = CreateInitializedHandler(options);

        var endpoint = InvokePrivate<string>(handler, "GetIntrospectionEndpoint");

        Assert.Equal("http://example.com/oauth/v2/introspect", endpoint);
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
    public void GetIntrospectionEndpoint_WithHttpAuthorityAndInsecureTransportAllowed_ReturnsExpectedPath()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "http://example.com",
            AllowInsecureTransport = true
        };

        var handler = CreateInitializedHandler(options);

        var endpoint = InvokePrivate<string>(handler, "GetIntrospectionEndpoint");

        Assert.Equal("http://example.com/oauth/v2/introspect", endpoint);
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

    [Fact]
    public void Options_Validate_WithAuthorityAndClientId_DoesNotThrow()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            ClientId = "my-client-id"
        };

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Options_Validate_WithAuthorityAndJwtProfile_DoesNotThrow()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            JwtProfile = new JwtProfileConfig
            {
                KeyId = "key-id",
                Key = "some-key",
                ClientId = "client-id"
            }
        };

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Options_Defaults_IncludeInactiveTokenRetryPolicy()
    {
        var options = new ZitadelIntrospectionOptions();

        Assert.Equal(1, options.InactiveTokenRetryCount);
        Assert.Equal(TimeSpan.FromMilliseconds(150), options.InactiveTokenRetryDelay);
    }

    [Fact]
    public async Task AuthenticateAsync_WithCachingDisabled_LogsCachingDisabledInsteadOfCacheMiss()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            EnableCaching = false
        };

        var loggerFactory = new ListLoggerFactory();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(new StaticHttpMessageHandler(CreateActiveResponse())));

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer opaque-token";

        var handler = CreateInitializedHandler(options, loggerFactory, httpClientFactory, context);

        var result = await ((IAuthenticationHandler)handler).AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Contains(loggerFactory.Messages, message => message.Contains("Caching disabled. Calling introspection endpoint.", StringComparison.Ordinal));
        Assert.DoesNotContain(loggerFactory.Messages, message => message.Contains("Cache miss. Calling introspection endpoint.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthenticateAsync_SanitizesRequestPathBeforeLogging()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            ClientId = "client-id"
        };

        var loggerFactory = new ListLoggerFactory();
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/\r\nforged");

        var handler = CreateInitializedHandler(options, loggerFactory, httpContext: context);

        var result = await ((IAuthenticationHandler)handler).AuthenticateAsync();

        Assert.False(result.Succeeded);
        var startMessage = Assert.Single(
            loggerFactory.Messages,
            message => message.Contains("Starting authentication for request", StringComparison.Ordinal));
        Assert.Contains("/\\r\\nforged", startMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("/\r\nforged", startMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_Validate_MissingAuthority_Throws()
    {
        var options = new ZitadelIntrospectionOptions
        {
            ClientId = "my-client-id"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Authority", exception.Message);
    }

    [Fact]
    public void Options_Validate_MissingClientCredentials_Throws()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("JwtProfile or ClientId", exception.Message);
    }

    [Fact]
    public void Options_Validate_NegativeInactiveTokenRetryCount_Throws()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            ClientId = "my-client-id",
            InactiveTokenRetryCount = -1
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("InactiveTokenRetryCount", exception.Message);
    }

    [Fact]
    public void Options_Validate_NegativeInactiveTokenRetryDelay_Throws()
    {
        var options = new ZitadelIntrospectionOptions
        {
            Authority = "https://example.com",
            ClientId = "my-client-id",
            InactiveTokenRetryDelay = TimeSpan.FromMilliseconds(-1)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("InactiveTokenRetryDelay", exception.Message);
    }

    private static object CreateInitializedHandler(
        ZitadelIntrospectionOptions options,
        ILoggerFactory? loggerFactory = null,
        IHttpClientFactory? httpClientFactory = null,
        HttpContext? httpContext = null,
        IMemoryCache? memoryCache = null)
    {
        var assembly = typeof(ZitadelIntrospectionOptions).Assembly;
        var handlerType = assembly.GetType("ZitadelSDK.Authentication.ZitadelIntrospectionHandler", throwOnError: true)!;

        var optionsMonitor = new StaticOptionsMonitor(options);
        httpClientFactory ??= Substitute.For<IHttpClientFactory>();
        memoryCache ??= new MemoryCache(new MemoryCacheOptions());
        loggerFactory ??= NullLoggerFactory.Instance;

        var handler = Activator.CreateInstance(
            handlerType,
            optionsMonitor,
            loggerFactory,
            UrlEncoder.Default,
            httpClientFactory,
            memoryCache)!;

        var authenticationHandler = (IAuthenticationHandler)handler;
        var scheme = new AuthenticationScheme("ZITADEL", "ZITADEL", handlerType);
        authenticationHandler.InitializeAsync(scheme, httpContext ?? new DefaultHttpContext()).GetAwaiter().GetResult();

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

    private static HttpResponseMessage CreateActiveResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"active\":true,\"sub\":\"user-1\"}", Encoding.UTF8, "application/json")
        };
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

    private sealed class StaticHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class ListLoggerFactory : ILoggerFactory
    {
        public List<string> Messages { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new ListLogger(Messages);
        }

        public void Dispose()
        {
        }
    }

    private sealed class ListLogger(List<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

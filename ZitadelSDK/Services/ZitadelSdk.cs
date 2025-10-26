using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using ZitadelSDK.Authentication;

namespace ZitadelSDK.Services;

/// <summary>
/// Provides access to shared ZITADEL gRPC channel resources and clients.
/// </summary>
public interface IZitadelSdk : IDisposable
{
    /// <summary>
    /// Retrieves a cached gRPC client or creates a new one backed by the shared channel.
    /// </summary>
    /// <typeparam name="TClient">The gRPC client type.</typeparam>
    /// <returns>The requested gRPC client instance.</returns>
    TClient GetClient<TClient>() where TClient : ClientBase<TClient>;

    /// <summary>
    /// Gets the shared gRPC channel used for all ZITADEL calls.
    /// </summary>
    GrpcChannel Channel { get; }

    /// <summary>
    /// Gets the call invoker derived from the shared channel.
    /// </summary>
    CallInvoker CallInvoker { get; }
}

/// <summary>
/// Default implementation of <see cref="IZitadelSdk"/> that manages a shared gRPC channel and client cache.
/// </summary>
public sealed class ZitadelSdk : IZitadelSdk
{
    private readonly ILogger<ZitadelSdk> _logger;
    private readonly GrpcChannel _channel;
    private readonly CallInvoker _callInvoker;
    private readonly ConcurrentDictionary<Type, object> _clients = new();
    private readonly ZitadelClientOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZitadelSdk"/> class.
    /// </summary>
    /// <param name="optionsAccessor">Accessor for configured ZITADEL options.</param>
    /// <param name="credentialProvider">Provider responsible for creating call credentials.</param>
    /// <param name="logger">Logger used for operational diagnostics.</param>
    public ZitadelSdk(
        IOptions<ZitadelClientOptions> optionsAccessor,
        IZitadelCredentialProvider credentialProvider,
        ILogger<ZitadelSdk> logger)
    {
        ArgumentNullException.ThrowIfNull(optionsAccessor);
        ArgumentNullException.ThrowIfNull(credentialProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        _options = optionsAccessor.Value ?? throw new InvalidOperationException("Zitadel options are not configured.");
        ValidateOptions(_options);

        var channelCredentials = BuildCredentials(_options, credentialProvider);

        _channel = GrpcChannel.ForAddress(_options.Authority, new GrpcChannelOptions
        {
            Credentials = channelCredentials
        });

        _callInvoker = _channel.CreateCallInvoker();
    }

    /// <inheritdoc />
    public GrpcChannel Channel => _channel;

    /// <inheritdoc />
    public CallInvoker CallInvoker => _callInvoker;

    /// <inheritdoc />
    public TClient GetClient<TClient>() where TClient : ClientBase<TClient>
    {
        ThrowIfDisposed();

        var clientType = typeof(TClient);

        // Fast path: return cached client if it exists
        if (_clients.TryGetValue(clientType, out var cached))
        {
            return (TClient)cached;
        }

        // Create new client using the generic constructor
        // All gRPC clients have a constructor that takes CallInvoker
        var client = (TClient)Activator.CreateInstance(clientType, _callInvoker)!;

        // Cache it for next time
        _clients.TryAdd(clientType, client);

        return client;
    }

    /// <summary>
    /// Gets or creates a gRPC client of the specified type using the configured channel.
    /// This overload is useful for dependency injection scenarios where the type is not known at compile time.
    /// </summary>
    /// <param name="clientType">The type of the gRPC client to create.</param>
    /// <returns>A gRPC client instance.</returns>
    public ClientBase GetClient<TClientBase>(Type clientType) where TClientBase : ClientBase
    {
        ThrowIfDisposed();

        if (!clientType.IsSubclassOf(typeof(ClientBase)))
        {
            throw new ArgumentException(
                $"Type {clientType.FullName} must inherit from ClientBase.",
                nameof(clientType));
        }

        // Fast path: return cached client if it exists
        if (_clients.TryGetValue(clientType, out var cached))
        {
            return (ClientBase)cached;
        }

        // Create new client - all gRPC clients have constructor taking CallInvoker
        var client = (ClientBase)Activator.CreateInstance(clientType, _callInvoker)!;

        // Cache it for next time
        _clients.TryAdd(clientType, client);

        return client;
    }

    private static ChannelCredentials BuildCredentials(
        ZitadelClientOptions options,
        IZitadelCredentialProvider credentialProvider)
    {
        var callCredentials = credentialProvider.CreateCallCredentials(options.Authority);
        return ChannelCredentials.Create(new SslCredentials(), callCredentials);
    }

    private static void ValidateOptions(ZitadelClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            throw new InvalidOperationException("Zitadel authority is not configured. Set 'ServiceAdmin:Authority' in appsettings.json or environment variables.");
        }

        if (!Uri.TryCreate(options.Authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException($"Zitadel authority '{options.Authority}' is not a valid absolute URI.");
        }

        if (!string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Zitadel authority must use HTTPS.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (!_disposed)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(ZitadelSdk));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var client in _clients.Values.OfType<IDisposable>())
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose gRPC client {ClientType}", client.GetType().FullName);
            }
        }

        _channel.Dispose();
        _disposed = true;
    }
}

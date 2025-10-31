using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using ZitadelSDK.Services;

namespace ZitadelSDK.Extensions;

/// <summary>
/// Extension methods for registering ZITADEL gRPC clients in the dependency injection container.
/// Clients are registered as Scoped services and reuse the singleton GrpcChannel from IZitadelSdk.
/// This approach prevents socket exhaustion and follows gRPC best practices.
/// </summary>
public static class ZitadelClientExtensions
{
    /// <summary>
    /// Registers a ZITADEL gRPC client for direct dependency injection.
    /// The client is registered as Scoped and reuses the shared GrpcChannel from the SDK.
    /// </summary>
    /// <typeparam name="TClient">The gRPC client type to register (must inherit from ClientBase).</typeparam>
    /// <param name="builder">The ZITADEL SDK builder.</param>
    /// <param name="lifetime">The service lifetime (default: Scoped). Use Singleton for long-lived clients or Transient for per-request clients.</param>
    /// <returns>The ZITADEL SDK builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// // In Program.cs
    /// builder.Services.AddZitadelSdk(builder.Configuration)
    ///     .WithJwtAuth(builder.Configuration)
    ///     .AddZitadelClient&lt;UserService.UserServiceClient&gt;()
    ///     .AddZitadelClient&lt;ManagementService.ManagementServiceClient&gt;();
    /// 
    /// // In your controller
    /// public class UserController : ControllerBase
    /// {
    ///     private readonly UserService.UserServiceClient _userClient;
    ///     
    ///     public UserController(UserService.UserServiceClient userClient)
    ///     {
    ///         _userClient = userClient;
    ///     }
    /// }
    /// </code>
    /// </example>
    public static ZitadelSdkBuilder AddZitadelClient<TClient>(
        this ZitadelSdkBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TClient : ClientBase<TClient>
    {
        builder.Services.AddZitadelClient<TClient>(lifetime);
        return builder;
    }

    /// <summary>
    /// Registers multiple ZITADEL gRPC clients for direct dependency injection.
    /// All clients are registered with the specified lifetime and reuse the shared GrpcChannel from the SDK.
    /// </summary>
    /// <param name="builder">The ZITADEL SDK builder.</param>
    /// <param name="lifetime">The service lifetime (default: Scoped).</param>
    /// <param name="clientTypes">The gRPC client types to register.</param>
    /// <returns>The ZITADEL SDK builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddZitadelSdk(builder.Configuration)
    ///     .WithJwtAuth(builder.Configuration)
    ///     .AddZitadelClients(
    ///         ServiceLifetime.Scoped,
    ///         typeof(UserService.UserServiceClient),
    ///         typeof(ManagementService.ManagementServiceClient)
    ///     );
    /// </code>
    /// </example>
    public static ZitadelSdkBuilder AddZitadelClients(
        this ZitadelSdkBuilder builder,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        params Type[] clientTypes)
    {
        builder.Services.AddZitadelClients(lifetime, clientTypes);
        return builder;
    }

    /// <summary>
    /// Registers a ZITADEL gRPC client for direct dependency injection.
    /// The client is registered as Scoped and reuses the shared GrpcChannel from the SDK.
    /// </summary>
    /// <typeparam name="TClient">The gRPC client type to register (must inherit from ClientBase).</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime (default: Scoped). Use Singleton for long-lived clients or Transient for per-request clients.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <example>
    /// <code>
    /// // In Program.cs
    /// builder.Services.AddZitadelSdk(builder.Configuration)
    ///     .WithJwtAuth(builder.Configuration);
    ///     
    /// builder.Services
    ///     .AddZitadelClient&lt;UserService.UserServiceClient&gt;()
    ///     .AddZitadelClient&lt;ManagementService.ManagementServiceClient&gt;();
    /// 
    /// public class UserController : ControllerBase
    /// {
    ///     private readonly UserService.UserServiceClient _userClient;
    ///     
    ///     public UserController(UserService.UserServiceClient userClient)
    ///     {
    ///         _userClient = userClient;
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddZitadelClient<TClient>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TClient : ClientBase<TClient>
    {
        var descriptor = new ServiceDescriptor(
            typeof(TClient),
            sp =>
            {
                var sdk = sp.GetRequiredService<IZitadelSdk>();
                // Use the SDK's optimized GetClient method (no reflection, uses Activator.CreateInstance)
                return sdk.GetClient<TClient>();
            },
            lifetime);

        services.Add(descriptor);

        return services;
    }

    /// <summary>
    /// Registers multiple ZITADEL gRPC clients for direct dependency injection.
    /// All clients are registered with the specified lifetime and reuse the shared GrpcChannel from the SDK.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime (default: Scoped).</param>
    /// <param name="clientTypes">The gRPC client types to register.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddZitadelSdk(builder.Configuration)
    ///     .WithJwtAuth(builder.Configuration)
    ///     .AddZitadelClients(
    ///         ServiceLifetime.Scoped,
    ///         typeof(UserService.UserServiceClient),
    ///         typeof(ManagementService.ManagementServiceClient),
    ///         typeof(SessionService.SessionServiceClient)
    ///     );
    /// </code>
    /// </example>
    public static IServiceCollection AddZitadelClients(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        params Type[] clientTypes)
    {
        foreach (var clientType in clientTypes)
        {
            if (!clientType.IsSubclassOf(typeof(ClientBase)))
            {
                throw new ArgumentException(
                    $"Type {clientType.FullName} must inherit from ClientBase.",
                    nameof(clientTypes));
            }

            var descriptor = new ServiceDescriptor(
                clientType,
                sp =>
                {
                    var sdk = sp.GetRequiredService<IZitadelSdk>();
                    // Activator.CreateInstance is faster than reflection for construction
                    return Activator.CreateInstance(clientType, sdk.CallInvoker)!;
                },
                lifetime);

            services.Add(descriptor);
        }

        return services;
    }
}

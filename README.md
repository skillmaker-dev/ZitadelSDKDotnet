# ZITADEL SDK for ASP.NET Core

A comprehensive ASP.NET Core SDK for integrating with ZITADEL, featuring centralized gRPC client management, flexible authentication methods for your APIs, and a clean, fluent builder API for configuration.

⚠️ **Note**: This is based on the 4.4.0 version of Zitadel, it may not work with older versions of Zitadel, especially < 4.0.0.

## 🌟 Features

- **Fluent Builder API**: Clean, intuitive configuration for the SDK's gRPC clients using `.WithJwtAuth()` and `.WithPatAuth()`.
- **Centralized gRPC Client Management**: Automatic authentication and lifetime management for ZITADEL's gRPC clients.
- **Multiple Authentication Methods**: Supports JWT Profile (recommended for production) and Personal Access Tokens (for development).
- **API Authentication Handlers**: Includes handlers for **OAuth2 Introspection** (for JWT and opaque tokens) and standard **JWT Bearer** validation to secure your web APIs.
- **Strongly-Typed Client Accessors**: Easy, dependency-injectable access to Admin, Auth, Management, Settings, System, and more services.
- **Automatic Token Management**: Caching and auto-refresh for JWT Profile tokens used by the SDK.

---

## 📋 Table of Contents

- [🚀 Quick Start](#-quick-start)
- [⚙️ Configuration](#️-configuration)
- [💻 SDK Usage](#-sdk-usage)
- [🏥 Health Checks](#-health-checks)
- [🔐 Authentication for Web APIs](#-authentication-for-web-apis)
- [📚 Examples](#-examples)
- [🐛 Troubleshooting](#-troubleshooting)
- [🔒 Security Best Practices](#-security-best-practices)

---

## 🚀 Quick Start

### 1. Install the NuGet Package

```bash
dotnet add package ZitadelSDK
```

This SDK includes all necessary dependencies for ZITADEL integration, including gRPC client libraries.

### 2. Configure `appsettings.json`

Choose **either** the JWT Profile (recommended for production) **or** a Personal Access Token for the SDK to authenticate its gRPC calls to ZITADEL.

**🔒 Security Note**: Never commit secrets to source control. Use user secrets for development:
`dotnet user-secrets set "ServiceAdmin:JwtProfile:Key" "your-key"`

#### Option A: JWT Profile (Recommended)

```json
{
  "ServiceAdmin": {
    "Authority": "https://your-instance.zitadel.cloud",
    "JwtProfile": {
      "KeyId": "your-key-id",
      "Key": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
      "UserId": "your-service-account-user-id"
    }
  }
}
```

#### Option B: Personal Access Token (For Development)

```json
{
  "ServiceAdmin": {
    "Authority": "https://your-instance.zitadel.cloud",
    "PersonalAccessToken": "your-pat-token"
  }
}
```

### 3. Register Services in `Program.cs`

Configure the ZITADEL SDK for gRPC clients and add authentication handlers to protect your API endpoints.

```csharp
using ZitadelSDK.Services;
using ZitadelSDK.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// ZITADEL SDK Configuration
// ========================================
// Configure how the SDK authenticates when making gRPC calls TO ZITADEL.
// Choose ONE of the following methods:

// ────────────────────────────────────────
// Method 1: Manual inline configuration
// ────────────────────────────────────────
builder.Services.AddZitadelSdk(config =>
{
    config.Authority = "https://your-instance.zitadel.cloud";
})
    .WithJwtAuth(config =>
    {
        config.KeyId = "your-key-id";
        config.Key = "-----BEGIN RSA PRIVATE KEY-----...";
        config.UserId = "user-id";
        config.AppId = "app-id";
    });

// ────────────────────────────────────────
// Method 2: Bind from appsettings.json section, it expects the "ServiceAdmin" section as shown above.
// ────────────────────────────────────────
builder.Services.AddZitadelSdk(builder.Configuration)
    .WithJwtAuth(config =>
    {
        builder.Configuration.GetSection("ServiceAdmin:JwtProfile").Bind(config);
    });

// ────────────────────────────────────────
// Method 3: Inline configuration from custom source
// ────────────────────────────────────────
var serviceConfig = builder.Configuration
    .GetSection("anotherSection")
    .Get<CustomServiceConfiguration>();

builder.Services.AddZitadelSdk(c =>
{
    c.Authority = serviceConfig.Authority;
}).WithJwtAuth(c =>
{
    c.UserId = serviceConfig.JwtProfile.UserId;
    c.Key = serviceConfig.JwtProfile.Key;
    c.KeyId = serviceConfig.JwtProfile.KeyId;
});

// ────────────────────────────────────────
// Method 4: Auto-load from appsettings.json
// ────────────────────────────────────────
builder.Services.AddZitadelSdk(builder.Configuration) // ServiceAdmin:Authority
    .WithJwtAuth(builder.Configuration);  // Reads ServiceAdmin:JwtProfile section

builder.Services.AddZitadelSdk(builder.Configuration) // ServiceAdmin:Authority
    .WithPatAuth(builder.Configuration);  // Reads ServiceAdmin:PersonalAccessToken key

// ────────────────────────────────────────
// Method 5: Personal Access Token (simple string)
// ────────────────────────────────────────
builder.Services.AddZitadelSdk(builder.Configuration)
    .WithPatAuth(builder.Configuration["ServiceAdmin:PersonalAccessToken"]!);

builder.Services.AddZitadelSdk(builder.Configuration)
    .WithPatAuth("your-pat-token");
```

### 4. Use in Your Code

---

## 💻 SDK Usage

The SDK provides two approaches for accessing ZITADEL gRPC clients, each with different trade-offs:

### Approach 1: Direct Client Injection (Recommended)

**Register specific clients in `Program.cs` and inject them directly into your services.**

✅ **Advantages:**

- **Cleaner DI**: Explicit dependencies in constructor
- **Better testability**: Easy to mock specific clients
- **Prevents socket exhaustion**: Clients properly managed by DI container
- **Type safety**: Compile-time checking of injected clients

```csharp
// In Program.cs - Register the clients you need
builder.Services.AddZitadelSdk(builder.Configuration)
    .WithJwtAuth(builder.Configuration)
    .AddZitadelClient<UserService.UserServiceClient>()
    .AddZitadelClient<ManagementService.ManagementServiceClient>();

// Or register multiple clients at once
builder.Services.AddZitadelSdk(builder.Configuration)
    .WithJwtAuth(builder.Configuration)
    .AddZitadelClients(
        ServiceLifetime.Scoped,  // Default is Scoped
        typeof(UserService.UserServiceClient),
        typeof(ManagementService.ManagementServiceClient),
        typeof(SessionService.SessionServiceClient)
    );
```

```csharp
// In your controller - Inject the client directly
public class UserController : ControllerBase
{
    private readonly UserService.UserServiceClient _userClient;

    public UserController(UserService.UserServiceClient userClient)
    {
        _userClient = userClient;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var response = await _userClient.ListUsersAsync(
            new ListUsersRequest());
        return Ok(response.Result);
    }
}
```

### Approach 2: Using `sdk.GetClient<T>()` (Flexible)

**Inject `IZitadelSdk` and get clients on-demand.**

✅ **Advantages:**

- **Flexibility**: Access any client without pre-registration
- **Dynamic**: Choose clients at runtime
- **Cached**: Clients are reused from internal cache

⚠️ **Note**: While clients are cached internally, registering them in DI (Approach 1) is more idiomatic for ASP.NET Core.

```csharp
public class UserController : ControllerBase
{
    private readonly IZitadelSdk _sdk;

    public UserController(IZitadelSdk sdk)
    {
        _sdk = sdk;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        // Get client on-demand
        var userClient = _sdk.GetClient<UserService.UserServiceClient>();
        var response = await userClient.ListUsersAsync(
            new ListUsersRequest());
        return Ok(response.Result);
    }
}
```

---

## 🏥 Health Checks

The SDK includes built-in health checks to monitor ZITADEL service availability. Health checks call the `/debug/ready` endpoint to verify ZITADEL is operational.

### Adding Health Checks

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddZitadel("https://your-instance.zitadel.cloud"); // Authority is required

// Or with custom configuration
builder.Services.AddHealthChecks()
    .AddZitadel(
        authority: "https://your-instance.zitadel.cloud",
        name: "zitadel-health",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "external", "identity" }
    );
```

### Health Check Response

The health check will return:

- **Healthy**: ZITADEL service is responding and ready
- **Unhealthy**: ZITADEL service is not responding or returned an error status
- **Degraded**: Can be configured for specific failure scenarios

### Health Check Endpoint

Configure the health check endpoint in your application:

```csharp
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Name == "zitadel"
});
```

### Example Response

```json
{
  "status": "Healthy",
  "results": {
    "zitadel": {
      "status": "Healthy",
      "description": "ZITADEL service is healthy",
      "data": {}
    }
  }
}
```

---

## ⚙️ Configuration

### How to Get Credentials

#### JWT Profile

1.  Log in to your ZITADEL instance.
2.  Create or select a **service account**.
3.  Navigate to **Keys** → **New Key**.
4.  Download the generated JSON file.
5.  Extract `keyId`, `key`, and `userId` and place them in `appsettings.json` or user secrets.

#### Personal Access Token (PAT)

1.  Log in to your ZITADEL instance.
2.  Navigate to your service user.
3.  Create a new **Personal Access Token (PAT)**.
4.  Copy the token and store it securely.

### Error Handling

Wrap gRPC calls in a `try-catch` block to handle potential `RpcException` errors.

```csharp
try
{
    var response = await _userClient.ListUsersAsync(new());
    return Ok(response);
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
{
    _logger.LogError(ex, "ZITADEL is unreachable");
    return StatusCode(503, new { error = "Service unavailable" });
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
{
    _logger.LogError(ex, "Authentication failed. Check SDK credentials.");
    return StatusCode(401, new { error = "Unauthorized" });
}
catch (RpcException ex)
{
    _logger.LogError(ex, "An unexpected ZITADEL API error occurred.");
    return StatusCode(502, new { error = ex.Status.Detail });
}
```

---

## 🔐 Authentication for Web APIs

These authentication handlers are for protecting your API endpoints, not for authenticating the SDK's gRPC client.

### OAuth2 Introspection

Recommended for web APIs that need to validate both JWT and opaque access tokens. It calls the ZITADEL introspection endpoint to validate tokens.

#### Configuration

**Option 1: Using Client ID and Secret (Basic Authentication)**

```csharp
builder.Services.AddAuthentication("ZITADEL")
    .AddZitadelIntrospection(options =>
    {
        options.Authority = "https://your-instance.zitadel.cloud";
        options.ClientId = "your-client-id@your-project";
        options.ClientSecret = "your-client-secret";
        options.EnableCaching = true; // Recommended for performance
        options.CacheDuration = TimeSpan.FromMinutes(10);
    });
```

**Option 2: Using JWT Profile (Recommended)**

For enhanced security, you can use JWT Profile authentication instead of client secrets. This is the recommended approach for production environments.

```csharp
builder.Services.AddAuthentication("ZITADEL")
    .AddZitadelIntrospection(options =>
    {
        options.Authority = "https://your-instance.zitadel.cloud";
        options.EnableCaching = true;
        options.CacheDuration = TimeSpan.FromMinutes(5);
        options.JwtProfile = new()
        {
            ClientId = "your-client-id@your-project",  // Required for introspection
            Key = "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----",
            KeyId = "your-key-id"
        };
    });
```

### JWT Bearer

```csharp
// ────────────────────────────────────────
// JWT Bearer (validates JWT tokens locally)
// ────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddZitadelJwtBearer(options =>
    {
        options.Authority = "https://your-instance.zitadel.cloud";
        options.Audience = "client-id";
        // OR options.Audiences = [ "client-id-1", "client-id-2" ];
    });
```

**⚠️ Important**: JWT Bearer validation only works with **JWT access tokens**. If ZITADEL is configured to issue **opaque access tokens**, you must use **OAuth2 Introspection** instead.

### Choosing Between JWT Bearer and Introspection

| Token Type              | Method        | When to Use                                                 |
| ----------------------- | ------------- | ----------------------------------------------------------- |
| **JWT Access Token**    | JWT Bearer    | Fast, local validation. No network calls.                   |
| **Opaque Access Token** | Introspection | Required for opaque tokens. Makes network calls to ZITADEL. |

#### **When ZITADEL Issues JWT vs Opaque Tokens**

**JWT Access Tokens** (use `.AddZitadelJwtBearer()`):

- ✅ **Public Clients** with "Access Token Type: JWT"
- ✅ **Confidential Clients** with "Access Token Type: JWT"
- ✅ **SPA/Mobile apps** can use JWT for faster validation

**Opaque Access Tokens** (use `.AddZitadelIntrospection()`):

- 🔒 **Public Clients** with "Access Token Type: Opaque" (default for security)
- 🔒 **Confidential Clients** with "Access Token Type: Opaque"
- 🔒 **High-security scenarios** where tokens shouldn't be readable

## 📚 Examples

### Example 1: List All Users

```csharp
[HttpGet("users")]
public async Task<IActionResult> ListUsers([FromQuery] int limit = 10)
{
    var userClient = _sdk.GetClient<Zitadel.User.V2.UserService.UserServiceClient>();
    var request = new ListUsersRequest
    {
        Query = new ListQuery { Limit = (uint)Math.Min(limit, 100) }
    };
    var response = await userClient.ListUsersAsync(request);
    return Ok(new
    {
        users = response.Result,
        total = response.Details?.TotalResult ?? 0
    });
}
```

### Example 2: Get Current User Info from a Protected Endpoint

This shows how to access user claims after they have been authenticated by the JWT Bearer or Introspection handler.

```csharp
[Authorize]
[HttpGet("me")]
public IActionResult GetCurrentUser()
{
    var userId = User.FindFirst(ZitadelClaimTypes.UserId)?.Value;
    var userName = User.Identity?.Name;
    var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
    var orgId = User.FindFirst(ZitadelClaimTypes.OrganizationId)?.Value;

    return Ok(new { userId, userName, roles, organizationId = orgId });
}
```

### Example 3: Organization-Specific Role Authorization

Check if a user has a specific role within a specific organization.

```csharp
[Authorize]
[HttpGet("admin/dashboard")]
public IActionResult GetAdminDashboard()
{
    var orgId = "123456789012345678"; // The organization to check
    var requiredRole = ZitadelClaimTypes.OrganizationRole(orgId);

    // Check if the user has the "admin" role within the specified organization
    if (!User.HasClaim(requiredRole, "admin"))
    {
        return Forbid();
    }

    return Ok(new { message = "Access granted to admin dashboard." });
}
```

---

## 🐛 Troubleshooting

| Error                                      | Solution                                                                                                                                                                                                |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **"No credential provider configured"**    | Ensure you have added either `.WithJwtAuth()` or `.WithPatAuth()` after `AddZitadelSdk()` in `Program.cs`.                                                                                              |
| **"JWT Profile configuration is invalid"** | Verify that `KeyId`, `Key`, and `UserId` are all present and correctly formatted in your configuration for the `JwtProfile` section.                                                                    |
| **"Unauthenticated" with gRPC calls**      | Check that your SDK's PAT or JWT Profile credentials are correct and that the service account has the necessary permissions (**IAM Owner** or **Org Owner** role) in ZITADEL.                           |
| **"401 Unauthorized" on API endpoints**    | This is an API authentication issue, not an SDK one. Check if the token sent by the client is valid, not expired, and that your `AddZitadelJwtBearer` or `AddZitadelIntrospection` options are correct. |
| **"Zitadel authority must be provided"**   | Make sure the `Authority` property is set in the `ServiceAdmin` section of your `appsettings.json`.                                                                                                     |

---

## 🔒 Security Best Practices

1.  **Never Commit Secrets**: Use user secrets, environment variables, or a managed secret store like Azure Key Vault.
2.  **Prefer JWT Profile for M2M**: Use the JWT Profile method for service-to-service authentication in production over static Personal Access Tokens.
3.  **Use HTTPS**: Always set `RequireHttpsMetadata = true` on your authentication handlers in production.
4.  **Principle of Least Privilege**: Grant only the permissions necessary for your service account.
5.  **Enable Caching**: For the introspection handler, enable caching to reduce latency and load on your ZITADEL instance.
6.  **Rotate Keys**: Regularly rotate service account keys and Personal Access Tokens.

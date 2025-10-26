using Zitadel.Management.V1;
using Zitadel.Settings.V2beta;
using ZitadelSDK.Extensions;
using ZitadelSDK.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// ========================================
// ZITADEL SDK Configuration
// ========================================
// Configure how the SDK authenticates when making gRPC calls TO ZITADEL.

// Option 1: Use sdk.GetClient<T>() in your code (no pre-registration needed)
builder.Services.AddZitadelSdk(builder.Configuration)
    .WithJwtAuth(config =>
    {
        builder.Configuration.GetSection("ServiceAdmin:JwtProfile").Bind(config);
    });

// Option 2: Register specific clients for direct injection (RECOMMENDED to prevent socket exhaustion)
// This registers the clients in DI so you can inject them directly into your controllers
builder.Services.AddZitadelClient<SettingsService.SettingsServiceClient>()
    .AddZitadelClient<ManagementService.ManagementServiceClient>();

// Option 3: Register multiple clients at once
// builder.Services.AddZitadelSdk(builder.Configuration)
//     .WithJwtAuth(builder.Configuration)
//     .AddZitadelClients(
//         ServiceLifetime.Scoped,
//         typeof(UserService.UserServiceClient),
//         typeof(ManagementService.ManagementServiceClient)
//     );

// ========================================
// ALTERNATIVE SDK AUTHENTICATION METHODS
// ========================================

// Manual inline configuration
// builder.Services.AddZitadelSdk(builder.Configuration)
//     .WithJwtAuth(config =>
//     {
//         config.KeyId = "your-key-id";
//         config.Key = "-----BEGIN RSA PRIVATE KEY-----...";
//         config.UserId = "user-id";
//         config.AppId = "app-id";
//     });

// Auto-load from appsettings.json (requires: using ZitadelTest.Extensions;)
// builder.Services.AddZitadelSdk(builder.Configuration)
//     .WithJwtAuth(builder.Configuration);  // Reads from ServiceAdmin:JwtProfile section

// Personal Access Token (simple string)
// builder.Services.AddZitadelSdk(builder.Configuration)
//     .WithPatAuth(builder.Configuration["ServiceAdmin:PersonalAccessToken"]!);

// Resolve token from DI (e.g., from secret manager)
// builder.Services.AddZitadelSdk(builder.Configuration)
//     .WithPatAuth(sp =>
//     {
//         var secretManager = sp.GetRequiredService<ISecretManager>();
//         return secretManager.GetSecret("ZitadelPat");
//     });

// ========================================
// ASP.NET Authentication (Optional)
// ========================================
// If you want to protect your API endpoints with [Authorize], configure ASP.NET authentication.
// This is SEPARATE from the SDK configuration above (which is for making gRPC calls TO ZITADEL).
//
// For OAuth2 Introspection (validates access tokens by calling ZITADEL):
//builder.Services.AddAuthentication()
//    .AddZitadelIntrospection(options =>
//    {
//        options.Authority = builder.Configuration["ServiceAdmin:Authority"]!;
//        options.ClientId = builder.Configuration["ServiceAdmin:JwtProfile:AppId"]!;
//        options.ClientSecret = builder.Configuration["ServiceAdmin:JwtProfile:Key"]!;
//        options.CacheDuration = TimeSpan.FromMinutes(5);
//    });

// For JWT Bearer (validates JWT tokens locally):
//builder.Services.AddAuthentication()
//    .AddZitadelJwtBearer(options =>
//    {
//        options.Authority = builder.Configuration["ServiceAdmin:Authority"]!;
//        options.Audience = builder.Configuration["ServiceAdmin:JwtProfile:AppId"]!;
//    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

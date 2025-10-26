using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Zitadel.Settings.V2beta;
using ZitadelSDK.Services;
using static Zitadel.Settings.V2beta.SettingsService;

namespace ZitadelTest.Controllers;

/// <summary>
/// Example controller demonstrating two approaches for using ZITADEL gRPC clients:
/// 1. Injecting IZitadelSdk into method parameters using [FromServices]
/// 2. Injecting specific client directly into method parameters using [FromServices]
/// </summary>
[ApiController]
[Route("[controller]")]
public class ZitadelController(ILogger<ZitadelController> logger) : ControllerBase
{
    /// <summary>
    /// Gets active identity providers from ZITADEL.
    /// APPROACH 1: Injects IZitadelSdk via [FromServices] and uses GetClient&lt;T&gt;()
    /// </summary>
    [HttpGet("identity-providers", Name = "GetActiveIdentityProviders")]
    public async Task<IActionResult> GetIdentityProviders([FromServices] IZitadelSdk sdk)
    {
        var request = new GetActiveIdentityProvidersRequest();
        try
        {
            logger.LogInformation("Requesting active identity providers from ZITADEL (using IZitadelSdk).");

            // Get client from SDK on-demand
            var settingsClient = sdk.GetClient<SettingsServiceClient>();
            var response = await settingsClient.GetActiveIdentityProvidersAsync(request);

            return Ok(response);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Failed to fetch identity providers from ZITADEL");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Status.Detail });
        }
    }

    /// <summary>
    /// Gets active identity providers from ZITADEL.
    /// APPROACH 2: Injects SettingsServiceClient directly via [FromServices]
    /// </summary>
    [HttpGet("identity-providers-direct", Name = "GetActiveIdentityProvidersDirect")]
    public async Task<IActionResult> GetIdentityProvidersDirect([FromServices] SettingsServiceClient client)
    {
        var request = new GetActiveIdentityProvidersRequest();
        try
        {
            logger.LogInformation("Requesting active identity providers from ZITADEL (using direct client injection).");

            // Use the injected client directly
            var response = await client.GetActiveIdentityProvidersAsync(request);

            return Ok(response);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Failed to fetch identity providers from ZITADEL");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Status.Detail });
        }
    }
}

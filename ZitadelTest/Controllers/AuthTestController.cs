using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZitadelTest.Controllers;

/// <summary>
/// Controller for testing different ZITADEL authentication flows.
/// Consolidates testing into two main validation methods: Local JWT validation and Server-side Introspection.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthTestController : ControllerBase
{
    /// <summary>
    /// Tests authentication for any token validated locally (using JWT Bearer).
    /// This endpoint is suitable for tokens obtained via PKCE, Authorization Code, or Implicit flows, 
    /// provided they are in JWT format and can be validated using ZITADEL's public keys.
    /// </summary>
    /// <remarks>
    /// Use Postman to obtain a token using your preferred OAuth 2.0 flow.
    /// Set the Authorization header as: Bearer {your_token}
    /// </remarks>
    /// <returns>A message indicating successful local validation and the associated claims.</returns>
    [HttpGet("token")]
    [Authorize(AuthenticationSchemes = "ZITADEL")]
    public IActionResult GetTokenTest()
    {
        return Ok(new
        {
            message = "Successfully authenticated via local JWT validation!",
            user = User.Identity?.Name,
            authenticationType = User.Identity?.AuthenticationType,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }

    /// <summary>
    /// Tests authentication using the Introspection flow.
    /// The SDK will call ZITADEL's introspection endpoint to validate the token.
    /// This is required for opaque tokens and can also be used for JWTs.
    /// </summary>
    /// <remarks>
    /// This endpoint supports both JWT and opaque tokens.
    /// The validation is performed server-side by calling ZITADEL's introspection endpoint.
    /// </remarks>
    /// <returns>A message indicating successful authentication via Introspection and the associated claims.</returns>
    [HttpGet("introspection")]
    [Authorize(AuthenticationSchemes = "ZITADEL-Introspection")]
    public IActionResult GetIntrospectionTest()
    {
        return Ok(new
        {
            message = "Successfully authenticated via server-side Introspection!",
            user = User.Identity?.Name,
            authenticationType = User.Identity?.AuthenticationType,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Event callbacks for custom ZITADEL introspection authentication.
/// </summary>
public class ZitadelIntrospectionEvents
{
    /// <summary>
    /// Called after the token has been introspected and a principal has been created.
    /// </summary>
    public Func<ZitadelTokenValidatedContext, Task> OnTokenValidated { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Called before sending client assertion fields for JWT profile authentication.
    /// </summary>
    public Func<ZitadelUpdateClientAssertionContext, Task> OnUpdateClientAssertion { get; set; } = _ => Task.CompletedTask;

    /// <summary>
    /// Called when introspection fails due to an exception.
    /// </summary>
    public Func<ZitadelIntrospectionFailedContext, Task> OnAuthenticationFailed { get; set; } = _ => Task.CompletedTask;
}

/// <summary>
/// Context for token validated event.
/// </summary>
/// <remarks>
/// Initializes a new context.
/// </remarks>
/// <param name="httpContext">Current HTTP context.</param>
/// <param name="scheme">Authentication scheme.</param>
/// <param name="options">Current introspection options.</param>
public class ZitadelTokenValidatedContext(HttpContext httpContext, AuthenticationScheme scheme, ZitadelIntrospectionOptions options)
{
    /// <summary>
    /// Gets the current HTTP context.
    /// </summary>
    public HttpContext HttpContext { get; } = httpContext;

    /// <summary>
    /// Gets the active authentication scheme.
    /// </summary>
    public AuthenticationScheme Scheme { get; } = scheme;

    /// <summary>
    /// Gets the active options instance.
    /// </summary>
    public ZitadelIntrospectionOptions Options { get; } = options;

    /// <summary>
    /// Gets or sets the access token being validated.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the principal produced from introspection.
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }
}

/// <summary>
/// Context for updating client assertion values before introspection requests.
/// </summary>
/// <remarks>
/// Initializes a new context.
/// </remarks>
/// <param name="httpContext">Current HTTP context.</param>
/// <param name="scheme">Authentication scheme.</param>
/// <param name="options">Current introspection options.</param>
public class ZitadelUpdateClientAssertionContext(HttpContext httpContext, AuthenticationScheme scheme, ZitadelIntrospectionOptions options)
{
    /// <summary>
    /// Gets the current HTTP context.
    /// </summary>
    public HttpContext HttpContext { get; } = httpContext;

    /// <summary>
    /// Gets the active authentication scheme.
    /// </summary>
    public AuthenticationScheme Scheme { get; } = scheme;

    /// <summary>
    /// Gets the active options instance.
    /// </summary>
    public ZitadelIntrospectionOptions Options { get; } = options;

    /// <summary>
    /// Gets or sets the JWT client assertion value.
    /// </summary>
    public string? ClientAssertion { get; set; }

    /// <summary>
    /// Gets or sets the client assertion type.
    /// </summary>
    public string? ClientAssertionType { get; set; }

    /// <summary>
    /// Gets or sets client assertion expiration timestamp.
    /// </summary>
    public DateTimeOffset? ClientAssertionExpirationTime { get; set; }
}

/// <summary>
/// Context for introspection authentication failures.
/// </summary>
/// <remarks>
/// Initializes a new context.
/// </remarks>
/// <param name="httpContext">Current HTTP context.</param>
/// <param name="scheme">Authentication scheme.</param>
/// <param name="options">Current introspection options.</param>
/// <param name="exception">Thrown exception.</param>
public class ZitadelIntrospectionFailedContext(HttpContext httpContext, AuthenticationScheme scheme, ZitadelIntrospectionOptions options, Exception exception)
{
    /// <summary>
    /// Gets the current HTTP context.
    /// </summary>
    public HttpContext HttpContext { get; } = httpContext;

    /// <summary>
    /// Gets the active authentication scheme.
    /// </summary>
    public AuthenticationScheme Scheme { get; } = scheme;

    /// <summary>
    /// Gets the active options instance.
    /// </summary>
    public ZitadelIntrospectionOptions Options { get; } = options;

    /// <summary>
    /// Gets the thrown exception.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the optional override authentication result.
    /// </summary>
    public AuthenticateResult? Result { get; private set; }

    /// <summary>
    /// Overrides the pipeline result with no result.
    /// </summary>
    public void NoResult() => Result = AuthenticateResult.NoResult();

    /// <summary>
    /// Overrides the pipeline result with a failure message.
    /// </summary>
    /// <param name="message">Failure message.</param>
    public void Fail(string message) => Result = AuthenticateResult.Fail(message);

    /// <summary>
    /// Overrides the pipeline result with a failure exception.
    /// </summary>
    /// <param name="exception">Failure exception.</param>
    public void Fail(Exception exception) => Result = AuthenticateResult.Fail(exception);
}
